using UnityEngine;

/// <summary>
/// Attach to an interaction point (ZoneEnter or ZoneEvac).
/// When the player stands inside the trigger and presses the interact key,
/// they are teleported to the linked destination.
///
/// For ExitZone (evac) teleporters, a countdown timer is displayed above the
/// object that the player must wait through before being teleported out.
/// If the player leaves the trigger during the countdown, it cancels.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ZoneTeleporter : MonoBehaviour
{
    public enum TeleporterType
    {
        /// <summary>Teleports the player INTO the Darkzone.</summary>
        EnterZone,
        /// <summary>Teleports the player OUT of the Darkzone (evac).</summary>
        ExitZone
    }

    [Header("Configuration")]
    [SerializeField] private TeleporterType type = TeleporterType.EnterZone;

    [Tooltip("The Transform the player will be moved to on interaction.")]
    [SerializeField] private Transform destination;

    [Tooltip("Key the player presses to interact.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Evac Timer")]
    [Tooltip("Seconds the player must wait at an ExitZone before being teleported out.")]
    [SerializeField] private float evacDuration = 15f;

    private bool playerInRange;
    private EvacTimerUI evacTimer;
    private InteractionPromptUI prompt;

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Set up the interaction prompt (auto-shows when player is in range)
        prompt = GetComponent<InteractionPromptUI>();
        if (prompt == null)
            prompt = gameObject.AddComponent<InteractionPromptUI>();

        prompt.PromptText = type == TeleporterType.EnterZone
            ? $"[{interactKey}] Enter Darkzone"
            : $"[{interactKey}] Evacuate";

        // Set up the evac timer for ExitZone teleporters
        if (type == TeleporterType.ExitZone)
        {
            evacTimer = GetComponent<EvacTimerUI>();
            if (evacTimer == null)
                evacTimer = gameObject.AddComponent<EvacTimerUI>();

            evacTimer.OnTimerComplete += OnEvacTimerComplete;
        }
    }

    private void OnDestroy()
    {
        if (evacTimer != null)
            evacTimer.OnTimerComplete -= OnEvacTimerComplete;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            // Cancel evac if the player walks away
            if (evacTimer != null && evacTimer.IsRunning)
            {
                evacTimer.CancelTimer();
                prompt.PromptText = $"[{interactKey}] Evacuate";
            }
        }
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (!Input.GetKeyDown(interactKey)) return;

        // If an evac timer is already counting down, ignore additional presses
        if (evacTimer != null && evacTimer.IsRunning) return;

        if (type == TeleporterType.ExitZone && evacTimer != null)
        {
            // Start the countdown instead of teleporting immediately
            evacTimer.StartTimer();
            // Update prompt to show waiting state
            prompt.PromptText = "Evacuating...";
        }
        else
        {
            Teleport();
        }
    }

    // ── Timer callback ─────────────────────────────────────────────────

    private void OnEvacTimerComplete()
    {
        // Only teleport if the player is still in range
        if (playerInRange)
            StartCoroutine(TeleportSequence());

        prompt.PromptText = $"[{interactKey}] Evacuate";
    }

    // ── Teleportation ──────────────────────────────────────────────────

    private void Teleport()
    {
        StartCoroutine(TeleportSequence());
    }

    private System.Collections.IEnumerator TeleportSequence()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) yield break;

        // Ensure ScreenFader exists
        if (ScreenFader.Instance == null)
        {
            new GameObject("ScreenFader").AddComponent<ScreenFader>();
        }

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.CanMove = false; // Block movement

        // Fade in (0.25s), wait (1s) and perform teleport, then fade out (0.25s)
        yield return ScreenFader.Instance.StartCoroutine(
            ScreenFader.Instance.DoFadeSequence(0.25f, 1f, () =>
            {
                // Execute the actual teleport while the screen is black
                player.transform.position = destination.position;

                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                if (DarkzoneManager.Instance != null)
                {
                    if (type == TeleporterType.EnterZone)
                        DarkzoneManager.Instance.EnterZone();
                    else
                        DarkzoneManager.Instance.LeaveZone();
                }

                playerInRange = false;
            })
        );

        if (movement != null)
            movement.CanMove = true; // Restore movement
    }
}
