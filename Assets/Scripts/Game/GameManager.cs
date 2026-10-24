using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>Game state, wave spawning, weapon unlocks, score, pause and scene flow.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { Playing, Paused, GameOver }

    [Serializable]
    public class EnemyEntry
    {
        public Enemy prefab;
        public int minWave = 1;
        public float weight = 1f;
        [Tooltip("Weight added per wave after minWave.")]
        public float weightPerWave;
    }

    public const string MenuScene = "MainMenu";

    [Header("Player")]
    public PlayerController player;
    public PlayerHealth playerHealth;
    public WeaponManager weapons;

    [Header("Enemies")]
    public EnemyEntry[] enemyTable;
    public Enemy bossPrefab;
    public int bossEvery = 5;
    public Transform[] spawnPoints;
    public NavMeshSurface navMeshSurface;

    [Header("Weapons")]
    [Tooltip("Wave at which each gun (same order as WeaponManager.guns) unlocks. 0 = starting weapon.")]
    public int[] weaponUnlockWaves;

    [Header("Waves")]
    public float firstWaveDelay = 4f;
    public float intermission = 7f;
    public int baseEnemyCount = 5;
    public float enemiesPerWave = 2.5f;
    public float spawnInterval = 0.6f;
    public int maxAlive = 24;
    public float healthScalePerWave = 0.1f;
    public float minSpawnDistance = 20f;
    public float healBetweenWaves = 30f;

    [Header("Front arc (measured from the player's start point)")]
    [Tooltip("Enemies only spawn inside this arc in front of the player (degrees). 180 = the whole front half.")]
    [SerializeField] float spawnArcAngle = 160f;
    [Tooltip("Distance from the player for arc spawns when no spawn gate is inside the arc.")]
    [SerializeField] float spawnDistance = 40f;
    [Tooltip("An enemy that stays behind the player's view this long is moved back to the front (seconds).")]
    [SerializeField] float behindRespawnDelay = 1f;

    /// <summary>
    /// Arc enemies spawn and circle in. With a fixed camera it's narrowed to what the camera actually shows
    /// (its horizontal FOV, a little inside the edges) so nothing appears off-screen.
    /// </summary>
    public float SpawnArcAngle => player != null ? Mathf.Min(spawnArcAngle, 2f * (player.ViewHalfAngle - 6f)) : spawnArcAngle;
    public float BehindRespawnDelay => behindRespawnDelay;
    public float FrontYaw => player != null ? player.FrontYaw : 0f;

    public State CurrentState { get; private set; } = State.Playing;
    public bool IsPlaying => CurrentState == State.Playing;
    public int Wave { get; private set; }
    public int Score { get; private set; }
    public int Kills { get; private set; }
    public bool NewBest { get; private set; }
    public int EnemiesRemaining => aliveEnemies + toSpawn;
    public float IntermissionTimeLeft { get; private set; }
    public float SurvivalTime { get; private set; }
    public bool IsBossWave => bossEvery > 0 && Wave > 0 && Wave % bossEvery == 0;
    /// <summary>First living boss, for the HUD health bar.</summary>
    public Enemy ActiveBoss
    {
        get
        {
            bosses.RemoveAll(b => b == null || b.IsDead);
            return bosses.Count > 0 ? bosses[0] : null;
        }
    }

    int aliveEnemies, toSpawn;
    readonly List<Enemy> bosses = new List<Enemy>();

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.Apply();
        // Shells, debris and corpses (Ignore Raycast layer) shouldn't shove the player around.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) Physics.IgnoreLayerCollision(playerLayer, LayerMask.NameToLayer("Ignore Raycast"));
        ProceduralAudio.Warmup();
        // Bake at runtime so the arena can be edited freely without re-baking.
        if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        LockCursor(true);
        player.fixedPosition = true; // front-facing shooter: always hold the start point
        playerHealth.Died += OnPlayerDied;
        StartCoroutine(WaveLoop());
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (CurrentState == State.Playing)
        {
            SurvivalTime += Time.deltaTime;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetPaused(true);
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }
        else if (CurrentState == State.Paused && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            SetPaused(false);
        }
    }

    IEnumerator WaveLoop()
    {
        float delay = firstWaveDelay;
        while (CurrentState != State.GameOver)
        {
            IntermissionTimeLeft = delay;
            while (IntermissionTimeLeft > 0f)
            {
                IntermissionTimeLeft -= Time.deltaTime;
                yield return null;
            }
            IntermissionTimeLeft = 0f;

            Wave++;
            UnlockWeaponsForWave();
            var hud = HUD.Instance;
            if (hud != null) hud.ShowBanner(IsBossWave ? $"WAVE {Wave} - BOSS" : $"WAVE {Wave}");

            int count = Mathf.RoundToInt((baseEnemyCount + enemiesPerWave * (Wave - 1)) * GameSettings.SpawnCountMult);
            int bossCount = IsBossWave && bossPrefab != null ? Wave / bossEvery : 0;
            toSpawn = count + bossCount;

            // Bosses first, so the fight is about them.
            for (int b = 0; b < bossCount; b++)
            {
                while (!SpawnEnemy(bossPrefab, true)) yield return new WaitForSeconds(0.3f);
                toSpawn--;
                yield return new WaitForSeconds(1.5f);
            }

            while (toSpawn > 0)
            {
                if (CurrentState == State.GameOver) yield break;
                if (aliveEnemies < maxAlive && SpawnEnemy(PickEnemy(), false)) toSpawn--;
                yield return new WaitForSeconds(spawnInterval);
            }

            while (aliveEnemies > 0) yield return null;
            if (CurrentState == State.GameOver) yield break;

            if (hud != null) hud.ShowBanner("WAVE CLEARED");
            if (playerHealth.Heal(healBetweenWaves) && hud != null) hud.AddMessage($"+{healBetweenWaves:0} HEALTH", new Color(0.4f, 1f, 0.5f));
            // Standing still means no looting runs, so every cleared wave also resupplies ammo.
            if (player.fixedPosition && weapons.AddAmmoToAll() && hud != null) hud.AddMessage("+AMMO RESUPPLY", new Color(1f, 0.85f, 0.3f));
            delay = intermission;
        }
    }

    void UnlockWeaponsForWave()
    {
        if (weaponUnlockWaves == null) return;
        for (int i = 0; i < weaponUnlockWaves.Length && i < weapons.guns.Length; i++)
        {
            if (weaponUnlockWaves[i] != Wave || weapons.IsOwned(i)) continue;
            weapons.Unlock(i);
            ProceduralAudio.Play2D(ProceduralAudio.Unlock, 0.7f, 0f);
            if (HUD.Instance != null) HUD.Instance.ShowUnlock(weapons.guns[i].displayName, i + 1);
        }
    }

    Enemy PickEnemy()
    {
        float total = 0f;
        foreach (var e in enemyTable)
            if (e.prefab != null && Wave >= e.minWave) total += Weight(e);
        float roll = Random.value * total;
        foreach (var e in enemyTable)
        {
            if (e.prefab == null || Wave < e.minWave) continue;
            roll -= Weight(e);
            if (roll <= 0f) return e.prefab;
        }
        return enemyTable[0].prefab;
    }

    float Weight(EnemyEntry e) => Mathf.Max(0f, e.weight + e.weightPerWave * (Wave - e.minWave));

    /// <summary>Horizontal angle between the player's front direction and a position, seen from the start point (0..180).</summary>
    float AngleFromFront(Vector3 position)
    {
        Vector3 d = position - player.FrontOrigin;
        d.y = 0f;
        return d.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(player.FrontForward, d);
    }

    public bool InSpawnArc(Vector3 position) => AngleFromFront(position) <= SpawnArcAngle * 0.5f;

    /// <summary>Outside what the player can see (fixed camera: off the side of the screen; else past the yaw limit).</summary>
    public bool IsBehindPlayer(Vector3 position) => player != null && AngleFromFront(position) > player.ViewHalfAngle + 5f;

    /// <summary>
    /// A NavMesh position inside the front arc: a spawn gate inside the arc (at least minSpawnDistance away),
    /// or else a point on the arc at spawnDistance. False if nothing valid was found this time.
    /// </summary>
    public bool TryGetFrontSpawnPosition(out Vector3 position)
    {
        position = default;
        if (player == null) return false;
        Vector3 origin = player.FrontOrigin;

        var candidates = new List<Transform>();
        if (spawnPoints != null)
            foreach (var sp in spawnPoints)
                if (sp != null && InSpawnArc(sp.position) && Vector3.Distance(sp.position, origin) >= minSpawnDistance)
                    candidates.Add(sp);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 p;
            if (candidates.Count > 0 && attempt < 4)
                p = candidates[Random.Range(0, candidates.Count)].position + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            else
            {
                float half = SpawnArcAngle * 0.5f;
                p = origin + Quaternion.Euler(0f, player.FrontYaw + Random.Range(-half, half), 0f) * Vector3.forward * spawnDistance;
            }
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 6f, NavMesh.AllAreas) && InSpawnArc(hit.position))
            {
                position = hit.position;
                return true;
            }
        }
        return false;
    }

    bool SpawnEnemy(Enemy prefab, bool boss)
    {
        if (prefab == null || !TryGetFrontSpawnPosition(out Vector3 spawnPos)) return false;

        Vector3 look = player.transform.position - spawnPos;
        look.y = 0f;
        var enemy = Instantiate(prefab, spawnPos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        enemy.maxHealth *= (1f + healthScalePerWave * (Wave - 1)) * GameSettings.EnemyHealthMult;
        enemy.moveSpeed *= Mathf.Min(1f + 0.02f * (Wave - 1), 1.25f);
        if (boss)
        {
            enemy.isBoss = true;
            enemy.maxHealth *= 1.5f;
            enemy.scoreValue *= 2;
            bosses.Add(enemy);
        }
        aliveEnemies++;
        return true;
    }

    public void OnEnemyKilled(Enemy enemy, bool byPlayer)
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        if (!byPlayer) return;
        Kills++;
        int points = enemy.scoreValue + 10 * (Wave - 1);
        Score += points;
        if (HUD.Instance != null) HUD.Instance.AddMessage(enemy.isBoss ? $"{enemy.displayName.ToUpper()} DESTROYED +{points}" : $"+{points}", enemy.isBoss ? new Color(1f, 0.8f, 0.3f) : Color.white);
    }

    void OnPlayerDied()
    {
        CurrentState = State.GameOver;
        NewBest = GameSettings.RecordRun(Score, Wave);
        LockCursor(false);
    }

    public void SetPaused(bool paused)
    {
        if (CurrentState == State.GameOver) return;
        CurrentState = paused ? State.Paused : State.Playing;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        LockCursor(!paused);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (CurrentState != State.GameOver && Score > 0) GameSettings.RecordRun(Score, Wave);
        SceneManager.LoadScene(MenuScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
