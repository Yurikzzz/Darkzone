using UnityEngine;
using System;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Destination")]
    [Tooltip("Transform outside the Darkzone where the player respawns on death. If null, automatically falls back to the safe zone entrance.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Timings")]
    [Tooltip("Time in seconds to wait after death for the death animation to finish playing before starting the respawn.")]
    [Min(0f)]
    [SerializeField] private float deathAnimationDuration = 1.0f;

    [Tooltip("Duration of the screen fade to black.")]
    [Min(0.05f)]
    [SerializeField] private float fadeDuration = 0.35f;

    [Tooltip("Hold time on the black screen while teleporting and resetting state.")]
    [Min(0f)]
    [SerializeField] private float blackScreenHold = 0.5f;

    [Header("Options")]
    [Tooltip("Whether to use the screen fade transition on respawn.")]
    [SerializeField] private bool useScreenFade = true;

    [Tooltip("Refill all ammunition back to full on respawn.")]
    [SerializeField] private bool refillAmmoOnRespawn = true;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerAnimation playerAnimation;
    private HandsAiming handsAiming;
    private WeaponController weaponController;
    private PlayerMeleeAttack playerMelee;
    private Rigidbody2D rb;

    private Vector3 initialPosition;
    private bool isRespawning;

    public event Action OnRespawnStarted;
    public event Action OnRespawnCompleted;

    public bool IsRespawning => isRespawning;
    public Transform RespawnPoint
    {
        get => respawnPoint;
        set => respawnPoint = value;
    }

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimation = GetComponent<PlayerAnimation>();
        handsAiming = GetComponent<HandsAiming>();
        weaponController = GetComponentInChildren<WeaponController>();
        playerMelee = GetComponent<PlayerMeleeAttack>();
        rb = GetComponent<Rigidbody2D>();

        initialPosition = transform.position;

        if (respawnPoint == null)
        {
            FindDefaultRespawnPoint();
        }
    }

    private void FindDefaultRespawnPoint()
    {
        GameObject enterObj = GameObject.Find("ZoneEnter");
        if (enterObj != null)
        {
            respawnPoint = enterObj.transform;
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (isRespawning) return;
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        isRespawning = true;
        OnRespawnStarted?.Invoke();

        // 1. Wait for death animation to finish playing
        yield return new WaitForSeconds(deathAnimationDuration);

        // 2. Determine target position outside the zone
        Vector3 targetPos = (respawnPoint != null) ? respawnPoint.position : initialPosition;

        // 3. Screen fade sequence & teleportation
        if (useScreenFade)
        {
            if (ScreenFader.Instance == null)
            {
                new GameObject("ScreenFader").AddComponent<ScreenFader>();
            }

            yield return ScreenFader.Instance.StartCoroutine(
                ScreenFader.Instance.DoFadeSequence(fadeDuration, blackScreenHold, () =>
                {
                    ExecuteRespawn(targetPos);
                })
            );
        }
        else
        {
            ExecuteRespawn(targetPos);
        }

        // Ensure movement is definitively restored
        if (playerMovement != null)
        {
            playerMovement.CanMove = true;
        }

        isRespawning = false;
        OnRespawnCompleted?.Invoke();
    }

    private void ExecuteRespawn(Vector3 targetPos)
    {
        // Teleport player back out of the zone
        transform.position = targetPos;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Reset the Darkzone completely: despawn all enemies, reset spawners, clear projectiles
        if (DarkzoneManager.Instance != null)
        {
            DarkzoneManager.Instance.ResetZone();
        }

        // Reset player health
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        // Reset movement & dash state (restores CanMove = true)
        if (playerMovement != null)
        {
            playerMovement.ResetState();
        }

        // Reset hands aiming & visual
        if (handsAiming != null)
        {
            handsAiming.ResetHands();
        }

        // Reset animation state back to Idle
        if (playerAnimation != null)
        {
            playerAnimation.ResetAnimationState();
        }

        // Reset melee state
        if (playerMelee != null)
        {
            playerMelee.ResetMelee();
        }

        // Refill weapons
        if (refillAmmoOnRespawn && weaponController != null)
        {
            weaponController.RefillAmmo();
        }

        Debug.Log($"[Darkzone] Player respawned outside the zone at {targetPos}!");
    }
}
