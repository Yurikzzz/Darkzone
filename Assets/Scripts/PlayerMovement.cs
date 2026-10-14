using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] [Range(0f, 1f)] private float walkSpeedRatio = 0.5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.8f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    
    public bool CanMove { get; set; } = true;

    public bool IsRunning { get; private set; }
    public bool IsMoving => moveInput != Vector2.zero && CanMove;
    public bool IsDashing => isDashing;
    public Vector2 MoveInput => moveInput;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<PlayerHealth>();
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
        CanMove = false;
        moveInput = Vector2.zero;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Resets movement, dashing, and cooldowns back to ready state.
    /// </summary>
    public void ResetState()
    {
        CanMove = true;
        isDashing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        moveInput = Vector2.zero;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (!CanMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
            return;
        }

        // Read input
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        // Dash
        if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0f && moveInput != Vector2.zero)
        {
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            dashDirection = moveInput;
        }
    }

    private void FixedUpdate()
    {
        if (!CanMove)
        {
            rb.linearVelocity = Vector2.zero;
            IsRunning = false;
            return;
        }

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            IsRunning = false;
            return;
        }

        IsRunning = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && moveInput != Vector2.zero;
        float speed = IsRunning ? runSpeed : runSpeed * walkSpeedRatio;
        rb.linearVelocity = moveInput * speed;
    }
}