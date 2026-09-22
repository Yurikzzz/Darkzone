using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float windUpDuration = 1.5f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private Vector2 hitboxSize = new Vector2(2f, 0.8f);
    [SerializeField] private float hitboxOffset = 1.5f;
    [SerializeField] private float cooldown = 2f;

    private bool isAttacking;
    private float windUpTimer;
    private float cooldownTimer;

    private Transform attackIndicator;
    private Transform fillTransform;
    private Transform player;

    private const float BORDER_THICKNESS = 0.04f;

    private void Awake()
    {
        CreateVisuals();
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void OnDestroy()
    {
        if (attackIndicator != null)
            Destroy(attackIndicator.gameObject);
    }

    private void CreateVisuals()
    {
        // Create a 1x1 white sprite at runtime (no asset dependency)
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        // Root object – not parented to enemy so it stays in place during wind-up
        attackIndicator = new GameObject("AttackIndicator").transform;
        attackIndicator.gameObject.SetActive(false);

        // Semi-transparent red area (hitbox preview)
        MakeSprite("AreaPreview", whiteSprite, new Color(1f, 0f, 0f, 0.15f),
            Vector3.zero, hitboxSize, 10);

        // Fill bar (grows left-to-right during wind-up)
        GameObject fillObj = MakeSprite("Fill", whiteSprite, new Color(1f, 0f, 0f, 0.5f),
            new Vector3(-hitboxSize.x / 2f, 0f, 0f), Vector2.zero, 11);
        fillTransform = fillObj.transform;

        // Red outline – four thin edge sprites
        float hw = hitboxSize.x / 2f;
        float hh = hitboxSize.y / 2f;
        Color borderColor = new Color(1f, 0f, 0f, 0.8f);

        MakeSprite("BorderTop", whiteSprite, borderColor,
            new Vector3(0f, hh - BORDER_THICKNESS / 2f, 0f),
            new Vector2(hitboxSize.x, BORDER_THICKNESS), 12);

        MakeSprite("BorderBottom", whiteSprite, borderColor,
            new Vector3(0f, -hh + BORDER_THICKNESS / 2f, 0f),
            new Vector2(hitboxSize.x, BORDER_THICKNESS), 12);

        MakeSprite("BorderLeft", whiteSprite, borderColor,
            new Vector3(-hw + BORDER_THICKNESS / 2f, 0f, 0f),
            new Vector2(BORDER_THICKNESS, hitboxSize.y), 12);

        MakeSprite("BorderRight", whiteSprite, borderColor,
            new Vector3(hw - BORDER_THICKNESS / 2f, 0f, 0f),
            new Vector2(BORDER_THICKNESS, hitboxSize.y), 12);
    }

    private GameObject MakeSprite(string name, Sprite sprite, Color color,
        Vector3 localPos, Vector2 size, int sortOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(attackIndicator);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortOrder;

        return obj;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.B) && !isAttacking && cooldownTimer <= 0f)
            StartAttack();

        if (isAttacking)
            UpdateWindUp();
    }

    private void StartAttack()
    {
        if (player == null) return;

        isAttacking = true;
        windUpTimer = 0f;

        // Aim the indicator toward the player
        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        attackIndicator.position = (Vector2)transform.position + dir * hitboxOffset;
        attackIndicator.rotation = Quaternion.Euler(0, 0, angle);
        attackIndicator.gameObject.SetActive(true);

        // Reset fill to zero width at the left edge
        fillTransform.localScale = new Vector3(0f, hitboxSize.y, 1f);
        fillTransform.localPosition = new Vector3(-hitboxSize.x / 2f, 0f, 0f);
    }

    private void UpdateWindUp()
    {
        windUpTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(windUpTimer / windUpDuration);

        // Grow fill from left to right
        float fillWidth = hitboxSize.x * progress;
        fillTransform.localScale = new Vector3(fillWidth, hitboxSize.y, 1f);
        fillTransform.localPosition = new Vector3(
            (-hitboxSize.x + fillWidth) / 2f, 0f, 0f);

        if (progress >= 1f)
            ExecuteAttack();
    }

    private void ExecuteAttack()
    {
        Vector2 center = attackIndicator.position;
        float angle = attackIndicator.eulerAngles.z;

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitboxSize, angle);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(attackDamage);
            }
        }

        EndAttack();
    }

    private void EndAttack()
    {
        isAttacking = false;
        cooldownTimer = cooldown;
        attackIndicator.gameObject.SetActive(false);
    }
}

