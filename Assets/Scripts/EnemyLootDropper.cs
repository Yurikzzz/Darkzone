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
