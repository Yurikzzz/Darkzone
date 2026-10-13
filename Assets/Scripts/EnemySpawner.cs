using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("The Enemy prefab (or scene Enemy) to spawn.")]
        public GameObject prefab;

        [Tooltip("Relative weight/chance for this enemy to be chosen. Higher values make it more common.")]
        [Min(0f)]
        public float weight = 1f;
    }

    [Header("Enemy Pool")]
    [Tooltip("List of enemy prefabs and their relative spawn weights.")]
    [SerializeField] private List<SpawnEntry> enemyPool = new List<SpawnEntry>();

    [Header("Spawner Settings")]
    [Tooltip("Time in seconds between each spawn while the player is in range.")]
    [SerializeField] private float spawnInterval = 5f;

    [Tooltip("Maximum number of enemies from this spawner alive at the same time (0 = unlimited).")]
    [SerializeField] private int maxActiveEnemies = 5;

    [Header("Range Settings")]
    [Tooltip("Distance from the spawner within which the player activates periodic spawning.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Radius around the spawner center where enemies can be placed when spawned.")]
    [SerializeField] private float spawnRadius = 1f;

    private Transform player;
    private float spawnTimer;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly Dictionary<int, GameObject> runtimeTemplates = new Dictionary<int, GameObject>();

    public float DetectionRange
    {
        get => detectionRange;
        set => detectionRange = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        // If the pool was left completely empty, auto-populate with an Enemy from the scene as a fallback
        if (enemyPool.Count == 0)
        {
            Enemy existingEnemy = FindFirstObjectByType<Enemy>();
            if (existingEnemy != null)
            {
                enemyPool.Add(new SpawnEntry
                {
                    prefab = existingEnemy.gameObject,
                    weight = 1f
                });
            }
        }

        // For any entry that references a live scene object instead of a project prefab asset,
        // cache an inactive template copy so killing the scene enemy doesn't break future spawns
        for (int i = 0; i < enemyPool.Count; i++)
        {
            GameObject prefab = enemyPool[i]?.prefab;
            if (prefab != null && prefab.scene.IsValid())
            {
                GameObject template = Instantiate(prefab);
                template.name = prefab.name + "_Template";
                template.SetActive(false);
                runtimeTemplates[i] = template;
            }
        }
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        // Allow an immediate first spawn as soon as the player enters range
        spawnTimer = 0f;
    }

    private void OnDestroy()
    {
        foreach (var kvp in runtimeTemplates)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        runtimeTemplates.Clear();
    }

    private void Update()
    {
        if (player == null || enemyPool.Count == 0) return;

        // Tick down cooldown timer
        if (spawnTimer > 0f)
            spawnTimer -= Time.deltaTime;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > detectionRange) return;

        // Clean up destroyed enemies from tracking list
        activeEnemies.RemoveAll(e => e == null);

        if (maxActiveEnemies > 0 && activeEnemies.Count >= maxActiveEnemies)
            return;

        if (spawnTimer <= 0f)
        {
            GameObject chosenSource = PickWeightedEnemy(out string baseName);
            if (chosenSource != null)
            {
                SpawnEnemy(chosenSource, baseName);
                spawnTimer = spawnInterval;
            }
        }
    }

    private GameObject GetEntrySource(int index)
    {
        if (runtimeTemplates.TryGetValue(index, out GameObject template) && template != null)
            return template;

        return enemyPool[index]?.prefab;
    }

    private GameObject PickWeightedEnemy(out string baseName)
    {
        baseName = "Enemy";
        float totalWeight = 0f;

        for (int i = 0; i < enemyPool.Count; i++)
        {
            SpawnEntry entry = enemyPool[i];
            if (entry == null || entry.weight <= 0f || GetEntrySource(i) == null)
                continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < enemyPool.Count; i++)
        {
            SpawnEntry entry = enemyPool[i];
            GameObject source = GetEntrySource(i);
            if (entry == null || entry.weight <= 0f || source == null)
                continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                baseName = entry.prefab != null ? entry.prefab.name : "Enemy";
                return source;
            }
        }

        return null;
    }

    private void SpawnEnemy(GameObject source, string baseName)
    {
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = new Vector3(
            transform.position.x + offset.x,
            transform.position.y + offset.y,
            0f);

        GameObject spawned = Instantiate(source, spawnPos, Quaternion.identity);
        spawned.name = baseName;
        spawned.SetActive(true);
        activeEnemies.Add(spawned);
    }

    private void OnDrawGizmos()
    {
        // Always show a subtle ring for the detection range so it's easy to size in the Scene view
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

    private void OnDrawGizmosSelected()
    {
        // Highlight detection range in bright orange and spawn radius in red when selected
        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
