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

    /// <summary>Fired when the player leaves the Darkzone (evac).</summary>
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
    /// Called by ZoneTeleporter when the player uses the evac point.
    /// </summary>
    public void LeaveZone()
    {
        if (!IsPlayerInZone) return;

        IsPlayerInZone = false;
        OnPlayerLeftZone?.Invoke();
    }
}

