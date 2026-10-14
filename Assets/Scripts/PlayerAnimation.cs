using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int MoveDirectionHash = Animator.StringToHash("MoveDirection");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
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

    private void Update()
    {
        if (animator == null || playerMovement == null) return;

        if (playerHealth != null && !playerHealth.IsAlive) return;

        animator.SetBool(IsMovingHash, playerMovement.IsMoving);
        animator.SetBool(IsRunningHash, playerMovement.IsRunning);

        // Determine facing direction: flipX true means facing Left, false means facing Right
        float facingDir = (playerSpriteRenderer != null && playerSpriteRenderer.flipX) ? -1f : 1f;
        Vector2 moveInput = playerMovement.MoveInput;

        // If walking horizontally in the opposite direction of facing, play animation in reverse (-1)
        float moveDirection = 1f;
        if (Mathf.Abs(moveInput.x) > 0.01f && (facingDir * moveInput.x) < 0f)
        {
            moveDirection = -1f;
        }

        animator.SetFloat(MoveDirectionHash, moveDirection);
    }

    private void HandleDeath()
    {
        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsDeadHash, true);
        }
    }

    /// <summary>
    /// Resets animation parameters back to Idle.
    /// </summary>
    public void ResetAnimationState()
    {
        if (animator != null)
        {
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(IsMovingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetFloat(MoveDirectionHash, 1f);
            animator.Play("Idle", 0, 0f);
        }
    }
}

