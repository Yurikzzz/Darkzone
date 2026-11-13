using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// Rarity tiers for loot drops with standardized color coding.
/// Blue = Common, Purple = Epic, Yellow = Legendary.
/// </summary>
public enum LootRarity
{
    Common,     // Blue
    Epic,       // Purple
    Legendary   // Yellow / Gold
}

/// <summary>
/// Placed on chests and crates in the Darkzone.
/// Requires the player to stand in range for a 3-second opening timer.
/// On opening, pops up the dropped weapon with a rarity-colored glow,
/// hovers for ~1 second, and flies smoothly into the player's inventory.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LootChest : MonoBehaviour
{
    [Header("Opening Configuration")]
    [Tooltip("Time in seconds the player must stay in range to open the chest.")]
    [SerializeField] private float openDuration = 3f;

    [Tooltip("Key pressed to initiate opening.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Default prompt text shown when chest is unopened.")]
    [SerializeField] private string promptText = "[E] Open Chest";

    [Tooltip("Text shown while chest is opening.")]
    [SerializeField] private string openingText = "Opening...";

    [Tooltip("Vertical offset of the opening timer ring.")]
    [SerializeField] private Vector2 timerOffset = new Vector2(0f, 1.2f);

    [Header("Rarity Settings")]
    [Tooltip("Fixed rarity tier (if randomizeRarity is false).")]
    [SerializeField] private LootRarity defaultRarity = LootRarity.Epic;

    [Tooltip("Whether to randomly select rarity from loot pool.")]
    [SerializeField] private bool randomizeRarity = true;

    [Header("Loot Pool")]
    [Tooltip("Weapons that can drop from this chest. Excludes pistol by default.")]
    [SerializeField] private List<WeaponData> availableLoot = new List<WeaponData>();

    [Header("Consumables Loot Pool")]
    [Tooltip("Whether this chest also drops a consumable alongside the weapon.")]
    [SerializeField] private bool dropConsumables = true;

    [Tooltip("Consumables that can drop from this chest alongside weapons.")]
    [SerializeField] private List<ConsumableData> availableConsumables = new List<ConsumableData>();

    [Range(0f, 1f)]
    [Tooltip("Chance that a consumable drops when the chest opens (1.0 = 100%).")]
    [SerializeField] private float consumableDropChance = 1.0f;

    [Header("Popup & Fly Animation")]
    [Tooltip("Height the loot icon pops up above the chest.")]
    [SerializeField] private float popUpHeight = 0.85f;

    [Tooltip("Time in seconds the icon stays hovering above the chest before flying to the player.")]
    [SerializeField] private float hoverDuration = 1.0f;

    [Tooltip("Flight travel duration from chest to player.")]
    [SerializeField] private float flyDuration = 0.45f;

    [Tooltip("Color tint applied to the chest when opened.")]
    [SerializeField] private Color openedChestColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private bool playerInRange;
    private bool isOpening;
    private bool isOpened;
    private Transform playerTransform;
    private InteractionPromptUI promptUI;
    private EvacTimerUI openTimer;
    private SpriteRenderer chestRenderer;
    private Color originalChestColor = Color.white;
    private GameObject activePopupObj;
    private GameObject activeConsumablePopupObj;
    private static Sprite radialGlowSprite;

    public bool IsOpened => isOpened;
    public bool IsOpening => isOpening;

    private void Awake()
    {
        // Guard against duplicate LootChest components on the same GameObject
        LootChest[] chests = GetComponents<LootChest>();
        if (chests.Length > 1 && chests[0] != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        chestRenderer = GetComponent<SpriteRenderer>();
        if (chestRenderer != null)
        {
            originalChestColor = chestRenderer.color;
        }

        // Configure trigger collider
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            if (col.size == Vector2.one)
            {
                col.size = new Vector2(2f, 2f);
            }
        }

        // Setup interaction prompt
        promptUI = GetComponent<InteractionPromptUI>();
        if (promptUI == null)
        {
            promptUI = gameObject.AddComponent<InteractionPromptUI>();
        }
        promptUI.PromptText = promptText;

        // Setup 3-second opening timer
        openTimer = GetComponent<EvacTimerUI>();
        if (openTimer == null)
        {
            openTimer = gameObject.AddComponent<EvacTimerUI>();
        }
        openTimer.Duration = openDuration;
        openTimer.Offset = timerOffset;
        openTimer.RingFillColor = new Color(1f, 0.75f, 0.2f, 0.95f);
        openTimer.OnTimerComplete += HandleOpenTimerComplete;

        // Auto-populate available loot pool if empty (excluding Pistol)
        if (availableLoot == null || availableLoot.Count == 0)
        {
            LoadDefaultLootPool();
        }

        // Auto-populate available consumables pool if empty
        if (availableConsumables == null || availableConsumables.Count == 0)
        {
            LoadDefaultConsumablesPool();
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (DarkzoneManager.Instance != null)
        {
            DarkzoneManager.Instance.OnPlayerLeftZone += ResetChest;
        }
    }

    private void OnDestroy()
    {
        if (openTimer != null)
        {
            openTimer.OnTimerComplete -= HandleOpenTimerComplete;
        }

        if (DarkzoneManager.Instance != null)
        {
            DarkzoneManager.Instance.OnPlayerLeftZone -= ResetChest;
        }

        if (activePopupObj != null)
        {
            Destroy(activePopupObj);
        }

        if (activeConsumablePopupObj != null)
        {
            Destroy(activeConsumablePopupObj);
        }
    }

    private void LoadDefaultLootPool()
    {
        availableLoot = new List<WeaponData>();
        WeaponData[] allWeapons = Resources.FindObjectsOfTypeAll<WeaponData>();
        foreach (var w in allWeapons)
        {
            if (w != null && !w.isRuntimeInstance && !w.name.ToLower().Contains("pistol") && !availableLoot.Contains(w))
            {
                availableLoot.Add(w);
            }
        }
    }

    private void LoadDefaultConsumablesPool()
    {
        availableConsumables = new List<ConsumableData>();
        ConsumableData[] allConsumables = Resources.FindObjectsOfTypeAll<ConsumableData>();
        foreach (var c in allConsumables)
        {
            if (c != null && !c.isRuntimeInstance && !availableConsumables.Contains(c))
            {
                availableConsumables.Add(c);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerTransform = other.transform;

            if (!isOpened && !isOpening)
            {
                promptUI.PromptText = promptText;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            // Cancel opening if player walks out of range
            if (isOpening)
            {
                CancelOpening();
            }
        }
    }

    private void Update()
    {
        if (!playerInRange || isOpened) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (!isOpening)
            {
                StartOpening();
            }
        }
    }

    private void StartOpening()
    {
        if (isOpening || isOpened) return;
        isOpening = true;

        if (promptUI == null)
            promptUI = GetComponent<InteractionPromptUI>();

        if (promptUI != null)
        {
            promptUI.SetSuppressed(true);
        }

        if (openTimer != null)
        {
            openTimer.StartTimer();
        }
    }

    private void CancelOpening()
    {
        isOpening = false;
        if (openTimer == null)
            openTimer = GetComponent<EvacTimerUI>();

        if (openTimer != null && openTimer.IsRunning)
        {
            openTimer.CancelTimer();
        }

        if (!isOpened)
        {
            if (promptUI == null)
                promptUI = GetComponent<InteractionPromptUI>();

            if (promptUI != null)
            {
                promptUI.PromptText = promptText;
                promptUI.SetSuppressed(false);
            }
        }
    }

    private void HandleOpenTimerComplete()
    {
        if (!playerInRange || isOpened) return;

        OpenChest();
    }

    /// <summary>
    /// Opens the chest, selects a random weapon, and triggers the animated loot drop sequence.
    /// Also rolls for a consumable drop alongside the weapon.
    /// </summary>
    public void OpenChest()
    {
        if (isOpened) return;
        isOpening = false;
        isOpened = true;

        if (promptUI != null)
        {
            promptUI.SetSuppressed(true);
            promptUI.PromptText = "";
        }

        if (openTimer != null && openTimer.IsRunning)
        {
            openTimer.CancelTimer();
        }

        if (chestRenderer != null)
        {
            chestRenderer.color = openedChestColor;
        }

        WeaponData loot = SelectRandomLoot();
        LootRarity rarity = DetermineRarity();
        ConsumableData consumableLoot = SelectRandomConsumable();

        StartCoroutine(AnimateLootDrop(loot, rarity, consumableLoot));
    }

    private ConsumableData SelectRandomConsumable()
    {
        if (!dropConsumables || Random.value > consumableDropChance)
            return null;

        if (availableConsumables == null || availableConsumables.Count == 0)
        {
            LoadDefaultConsumablesPool();
        }

        if (availableConsumables != null && availableConsumables.Count > 0)
        {
            ConsumableData template = availableConsumables[Random.Range(0, availableConsumables.Count)];
            return template != null ? template.CreateRuntimeInstance() : null;
        }

        return null;
    }

    private WeaponData SelectRandomLoot()
    {
        // Filter out pistol just in case
        List<WeaponData> validLoot = availableLoot.FindAll(w => w != null && !w.name.ToLower().Contains("pistol"));

        if (validLoot.Count == 0)
        {
            LoadDefaultLootPool();
            validLoot = availableLoot.FindAll(w => w != null && !w.name.ToLower().Contains("pistol"));
        }

        if (validLoot.Count > 0)
        {
            int index = Random.Range(0, validLoot.Count);
            WeaponData template = validLoot[index];
            return template != null ? template.CreateRuntimeInstance() : null;
        }

        return null;
    }

    private LootRarity DetermineRarity()
    {
        if (!randomizeRarity)
            return defaultRarity;

        // Weighted roll: 50% Common (Blue), 35% Epic (Purple), 15% Legendary (Yellow)
        float roll = Random.value;
        if (roll < 0.50f)
            return LootRarity.Common;
        if (roll < 0.85f)
            return LootRarity.Epic;
        return LootRarity.Legendary;
    }

    /// <summary>
    /// Color associated with each rarity tier.
    /// Blue -> Common, Purple -> Epic, Yellow -> Legendary.
    /// </summary>
    public static Color GetRarityColor(LootRarity rarity)
    {
        switch (rarity)
        {
            case LootRarity.Common:
                return new Color(0.22f, 0.60f, 1f, 1f);      // Vibrant Blue
            case LootRarity.Epic:
                return new Color(0.75f, 0.30f, 1f, 1f);      // Royal Purple
            case LootRarity.Legendary:
                return new Color(1f, 0.82f, 0.15f, 1f);      // Radiant Gold / Yellow
            default:
                return Color.white;
        }
    }

    private IEnumerator AnimateLootDrop(WeaponData weapon, LootRarity rarity, ConsumableData consumable)
    {
        Color rarityColor = GetRarityColor(rarity);

        if (activePopupObj != null) Destroy(activePopupObj);
        if (activeConsumablePopupObj != null) Destroy(activeConsumablePopupObj);

        Vector3 startPos = transform.position;
        Vector3 weaponPeakPos = startPos + Vector3.up * popUpHeight;
        Vector3 consumablePeakPos = startPos + Vector3.up * (popUpHeight * 0.9f);

        if (weapon != null && consumable != null)
        {
            weaponPeakPos += Vector3.left * 0.45f;
            consumablePeakPos += Vector3.right * 0.55f;
        }

        SpriteRenderer weaponGlowRenderer = null;

        // 1. Setup Weapon Popup (if present)
        if (weapon != null)
        {
            activePopupObj = new GameObject($"LootPopup_{weapon.weaponName}");
            activePopupObj.transform.position = startPos;

            // Simulated Glow Aura
            GameObject glowObj = new GameObject("RarityGlow");
            glowObj.transform.SetParent(activePopupObj.transform, false);
            glowObj.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            weaponGlowRenderer = glowObj.AddComponent<SpriteRenderer>();
            if (radialGlowSprite == null) radialGlowSprite = CreateRadialGlowSprite();
            weaponGlowRenderer.sprite = radialGlowSprite;
            weaponGlowRenderer.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.85f);
            weaponGlowRenderer.sortingOrder = 30;

            // Weapon Icon
            GameObject iconObj = new GameObject("WeaponIcon");
            iconObj.transform.SetParent(activePopupObj.transform, false);
            iconObj.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            SpriteRenderer iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            iconRenderer.sprite = weapon.weaponSprite;
            iconRenderer.sortingOrder = 31;

            // Floating World-Space Rarity Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(activePopupObj.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            TextMeshPro labelTmp = labelObj.AddComponent<TextMeshPro>();
            labelTmp.text = $"<b>{weapon.weaponName}</b>\n<size=75%><color=#{ColorUtility.ToHtmlStringRGB(rarityColor)}>[{rarity.ToString().ToUpper()}]</color></size>";
            labelTmp.fontSize = 3.2f;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = Color.white;
            labelTmp.sortingOrder = 32;
        }

        // 2. Setup Consumable Popup (if present)
        SpriteRenderer consumableGlowRenderer = null;
        if (consumable != null)
        {
            activeConsumablePopupObj = new GameObject($"LootPopup_{consumable.consumableName}");
            activeConsumablePopupObj.transform.position = startPos;

            // Consumable Glow Aura
            GameObject cGlowObj = new GameObject("ConsumableGlow");
            cGlowObj.transform.SetParent(activeConsumablePopupObj.transform, false);
            cGlowObj.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            consumableGlowRenderer = cGlowObj.AddComponent<SpriteRenderer>();
            if (radialGlowSprite == null) radialGlowSprite = CreateRadialGlowSprite();
            consumableGlowRenderer.sprite = radialGlowSprite;
            consumableGlowRenderer.color = new Color(consumable.tintColor.r, consumable.tintColor.g, consumable.tintColor.b, 0.75f);
            consumableGlowRenderer.sortingOrder = 30;

            // Consumable Icon (White Square placeholder or custom icon)
            GameObject cIconObj = new GameObject("ConsumableIcon");
            cIconObj.transform.SetParent(activeConsumablePopupObj.transform, false);
            cIconObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            SpriteRenderer cIconRenderer = cIconObj.AddComponent<SpriteRenderer>();
            cIconRenderer.sprite = consumable.icon;
            cIconRenderer.color = consumable.tintColor;
            cIconRenderer.sortingOrder = 31;

            // Floating World-Space Label
            GameObject cLabelObj = new GameObject("Label");
            cLabelObj.transform.SetParent(activeConsumablePopupObj.transform, false);
            cLabelObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            TextMeshPro cLabelTmp = cLabelObj.AddComponent<TextMeshPro>();
            string tintHex = ColorUtility.ToHtmlStringRGB(consumable.tintColor);
            cLabelTmp.text = $"<b>{consumable.consumableName}</b>\n<size=75%><color=#{tintHex}>[CONSUMABLE]</color></size>";
            cLabelTmp.fontSize = 2.8f;
            cLabelTmp.alignment = TextAlignmentOptions.Center;
            cLabelTmp.color = Color.white;
            cLabelTmp.sortingOrder = 32;
        }

        // --- Phase 1: Pop Up (0 to 0.35s) ---
        float popDuration = 0.35f;
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);

            if (activePopupObj != null)
                activePopupObj.transform.position = Vector3.Lerp(startPos, weaponPeakPos, smoothT);
            if (activeConsumablePopupObj != null)
                activeConsumablePopupObj.transform.position = Vector3.Lerp(startPos, consumablePeakPos, smoothT);

            yield return null;
        }

        // --- Phase 2: Hover with Pulsing Glow (~1.0s) ---
        elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.deltaTime;

            // Floating bob
            float bob = Mathf.Sin(elapsed * 5f) * 0.06f;
            float cBob = Mathf.Sin(elapsed * 5f + 1.2f) * 0.05f;

            if (activePopupObj != null)
            {
                activePopupObj.transform.position = weaponPeakPos + Vector3.up * bob;
                if (weaponGlowRenderer != null)
                {
                    float pulse = 0.65f + 0.35f * Mathf.Sin(elapsed * 7f);
                    weaponGlowRenderer.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, pulse);
                }
            }

            if (activeConsumablePopupObj != null)
            {
                activeConsumablePopupObj.transform.position = consumablePeakPos + Vector3.up * cBob;
                if (consumableGlowRenderer != null)
                {
                    float pulse = 0.60f + 0.30f * Mathf.Sin(elapsed * 7f + 1.5f);
                    consumableGlowRenderer.color = new Color(consumable.tintColor.r, consumable.tintColor.g, consumable.tintColor.b, pulse);
                }
            }

            yield return null;
        }

        // --- Phase 3: Fly into Player (~0.45s) ---
        Vector3 flyStartWeapon = activePopupObj != null ? activePopupObj.transform.position : startPos;
        Vector3 flyStartConsumable = activeConsumablePopupObj != null ? activeConsumablePopupObj.transform.position : startPos;
        elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float curveT = t * t;

            Vector3 targetPlayerPos = playerTransform != null ? playerTransform.position : transform.position;

            if (activePopupObj != null)
            {
                activePopupObj.transform.position = Vector3.Lerp(flyStartWeapon, targetPlayerPos, curveT);
                activePopupObj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, curveT);
            }

            if (activeConsumablePopupObj != null)
            {
                activeConsumablePopupObj.transform.position = Vector3.Lerp(flyStartConsumable, targetPlayerPos, curveT);
                activeConsumablePopupObj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, curveT);
            }

            yield return null;
        }

        // --- Phase 4: Delivery into Player Inventory ---
        if (PlayerInventory.Instance != null)
        {
            if (weapon != null)
            {
                PlayerInventory.Instance.AddOwnedWeapon(weapon);
                Debug.Log($"[LootChest] Added {weapon.weaponName} ({rarity}) to player inventory!");
            }

            if (consumable != null)
            {
                PlayerInventory.Instance.AddConsumable(consumable);
                Debug.Log($"[LootChest] Added {consumable.consumableName} to player inventory!");
            }
        }

        if (activePopupObj != null)
        {
            Destroy(activePopupObj);
            activePopupObj = null;
        }

        if (activeConsumablePopupObj != null)
        {
            Destroy(activeConsumablePopupObj);
            activeConsumablePopupObj = null;
        }
    }

    /// <summary>
    /// Resets the chest to its unopened state (called when player leaves or resets the zone).
    /// </summary>
    public void ResetChest()
    {
        CancelOpening();

        if (activePopupObj != null)
        {
            if (Application.isPlaying)
                Destroy(activePopupObj);
            else
                DestroyImmediate(activePopupObj);
            activePopupObj = null;
        }

        if (activeConsumablePopupObj != null)
        {
            if (Application.isPlaying)
                Destroy(activeConsumablePopupObj);
            else
                DestroyImmediate(activeConsumablePopupObj);
            activeConsumablePopupObj = null;
        }

        isOpened = false;
        isOpening = false;

        if (chestRenderer == null)
            chestRenderer = GetComponent<SpriteRenderer>();

        if (chestRenderer != null)
        {
            chestRenderer.color = originalChestColor;
        }

        if (promptUI == null)
            promptUI = GetComponent<InteractionPromptUI>();

        if (promptUI != null)
        {
            promptUI.PromptText = promptText;
            promptUI.SetSuppressed(false);
        }
    }

    /// <summary>
    /// Creates a 64x64 soft circular radial gradient texture for simulated rarity glow.
    /// </summary>
    private static Sprite CreateRadialGlowSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float norm = Mathf.Clamp01(dist / radius);
                // Quadratic falloff for smooth luminous gradient
                float alpha = Mathf.Pow(1f - norm, 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }
}
