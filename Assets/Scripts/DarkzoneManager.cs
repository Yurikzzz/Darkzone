using System;
using UnityEngine;

/// <summary>
/// Central manager for Darkzone state. Tracks whether the player is currently
/// inside the zone and raises events that other systems can subscribe to
/// (e.g. enemy spawners, loot generation, ambient changes).
/// </summary>
public class DarkzoneManager : MonoBehaviour
{
    public static DarkzoneManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The root DARKZONE GameObject that contains all zone content.")]
    [SerializeField] private GameObject darkzoneRoot;

    /// <summary>True while the player is inside the Darkzone.</summary>
    public bool IsPlayerInZone { get; private set; }

    // ── Events ──────────────────────────────────────────────────────────
    // Subscribe to these from spawners, loot managers, UI, audio, etc.

    /// <summary>Fired when the player enters the Darkzone.</summary>
    public event Action OnPlayerEnteredZone;

    /// <summary>Fired when the player leaves the Darkzone (evac or death).</summary>
    public event Action OnPlayerLeftZone;

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Called by ZoneTeleporter when the player uses the zone entrance.
    /// </summary>
    public void EnterZone()
    {
        if (IsPlayerInZone) return;

        IsPlayerInZone = true;
        OnPlayerEnteredZone?.Invoke();
    }

    /// <summary>
    /// Called when the player leaves the Darkzone (evacuation, death respawn, or teleporter).
    /// Resets the zone back to its initial state.
    /// </summary>
    public void LeaveZone()
    {
        ResetZone();
    }

    /// <summary>
    /// Resets the entire Darkzone back to its pristine state:
    /// despawns all active enemies, resets all enemy spawners, and clears leftover projectiles.
    /// </summary>
    public void ResetZone()
    {
        IsPlayerInZone = false;

        // 1. Reset all EnemySpawners in the scene
        EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            if (spawner != null)
                spawner.ResetSpawner();
        }

        // 2. Destroy any remaining Enemy objects in the scene
        Enemy[] remainingEnemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var enemy in remainingEnemies)
        {
            if (enemy != null && enemy.gameObject.scene.IsValid())
            {
                SafeDestroy(enemy.gameObject);
            }
        }

        // 3. Clean up any active bullets/trails in the zone
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var bullet in bullets)
        {
            if (bullet != null)
                SafeDestroy(bullet.gameObject);
        }

        // 4. Clean up any leftover attack indicator objects
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go != null && go.name.StartsWith("AttackIndicator"))
            {
                SafeDestroy(go);
            }
        }

        OnPlayerLeftZone?.Invoke();
        Debug.Log("[DarkzoneManager] Zone reset complete! All enemies despawned and spawners reset.");
    }

    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}
