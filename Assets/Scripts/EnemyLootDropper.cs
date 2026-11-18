using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to enemies to roll for consumable drops on death.
/// Drops items like Medkits and Frag Grenades with tweakable drop rates.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyLootDropper : MonoBehaviour
{
    [Header("Drop Settings")]
    [Tooltip("Probability of dropping a consumable on death (0.0 = 0%, 1.0 = 100%).")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.5f;

    [Tooltip("List of possible consumable drops.")]
    [SerializeField] private List<ConsumableData> dropPool = new List<ConsumableData>();

    [Tooltip("Physics pop force applied to the dropped item.")]
    [SerializeField] private float popForce = 2.2f;

    [Header("Coin Drop Settings")]
    [Tooltip("Probability of dropping coins on death (0.0 = 0%, 1.0 = 100%).")]
    [Range(0f, 1f)]
    [SerializeField] private float coinDropChance = 1f;

    [Tooltip("Minimum number of coins dropped when a coin drop succeeds.")]
    [Min(0)]
    [SerializeField] private int minCoins = 1;

    [Tooltip("Maximum number of coins dropped when a coin drop succeeds.")]
    [Min(0)]
    [SerializeField] private int maxCoins = 3;

    [Tooltip("Currency value granted by each dropped coin.")]
    [Min(1)]
    [SerializeField] private int coinValue = 1;

    [Tooltip("Scatter pop speed applied to dropped coins.")]
    [SerializeField] private float coinPopForce = 2.4f;

    private Enemy enemy;
    private bool hasDropped;

    public float DropChance
    {
        get => dropChance;
        set => dropChance = Mathf.Clamp01(value);
    }

    public List<ConsumableData> DropPool => dropPool;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.OnDeath += HandleDeath;
        }

        // Auto-find default consumables if pool is empty
        if (dropPool == null || dropPool.Count == 0)
        {
            LoadDefaultDropPool();
        }
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnDeath -= HandleDeath;
        }
    }

    private void LoadDefaultDropPool()
    {
        dropPool = new List<ConsumableData>();
        ConsumableData[] allConsumables = Resources.FindObjectsOfTypeAll<ConsumableData>();
        foreach (var c in allConsumables)
        {
            if (c != null && !c.isRuntimeInstance && !dropPool.Contains(c))
            {
                dropPool.Add(c);
            }
        }
    }

    private void HandleDeath()
    {
        if (hasDropped) return;
        hasDropped = true;

        DropCoins();

        if (dropPool == null || dropPool.Count == 0)
        {
            LoadDefaultDropPool();
        }

        if (dropPool == null || dropPool.Count == 0) return;

        // Roll drop chance
        if (Random.value <= dropChance)
        {
            DropItem();
        }
    }

    private void DropCoins()
    {
        if (coinDropChance <= 0f || maxCoins <= 0) return;
        if (Random.value > coinDropChance) return;

        int min = Mathf.Max(0, minCoins);
        int max = Mathf.Max(min, maxCoins);
        int count = Random.Range(min, max + 1);
        if (count <= 0) return;

        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        float step = (Mathf.PI * 2f) / count;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + i * step + Random.Range(-0.35f, 0.35f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = coinPopForce * Random.Range(0.75f, 1.25f);
            CoinPickup.Spawn(transform.position, coinValue, dir * speed);
        }
    }

    private void DropItem()
    {
        ConsumableData chosen = dropPool[Random.Range(0, dropPool.Count)];
        if (chosen == null) return;

        ConsumableData instance = chosen.CreateRuntimeInstance();

        GameObject pickupObj = new GameObject($"Pickup_{instance.consumableName}");
        pickupObj.transform.position = transform.position;

        ConsumablePickup pickup = pickupObj.AddComponent<ConsumablePickup>();
        pickup.Initialize(instance);

        Vector2 randomDir = Random.insideUnitCircle.normalized;
        if (randomDir == Vector2.zero) randomDir = Vector2.up;
        pickup.Pop(randomDir, popForce);
    }
}
