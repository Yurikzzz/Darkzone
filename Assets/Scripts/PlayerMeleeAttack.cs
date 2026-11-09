using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Key to trigger the melee attack.")]
    [SerializeField] private KeyCode meleeKey = KeyCode.V;

    [Header("Combat Stats")]
    [Tooltip("Damage dealt to enemies within the melee sweep.")]
    [Min(0f)]
    [SerializeField] private float damage = 45f;

    [Tooltip("Cooldown in seconds between melee attacks.")]
    [Min(0.05f)]
    [SerializeField] private float attackCooldown = 0.45f;

    [Tooltip("Reach / radius of the melee slash from the player.")]
    [Min(0.5f)]
    [SerializeField] private float attackRange = 1.6f;

    [Tooltip("Total arc angle in degrees of the melee sweep.")]
    [Range(30f, 180f)]
    [SerializeField] private float arcAngle = 110f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback impulse force applied to enemies hit by the melee slash.")]
    [Min(0f)]
    [SerializeField] private float knockbackForce = 12f;

    [Tooltip("Duration in seconds that the enemy is pushed back and stunned.")]
    [Min(0.05f)]
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("Visual Slash Settings")]
    [Tooltip("Sprite for the knife.")]
    [SerializeField] private Sprite knifeSprite;

    [Tooltip("Duration in seconds of the slash animation.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float slashDuration = 0.16f;

    [Tooltip("Distance from the player where the knife orbits during the slash.")]
    [Min(0.2f)]
    [SerializeField] private float slashDistance = 0.75f;

    [Tooltip("Scale of the knife sprite.")]
    [SerializeField] private Vector3 knifeScale = new Vector3(1.8f, 1.8f, 1f);

    [Tooltip("Color tint of the knife during the attack.")]
    [SerializeField] private Color slashColor = Color.white;

    [Tooltip("Whether to spawn a luminous blade trail behind the knife tip.")]
    [SerializeField] private bool showTrail = true;

    [Tooltip("Color of the blade slash trail.")]
    [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("Whether to briefly hide hands while slashing.")]
    [SerializeField] private bool hideHandsDuringSlash = false;

    private float cooldownTimer;
    private bool isAttacking;
    private Camera mainCam;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private Transform handsTransform;
    private GameObject knifeVisualObj;
    private SpriteRenderer knifeRenderer;
    private TrailRenderer knifeTrail;
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    public event Action OnMeleeAttack;
    public event Action<IDamageable, float> OnMeleeHit;

    public bool IsAttacking => isAttacking;
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
    public float CooldownRatio => attackCooldown > 0f ? Mathf.Clamp01(cooldownTimer / attackCooldown) : 0f;

    private void Awake()
    {
        mainCam = Camera.main;
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        handsTransform = transform.Find("Hands");

        SetupKnifeVisual();
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
        StopAllCoroutines();
        if (knifeVisualObj != null)
            knifeVisualObj.SetActive(false);
        if (knifeTrail != null)
            knifeTrail.enabled = false;
        isAttacking = false;
    }

    /// <summary>
    /// Resets melee cooldown and visual states back to idle.
    /// </summary>
    public void ResetMelee()
    {
        HandleDeath();
        cooldownTimer = 0f;
    }

    private void SetupKnifeVisual()
    {
        if (knifeVisualObj != null) return;

        knifeVisualObj = new GameObject("KnifeSlashVisual");
        knifeVisualObj.transform.SetParent(transform);
        knifeVisualObj.SetActive(false);

        knifeRenderer = knifeVisualObj.AddComponent<SpriteRenderer>();
        knifeRenderer.sprite = knifeSprite;
        knifeRenderer.color = slashColor;
        knifeRenderer.sortingOrder = 15;

        // Create knife tip child with TrailRenderer for swift slash effect
        GameObject tipObj = new GameObject("KnifeTip");
        tipObj.transform.SetParent(knifeVisualObj.transform);
        tipObj.transform.localPosition = new Vector3(0.25f, 0f, 0f);

        knifeTrail = tipObj.AddComponent<TrailRenderer>();
        knifeTrail.time = 0.1f;
        knifeTrail.startWidth = 0.35f;
        knifeTrail.endWidth = 0.02f;
        knifeTrail.sortingOrder = 14;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");

        if (shader != null)
        {
            Material mat = new Material(shader);
            knifeTrail.material = mat;
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        knifeTrail.colorGradient = gradient;
        knifeTrail.enabled = false;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (playerMovement != null && !playerMovement.CanMove) return;

        if (Input.GetKeyDown(meleeKey) && cooldownTimer <= 0f && !isAttacking)
        {
            StartCoroutine(PerformSlashRoutine());
        }
    }

    private IEnumerator PerformSlashRoutine()
    {
        isAttacking = true;
        cooldownTimer = attackCooldown;
        hitTargets.Clear();
        OnMeleeAttack?.Invoke();

        if (hideHandsDuringSlash && handsTransform != null)
            handsTransform.gameObject.SetActive(false);

        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 origin = transform.position;
        Vector2 aimDir = ((Vector2)mouseWorld - origin).normalized;
        if (aimDir == Vector2.zero) aimDir = Vector2.right;

        float baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        // Facing direction determines sweep direction
        bool facingLeft = aimDir.x < 0f;
        float startAngle = facingLeft ? (baseAngle - arcAngle * 0.5f) : (baseAngle + arcAngle * 0.5f);
        float endAngle = facingLeft ? (baseAngle + arcAngle * 0.5f) : (baseAngle - arcAngle * 0.5f);

        knifeVisualObj.SetActive(true);
        if (knifeRenderer != null)
        {
            if (knifeRenderer.sprite == null && knifeSprite != null)
                knifeRenderer.sprite = knifeSprite;
            knifeRenderer.color = slashColor;
        }

        if (showTrail && knifeTrail != null)
        {
            knifeTrail.Clear();
            knifeTrail.enabled = true;
        }

        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slashDuration);
            // Snappy slash curve
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
            float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);

            Vector2 dir = new Vector2(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentAngle * Mathf.Deg2Rad)
            );

            Vector2 knifePos = (Vector2)transform.position + dir * slashDistance;
            knifeVisualObj.transform.position = knifePos;

            // Angle knife so blade points outward and cuts through the arc
            float bladeRotationOffset = facingLeft ? 45f : -45f;
            knifeVisualObj.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle + bladeRotationOffset);

            // Flip Y scale if facing left so blade orientation stays consistent
            knifeVisualObj.transform.localScale = new Vector3(
                knifeScale.x,
                facingLeft ? -Mathf.Abs(knifeScale.y) : Mathf.Abs(knifeScale.y),
                knifeScale.z
            );

            // Hit detection during the swing
            CheckMeleeHits((Vector2)transform.position, baseAngle);

            yield return null;
        }

        // Final hit check
        CheckMeleeHits((Vector2)transform.position, baseAngle);

        if (showTrail && knifeTrail != null)
        {
            knifeTrail.enabled = false;
        }

        knifeVisualObj.SetActive(false);

        if (hideHandsDuringSlash && handsTransform != null)
            handsTransform.gameObject.SetActive(true);

        isAttacking = false;
    }

    private void CheckMeleeHits(Vector2 origin, float baseAngle)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, attackRange);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Player")) continue;

            Vector2 toTarget = (Vector2)col.bounds.ClosestPoint(origin) - origin;
            float distance = toTarget.magnitude;

            if (distance <= attackRange)
            {
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float angleDelta = Mathf.DeltaAngle(baseAngle, targetAngle);

                if (Mathf.Abs(angleDelta) <= arcAngle * 0.55f)
                {
                    IDamageable damageable = col.GetComponent<IDamageable>();
                    if (damageable != null && hitTargets.Add(damageable))
                    {
                        damageable.TakeDamage(damage);
                        OnMeleeHit?.Invoke(damageable, damage);

                        // Calculate knockback direction away from the player
                        Vector2 knockbackDir = ((Vector2)col.transform.position - origin).normalized;
                        if (knockbackDir == Vector2.zero)
                        {
                            knockbackDir = new Vector2(
                                Mathf.Cos(baseAngle * Mathf.Deg2Rad),
                                Mathf.Sin(baseAngle * Mathf.Deg2Rad)
                            );
                        }

                        // Apply knockback to EnemyMovement if present
                        EnemyMovement enemyMovement = col.GetComponent<EnemyMovement>();
                        if (enemyMovement != null)
                        {
                            enemyMovement.ApplyKnockback(knockbackDir, knockbackForce, knockbackDuration);
                        }
                        else
                        {
                            Rigidbody2D colRb = col.GetComponent<Rigidbody2D>();
                            if (colRb != null)
                            {
                                colRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
