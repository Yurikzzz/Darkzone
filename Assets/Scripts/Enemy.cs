using UnityEngine;
using System;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 50f;

    private float currentHealth;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0f;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    protected virtual void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

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

    protected virtual void Die()
    {
        OnDeath?.Invoke();

        if (GetComponent<EnemyLootDropper>() == null)
        {
            int count = UnityEngine.Random.Range(1, 3);
            float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + i * (Mathf.PI * 2f / count) + UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                CoinPickup.Spawn(transform.position, 1, dir * UnityEngine.Random.Range(1.8f, 2.8f));
            }
        }

        Destroy(gameObject);
    }
}

