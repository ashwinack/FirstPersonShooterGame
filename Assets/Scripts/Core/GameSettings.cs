using UnityEngine;

public enum Difficulty { Easy, Normal, Hard }

/// <summary>Player preferences persisted in PlayerPrefs, plus difficulty multipliers.</summary>
public static class GameSettings
{
    static bool loaded;
    static float sensitivity, fov, volume;
    static Difficulty difficulty;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        sensitivity = PlayerPrefs.GetFloat("Sensitivity", 0.1f);
        fov = PlayerPrefs.GetFloat("Fov", 75f);
        volume = PlayerPrefs.GetFloat("Volume", 0.8f);
        difficulty = (Difficulty)PlayerPrefs.GetInt("Difficulty", (int)Difficulty.Normal);
    }

    public static float Sensitivity
    {
        get { Load(); return sensitivity; }
        set { Load(); if (Mathf.Approximately(sensitivity, value)) return; sensitivity = value; PlayerPrefs.SetFloat("Sensitivity", value); PlayerPrefs.Save(); }
    }

    public static float Fov
    {
        get { Load(); return fov; }
        set { Load(); if (Mathf.Approximately(fov, value)) return; fov = value; PlayerPrefs.SetFloat("Fov", value); PlayerPrefs.Save(); }
    }

    public static float Volume
    {
        get { Load(); return volume; }
        set { Load(); if (Mathf.Approximately(volume, value)) return; volume = value; PlayerPrefs.SetFloat("Volume", value); AudioListener.volume = value; PlayerPrefs.Save(); }
    }

    public static Difficulty Difficulty
    {
        get { Load(); return difficulty; }
        set { Load(); if (difficulty == value) return; difficulty = value; PlayerPrefs.SetInt("Difficulty", (int)value); PlayerPrefs.Save(); }
    }

    /// <summary>Survival turret mode: the player can't walk, only look and shoot.</summary>
    public static bool FixedPosition
    {
        get => PlayerPrefs.GetInt("FixedPosition", 1) == 1;
        set { PlayerPrefs.SetInt("FixedPosition", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static float EnemyHealthMult => Difficulty switch { Difficulty.Easy => 0.75f, Difficulty.Hard => 1.3f, _ => 1f };
    public static float EnemyDamageMult => Difficulty switch { Difficulty.Easy => 0.6f, Difficulty.Hard => 1.35f, _ => 1f };
    public static float SpawnCountMult => Difficulty switch { Difficulty.Easy => 0.8f, Difficulty.Hard => 1.25f, _ => 1f };

    public static int GetBestScore(Difficulty d) => PlayerPrefs.GetInt("BestScore_" + d, 0);
    public static int GetBestWave(Difficulty d) => PlayerPrefs.GetInt("BestWave_" + d, 0);

    /// <summary>Records a run; returns true if it's a new best score.</summary>
    public static bool RecordRun(int score, int wave)
    {
        var d = Difficulty;
        if (wave > GetBestWave(d)) PlayerPrefs.SetInt("BestWave_" + d, wave);
        bool best = score > GetBestScore(d);
        if (best) PlayerPrefs.SetInt("BestScore_" + d, score);
        PlayerPrefs.Save();
        return best;
    }

    public static void Apply() => AudioListener.volume = Volume;
    public static void Save() => PlayerPrefs.Save();
}
