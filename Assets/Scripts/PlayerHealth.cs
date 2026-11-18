using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit Feedback")]
    [Tooltip("Camera shake intensity when the player takes damage.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float hitShakeIntensity = 0.12f;

    [Tooltip("Duration in seconds of the hit camera shake.")]
    [Range(0.02f, 0.5f)]
    [SerializeField] private float hitShakeDuration = 0.18f;

    private float currentHealth;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (hitShakeIntensity > 0f && hitShakeDuration > 0f)
        {
            float scale = Mathf.Clamp(damage / 20f, 0.75f, 1.5f);
            CameraFollow.Shake(hitShakeIntensity * scale, hitShakeDuration);
        }

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Restores player health back to maximum and triggers the health changed event.
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        OnDeath?.Invoke();
        // Player death can be handled here (e.g. respawn, game over screen)
        Debug.Log("Player died!");
    }
}

