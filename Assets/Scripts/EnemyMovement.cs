using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyAttack))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Attack Range")]
    [Tooltip("Distance at which the enemy stops and begins attacking.")]
    [SerializeField] private float attackRange = 2f;

    [Header("Knockback Settings")]
    [Tooltip("Drag deceleration factor applied during knockback.")]
    [SerializeField] private float knockbackDrag = 6f;

    private Rigidbody2D rb;
    private EnemyAttack enemyAttack;
    private Enemy enemy;
    private Transform player;

    private bool isKnockedBack;
    private float knockbackTimer;

    public bool IsKnockedBack => isKnockedBack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyAttack = GetComponent<EnemyAttack>();
        enemy = GetComponent<Enemy>();

        // Top-down 2D setup: no gravity and never allow physics rotation
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Reset()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    /// <summary>
    /// Applies a physics knockback impulse to the enemy, interrupting attacks and temporarily disabling movement AI.
    /// </summary>
    public void ApplyKnockback(Vector2 direction, float force, float duration = 0.25f)
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (enemy != null && !enemy.IsAlive) return;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (enemyAttack == null)
            enemyAttack = GetComponent<EnemyAttack>();

        isKnockedBack = true;
        knockbackTimer = duration;

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearVelocity = direction.normalized * force;
        }

        // Cancel any pending or winding-up attack
        if (enemyAttack != null && enemyAttack.IsAttacking)
        {
            enemyAttack.CancelAttack();
        }
    }

    private void FixedUpdate()
    {
        if (player == null || (enemy != null && !enemy.IsAlive)) return;

        // Handle active knockback state
        if (isKnockedBack)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            // Smoothly decelerate back to zero
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, knockbackDrag * Time.fixedDeltaTime);

            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= attackRange || enemyAttack.IsAttacking)
        {
            // Stand completely still while in range or mid-attack
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            if (distance <= attackRange)
                enemyAttack.TryAttack();
        }
        else
        {
            // Walk toward the player while outside attack range, keeping rotation locked
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
