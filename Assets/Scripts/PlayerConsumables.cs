using System.Collections;
using UnityEngine;

/// <summary>
/// Handles player input for using equipped quick-slot consumables (Keys 3, 4, 5, 6, G, H).
/// Triggers healing on the player or spawns throwable grenades towards the mouse cursor.
/// </summary>
public class PlayerConsumables : MonoBehaviour
{
    [Header("Input Keys")]
    [SerializeField] private KeyCode slot1Key = KeyCode.Alpha3;
    [SerializeField] private KeyCode slot2Key = KeyCode.Alpha4;
    [SerializeField] private KeyCode slot3Key = KeyCode.Alpha5;
    [SerializeField] private KeyCode slot4Key = KeyCode.Alpha6;
    [SerializeField] private KeyCode quickGrenadeKey = KeyCode.G;
    [SerializeField] private KeyCode quickHealKey = KeyCode.H;

    [Header("Cooldown")]
    [Tooltip("Global cooldown between using consumables in seconds.")]
    [SerializeField] private float globalCooldown = 0.5f;

    private PlayerHealth playerHealth;
    private PlayerInventory inventory;
    private float lastUseTime = -10f;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        inventory = GetComponent<PlayerInventory>();
    }

    private void Start()
    {
        if (inventory == null)
            inventory = PlayerInventory.Instance;
    }

    private void Update()
    {
        // Don't allow using consumables if inventory is null or player is dead
        if (playerHealth != null && !playerHealth.IsAlive) return;

        // Check slots 1-4 (indices 0-3)
        if (Input.GetKeyDown(slot1Key) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            UseSlot(0);
        }
        else if (Input.GetKeyDown(slot2Key) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            UseSlot(1);
        }
        else if (Input.GetKeyDown(slot3Key) || Input.GetKeyDown(KeyCode.Keypad5))
        {
            UseSlot(2);
        }
        else if (Input.GetKeyDown(slot4Key) || Input.GetKeyDown(KeyCode.Keypad6))
        {
            UseSlot(3);
        }
        // Quick grenade hotkey (G)
        else if (Input.GetKeyDown(quickGrenadeKey))
        {
            UseFirstOfType(ConsumableType.Grenade);
        }
        // Quick heal hotkey (H)
        else if (Input.GetKeyDown(quickHealKey))
        {
            UseFirstOfType(ConsumableType.Healing);
        }
    }

    /// <summary>
    /// Uses the consumable in the given quick-slot (0-3).
    /// </summary>
    public bool UseSlot(int slotIndex)
    {
        if (Time.time < lastUseTime + globalCooldown) return false;

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>() ?? PlayerInventory.Instance;
        if (inventory == null) return false;

        var equipped = inventory.EquippedConsumables;
        if (slotIndex < 0 || slotIndex >= equipped.Count) return false;

        ConsumableData item = equipped[slotIndex] as ConsumableData;
        if (item == null) return false;

        // Execute behavior
        bool success = ExecuteConsumableEffect(item);
        if (success)
        {
            lastUseTime = Time.time;
            inventory.ConsumeEquippedSlot(slotIndex, out _);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds and uses the first equipped consumable matching the requested type.
    /// </summary>
    public bool UseFirstOfType(ConsumableType type)
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>() ?? PlayerInventory.Instance;
        if (inventory == null) return false;

        var equipped = inventory.EquippedConsumables;
        for (int i = 0; i < equipped.Count; i++)
        {
            ConsumableData item = equipped[i] as ConsumableData;
            if (item != null && item.type == type)
            {
                return UseSlot(i);
            }
        }

        return false;
    }

    private bool ExecuteConsumableEffect(ConsumableData item)
    {
        switch (item.type)
        {
            case ConsumableType.Healing:
                return ApplyHealing(item);

            case ConsumableType.Grenade:
                return ThrowGrenade(item);

            default:
                return false;
        }
    }

    private bool ApplyHealing(ConsumableData item)
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null) return false;

        // Heal player
        playerHealth.Heal(item.healAmount);

        // Visual flash / floating heal indicator (only during play mode)
        if (Application.isPlaying)
            StartCoroutine(HealVisualEffect(item.healEffectColor));
        return true;
    }

    private bool ThrowGrenade(ConsumableData item)
    {
        Vector2 mousePos = Camera.main != null
            ? (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition)
            : (Vector2)transform.position + Vector2.right;

        GameObject grenadeObj = new GameObject($"Thrown_{item.consumableName}");
        ThrownGrenade grenade = grenadeObj.AddComponent<ThrownGrenade>();
        grenade.Initialize(item, transform.position, mousePos);

        return true;
    }

    private IEnumerator HealVisualEffect(Color color)
    {
        GameObject fx = new GameObject("HealFlashFX");
        fx.transform.position = transform.position;
        fx.transform.SetParent(transform, true);

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        // Use a simple circle/cross sprite
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                float a = Mathf.Clamp01((16f - dist) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
        sr.color = color;
        sr.sortingOrder = 20;

        float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fx.transform.localScale = Vector3.Lerp(new Vector3(0.5f, 0.5f, 1f), new Vector3(1.8f, 1.8f, 1f), t);
            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, t);
            sr.color = c;
            yield return null;
        }

        Destroy(fx);
    }
}
