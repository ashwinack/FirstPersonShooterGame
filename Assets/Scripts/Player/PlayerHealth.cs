using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float regenDelay = 5f;
    public float regenRate = 8f;

    public float Current { get; private set; }
    public bool IsDead { get; private set; }

    public event Action Died;
    /// <summary>Fired with the attacker's world position (callers pass it as hitPoint).</summary>
    public event Action<Vector3> Damaged;

    float lastDamageTime = -999f;
    PlayerController controller;

    void Awake()
    {
        Current = maxHealth;
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (!IsDead && Current < maxHealth && Time.time - lastDamageTime > regenDelay)
            Current = Mathf.Min(maxHealth, Current + regenRate * Time.deltaTime);
    }

    public bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead) return false;

        Current -= amount;
        lastDamageTime = Time.time;
        ProceduralAudio.Play2D(ProceduralAudio.Hurt, 0.6f);
        if (controller) controller.AddShake(0.3f);
        Damaged?.Invoke(hitPoint);

        if (Current <= 0f)
        {
            Current = 0f;
            IsDead = true;
            if (controller) controller.EnterDeathView();
            Died?.Invoke();
            return true;
        }
        return false;
    }

    public bool Heal(float amount)
    {
        if (IsDead || Current >= maxHealth) return false;
        Current = Mathf.Min(maxHealth, Current + amount);
        return true;
    }
}
