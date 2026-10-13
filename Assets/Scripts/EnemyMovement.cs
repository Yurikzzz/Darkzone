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

    private Rigidbody2D rb;
    private EnemyAttack enemyAttack;
    private Enemy enemy;
    private Transform player;

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

    private void FixedUpdate()
    {
        if (player == null || (enemy != null && !enemy.IsAlive)) return;

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

