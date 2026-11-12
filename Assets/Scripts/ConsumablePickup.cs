using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// World-space pickup for consumables dropped by enemies or found in the environment.
/// Automatically collects when the player walks into trigger range,
/// adding the consumable to the player's stash/equipped slots.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class ConsumablePickup : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The consumable data granted when collected.")]
    [SerializeField] private ConsumableData consumableData;

    [Tooltip("Trigger radius for collecting the item.")]
    [SerializeField] private float pickupRadius = 0.85f;

    [Tooltip("Speed of the idle floating bob animation.")]
    [SerializeField] private float bobSpeed = 3.5f;

    [Tooltip("Height amplitude of the idle floating bob.")]
    [SerializeField] private float bobAmplitude = 0.08f;

    private SpriteRenderer iconRenderer;
    private TextMeshPro labelText;
    private CircleCollider2D triggerCollider;
    private Vector3 basePosition;
    private bool isCollected;
    private float aliveTime;

    public ConsumableData Consumable => consumableData;

    public void Initialize(ConsumableData data)
    {
        consumableData = data;
        UpdateVisuals();
    }

    private void Awake()
    {
        basePosition = transform.position;

        triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.radius = pickupRadius;
        }

        SetupVisuals();
    }

    private void Start()
    {
        basePosition = transform.position;
        UpdateVisuals();
    }

    private void SetupVisuals()
    {
        // 1. Icon SpriteRenderer
        iconRenderer = GetComponent<SpriteRenderer>();
        if (iconRenderer == null)
        {
            iconRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        iconRenderer.sortingOrder = 15;
        transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        // 2. Floating World Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(transform, false);
        labelObj.transform.localPosition = new Vector2(0f, 0.45f);

        labelText = labelObj.AddComponent<TextMeshPro>();
        labelText.fontSize = 2.4f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.sortingOrder = 16;
        labelText.enableWordWrapping = false;
    }

    private void UpdateVisuals()
    {
        if (consumableData == null) return;

        if (iconRenderer != null)
        {
            if (consumableData.icon != null)
                iconRenderer.sprite = consumableData.icon;
            iconRenderer.color = consumableData.tintColor;
        }

        if (labelText != null)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(consumableData.tintColor);
            labelText.text = $"<color=#{colorHex}><b>{consumableData.consumableName}</b></color>";
        }
    }

    private void Update()
    {
        if (isCollected) return;

        aliveTime += Time.deltaTime;

        // Gentle floating bob
        float yOffset = Mathf.Sin(aliveTime * bobSpeed) * bobAmplitude;
        transform.position = basePosition + new Vector3(0f, yOffset, 0f);
    }

    /// <summary>
    /// Pops the pickup out in a direction (e.g. from a defeated enemy) with smooth physics dampening.
    /// </summary>
    public void Pop(Vector2 direction, float force)
    {
        StartCoroutine(PopRoutine(direction.normalized * force));
    }

    private IEnumerator PopRoutine(Vector2 velocity)
    {
        float timer = 0.35f;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            basePosition += (Vector3)(velocity * Time.deltaTime);
            velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 6f);
            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    private void Collect(GameObject player)
    {
        if (isCollected || consumableData == null) return;
        isCollected = true;

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.AddConsumable(consumableData);
            Debug.Log($"[ConsumablePickup] Collected {consumableData.consumableName}!");
        }

        StartCoroutine(CollectAnimation(player.transform));
    }

    private IEnumerator CollectAnimation(Transform playerTransform)
    {
        if (triggerCollider != null)
            triggerCollider.enabled = false;

        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 start = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 target = playerTransform != null ? playerTransform.position : start;
            transform.position = Vector3.Lerp(start, target, t * t);
            transform.localScale = Vector3.Lerp(new Vector3(0.5f, 0.5f, 1f), Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
