using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum Kind { Health, Ammo }

    public Kind kind;
    public float healAmount = 35f;
    public float lifetime = 25f;
    public float radius = 1.4f;

    Vector3 basePos;
    float spawnTime, magnetSpeed = 6f;

    void Start()
    {
        basePos = transform.position;
        spawnTime = Time.time;
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        var gm = GameManager.Instance;

        // Turret mode: the player can't walk over to loot, so drops fly in (only the ones that are useful).
        if (gm != null && gm.player != null && gm.player.fixedPosition)
        {
            bool wanted = kind != Kind.Health || gm.playerHealth.Current < gm.playerHealth.maxHealth;
            if (wanted)
            {
                magnetSpeed = Mathf.Min(magnetSpeed + 14f * Time.deltaTime, 22f);
                basePos = Vector3.MoveTowards(basePos, gm.player.transform.position + Vector3.up * 0.9f, magnetSpeed * Time.deltaTime);
            }
        }

        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 3f) * 0.12f);
        if (age > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (gm == null || gm.player == null) return;
        Vector3 d = gm.player.transform.position + Vector3.up * 0.9f - transform.position;
        if (d.sqrMagnitude > radius * radius) return;

        bool used = kind == Kind.Health ? gm.playerHealth.Heal(healAmount) : gm.weapons.AddAmmoToAll();
        if (!used) return;

        ProceduralAudio.Play2D(ProceduralAudio.Pickup, 0.6f, 0f);
        if (HUD.Instance != null)
            HUD.Instance.AddMessage(kind == Kind.Health ? "+HEALTH" : "+AMMO",
                kind == Kind.Health ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.3f));
        Destroy(gameObject);
    }

    public static Pickup Spawn(Kind kind, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.name = kind + "Pickup";
        go.transform.position = position;
        go.transform.localScale = kind == Kind.Health ? new Vector3(0.35f, 0.35f, 0.35f) : new Vector3(0.45f, 0.25f, 0.3f);

        var lib = FXLibrary.Instance;
        if (lib != null)
            go.GetComponent<Renderer>().sharedMaterial = kind == Kind.Health ? lib.healthPickup : lib.ammoPickup;

        var pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        return pickup;
    }
}
