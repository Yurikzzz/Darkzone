using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Loadout & Inventory station modal window.
/// The whole UI is built in code with layout groups (designed for the 640x320 reference canvas),
/// so sizing is always driven by the layout system instead of hand-placed offsets.
///
/// Layout:
///   Header (title + close)
///   Left column : two equipped weapon slots, swap button, 4 reserved consumable slots
///   Right column: stash tabs (Weapons / Equipment / Consumables) + scrollable content
///   Footer      : control hints
/// </summary>
public class LoadoutUI : MonoBehaviour
{
    public static LoadoutUI Instance { get; private set; }

    public enum TabCategory
    {
        Weapons,
        Equipment,
        Consumables
    }

    // ---------- Layout constants (reference-canvas pixels) ----------
    private const float PanelWidthFraction = 0.94f;   // of canvas width
    private const float PanelHeightFraction = 0.92f;  // of canvas height
    private const float HeaderHeight = 22f;
    private const float FooterHeight = 14f;
    private const float BodyPadding = 8f;
    private const float ColumnSpacing = 8f;
    private const float LeftColumnWidth = 230f;
    private const float SlotCardHeight = 56f;
    private const float ConsumableSlotHeight = 40f;
    private const float TabHeight = 20f;
    private const float WeaponCardHeight = 38f;

    // ---------- Palette ----------
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color BorderColor = new Color(0.20f, 0.24f, 0.32f, 1f);
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.12f, 1f);
    private static readonly Color HeaderColor = new Color(0.11f, 0.14f, 0.19f, 1f);
    private static readonly Color CardColor = new Color(0.10f, 0.13f, 0.18f, 1f);
    private static readonly Color InsetColor = new Color(0.05f, 0.07f, 0.10f, 1f);
    private static readonly Color Amber = new Color(0.95f, 0.65f, 0.15f, 1f);
    private static readonly Color Blue = new Color(0.25f, 0.60f, 0.90f, 1f);
    private static readonly Color Red = new Color(0.65f, 0.22f, 0.22f, 1f);
    private static readonly Color Grey = new Color(0.30f, 0.34f, 0.42f, 1f);
    private static readonly Color DimText = new Color(0.55f, 0.60f, 0.68f, 1f);
    private static readonly Color InactiveTab = new Color(0.18f, 0.21f, 0.28f, 1f);
    private static readonly Color DarkText = new Color(0.08f, 0.08f, 0.08f, 1f);

    private class SlotView
    {
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statsText;
        public Button unequipButton;
    }

    private GameObject menuRoot;
    private SlotView primaryView;
    private SlotView secondaryView;
    private Button swapButton;
    private Transform consumablesRow;

    private readonly Image[] tabImages = new Image[3];
    private readonly TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[3];
    private GameObject weaponsView;
    private GameObject equipmentView;
    private GameObject consumablesView;
    private Transform weaponsListContainer;

    private TabCategory currentTab = TabCategory.Weapons;
    private PlayerMovement playerMovement;
    private readonly List<GameObject> spawnedWeaponRows = new List<GameObject>();
    private readonly List<GameObject> spawnedConsumableSlots = new List<GameObject>();
    private int openedFrame = -1;

    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Discard the previously authored (broken) scene hierarchy and build a fresh one.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject old = transform.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        BuildUI();
        menuRoot.SetActive(false);
    }

    private void Start()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnInventoryChanged += RefreshUI;
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnInventoryChanged -= RefreshUI;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == openedFrame) return;

        // Escape closes the menu. The interact key (E) is handled by LoadoutStation.Toggle(),
        // so it is deliberately not handled here to avoid close-then-reopen in the same frame.
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // =====================================================================
    // Public API
    // =====================================================================

    /// <summary>Opens the loadout station UI and disables player movement.</summary>
    public void Open()
    {
        openedFrame = Time.frameCount;

        if (menuRoot != null)
            menuRoot.SetActive(true);

        if (playerMovement == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerMovement = player.GetComponent<PlayerMovement>();
        }

        if (playerMovement != null)
            playerMovement.CanMove = false;

        SwitchTab(TabCategory.Weapons);
    }

    /// <summary>Closes the loadout station UI and restores player movement.</summary>
    public void Close()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        if (playerMovement != null)
            playerMovement.CanMove = true;
    }

    /// <summary>Toggles the loadout station UI.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            if (Time.frameCount != openedFrame)
                Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Switches the right-side stash tab category.</summary>
    public void SwitchTab(TabCategory tab)
    {
        currentTab = tab;

        if (weaponsView != null) weaponsView.SetActive(tab == TabCategory.Weapons);
        if (equipmentView != null) equipmentView.SetActive(tab == TabCategory.Equipment);
        if (consumablesView != null) consumablesView.SetActive(tab == TabCategory.Consumables);

        for (int i = 0; i < tabImages.Length; i++)
        {
            bool active = i == (int)tab;
            if (tabImages[i] != null) tabImages[i].color = active ? Amber : InactiveTab;
            if (tabTexts[i] != null) tabTexts[i].color = active ? DarkText : DimText;
        }

        RefreshUI();
    }

    /// <summary>Refreshes equipped slots, consumable slots, tab counts and the stash list.</summary>
    public void RefreshUI()
    {
        if (!IsOpen) return;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        // Primary can only be unequipped when a secondary exists to take its place.
        RefreshSlot(primaryView, inv.PrimaryWeapon, inv.SecondaryWeapon != null);
        RefreshSlot(secondaryView, inv.SecondaryWeapon, inv.SecondaryWeapon != null);

        // Swapping only makes sense with two weapons equipped.
        if (swapButton != null)
            swapButton.interactable = inv.PrimaryWeapon != null && inv.SecondaryWeapon != null;

        RefreshConsumableSlots(inv);
        RefreshTabLabels(inv);

        if (currentTab == TabCategory.Weapons)
            RefreshWeaponsStash(inv);
    }

    // =====================================================================
    // Refresh helpers
    // =====================================================================

    private void RefreshSlot(SlotView view, WeaponData weapon, bool canUnequip)
    {
        if (view == null) return;

        if (weapon != null)
        {
            view.icon.sprite = weapon.weaponSprite;
            view.icon.gameObject.SetActive(weapon.weaponSprite != null);
            view.nameText.text = weapon.weaponName;
            view.nameText.color = Color.white;
            view.statsText.text = FormatStats(weapon);
            view.unequipButton.gameObject.SetActive(canUnequip);
        }
        else
        {
            view.icon.gameObject.SetActive(false);
            view.nameText.text = "EMPTY";
            view.nameText.color = DimText;
            view.statsText.text = "No weapon equipped";
            view.unequipButton.gameObject.SetActive(false);
        }
    }

    private void RefreshConsumableSlots(PlayerInventory inv)
    {
        if (consumablesRow == null) return;

        foreach (GameObject go in spawnedConsumableSlots)
        {
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        spawnedConsumableSlots.Clear();

        IReadOnlyList<ScriptableObject> equipped = inv.EquippedConsumables;
        for (int i = 0; i < inv.ConsumableSlotCount; i++)
        {
            ScriptableObject item = (equipped != null && i < equipped.Count) ? equipped[i] : null;
            spawnedConsumableSlots.Add(CreateConsumableSlot(consumablesRow, i + 1, item));
        }
    }

    private void RefreshTabLabels(PlayerInventory inv)
    {
        SetTabLabel(TabCategory.Weapons, $"WEAPONS ({CountOf(inv.OwnedWeapons)})");
        SetTabLabel(TabCategory.Equipment, $"EQUIPMENT ({CountOf(inv.OwnedEquipment)})");
        SetTabLabel(TabCategory.Consumables, $"CONSUMABLES ({CountOf(inv.OwnedConsumables)})");
    }

    private static int CountOf<T>(IReadOnlyList<T> list) where T : class
    {
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) n++;
        return n;
    }

    private void SetTabLabel(TabCategory tab, string label)
    {
        TextMeshProUGUI t = tabTexts[(int)tab];
        if (t != null) t.text = label;
    }

    private void RefreshWeaponsStash(PlayerInventory inv)
    {
        if (weaponsListContainer == null) return;

        foreach (GameObject row in spawnedWeaponRows)
        {
            if (row == null) continue;
            row.SetActive(false);
            Destroy(row);
        }
        spawnedWeaponRows.Clear();

        IReadOnlyList<WeaponData> owned = inv.OwnedWeapons;
        if (owned == null) return;

        for (int i = 0; i < owned.Count; i++)
        {
            WeaponData weapon = owned[i];
            if (weapon == null) continue;
            spawnedWeaponRows.Add(CreateWeaponCard(weaponsListContainer, weapon, inv));
        }
    }

    private static string FormatStats(WeaponData w)
    {
        return $"<color=#FFB84D>DMG {w.damage:0}</color>  <color=#66CCFF>MAG {w.magazineSize}</color>  <color=#88FFAA>{(w.isAutomatic ? "AUTO" : "SEMI")}</color>";
    }

    // =====================================================================
    // UI construction
    // =====================================================================

    private void BuildUI()
    {
        // Root: full-screen dim overlay that also blocks clicks to the game behind.
        RectTransform root = NewRect("ModalRoot", transform);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = OverlayColor;
        menuRoot = root.gameObject;

        // Border + panel: anchored with proportional margins so it always fits the canvas,
        // capped by the layout minimums below (left column + tabs, header, slots, footer).
        RectTransform border = NewRect("PanelBorder", root);
        border.anchorMin = new Vector2(0.5f - PanelWidthFraction * 0.5f, 0.5f - PanelHeightFraction * 0.5f);
        border.anchorMax = new Vector2(0.5f + PanelWidthFraction * 0.5f, 0.5f + PanelHeightFraction * 0.5f);
        border.pivot = new Vector2(0.5f, 0.5f);
        border.offsetMin = Vector2.zero;
        border.offsetMax = Vector2.zero;
        border.gameObject.AddComponent<Image>().color = BorderColor;

        RectTransform panel = NewRect("Panel", border);
        Stretch(panel, 1f, 1f, 1f, 1f);
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        AddLayout(panel.gameObject, true, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        BuildHeader(panel);
        BuildBody(panel);
        BuildFooter(panel);
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform header = NewRect("Header", parent);
        header.gameObject.AddComponent<Image>().color = HeaderColor;
        SetLE(header.gameObject, -1f, HeaderHeight, 1f, 0f);
        AddLayout(header.gameObject, false, 6f, new RectOffset(8, 4, 3, 3), TextAnchor.MiddleLeft);

        TextMeshProUGUI title = NewText("Title", header, "LOADOUT & INVENTORY", 10f, Amber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(title.gameObject, 0f, -1f, 1f, 1f);

        Button close = NewButton("CloseButton", header, "X", Red, 8f, Close);
        SetLE(close.gameObject, 16f, 16f, 0f, 0f);
    }

    private void BuildBody(Transform parent)
    {
        RectTransform body = NewRect("Body", parent);
        SetLE(body.gameObject, -1f, 0f, 1f, 1f);
        int pad = Mathf.RoundToInt(BodyPadding);
        AddLayout(body.gameObject, false, ColumnSpacing, new RectOffset(pad, pad, pad, pad), TextAnchor.UpperLeft);

        BuildLeftColumn(body);
        BuildRightColumn(body);
    }

    private void BuildFooter(Transform parent)
    {
        RectTransform footer = NewRect("Footer", parent);
        footer.gameObject.AddComponent<Image>().color = HeaderColor;
        SetLE(footer.gameObject, -1f, FooterHeight, 1f, 0f);
        AddLayout(footer.gameObject, false, 0f, new RectOffset(8, 8, 1, 1), TextAnchor.MiddleCenter);

        TextMeshProUGUI hint = NewText("Hint", footer, "[E] / [ESC]  CLOSE", 7f, DimText, TextAlignmentOptions.Midline, FontStyles.Normal);
        SetLE(hint.gameObject, 0f, -1f, 1f, 1f);
    }

    // ---------------- Left column ----------------

    private void BuildLeftColumn(Transform parent)
    {
        RectTransform col = NewRect("LeftColumn_Equipped", parent);
        SetLE(col.gameObject, LeftColumnWidth, 0f, 0f, 1f);
        AddLayout(col.gameObject, true, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionLabel(col, "EQUIPPED WEAPONS");

        primaryView = BuildSlotCard(col, "Slot1_Primary", "SLOT 1  -  PRIMARY", Amber, () =>
        {
            if (PlayerInventory.Instance != null) PlayerInventory.Instance.UnequipWeapon(0);
        });

        swapButton = NewButton("SwapSlotsButton", col, "SWAP SLOTS", Grey, 8f, () =>
        {
            if (PlayerInventory.Instance != null) PlayerInventory.Instance.SwapEquippedSlots();
        });
        SetLE(swapButton.gameObject, 0f, 16f, 1f, 0f);

        secondaryView = BuildSlotCard(col, "Slot2_Secondary", "SLOT 2  -  SECONDARY", Blue, () =>
        {
            if (PlayerInventory.Instance != null) PlayerInventory.Instance.UnequipWeapon(1);
        });

        SectionLabel(col, "CONSUMABLES");

        RectTransform row = NewRect("ConsumablesRow", col);
        SetLE(row.gameObject, 0f, ConsumableSlotHeight, 1f, 0f);
        AddLayout(row.gameObject, false, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
        consumablesRow = row;
    }

    private void SectionLabel(Transform parent, string text)
    {
        TextMeshProUGUI label = NewText("Label_" + text, parent, text, 7.5f, DimText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(label.gameObject, 0f, 10f, 1f, 0f);
    }

    private SlotView BuildSlotCard(Transform parent, string name, string header, Color accent, Action onUnequip)
    {
        RectTransform card = NewRect(name, parent);
        card.gameObject.AddComponent<Image>().color = accent;
        SetLE(card.gameObject, 0f, SlotCardHeight, 1f, 0f);

        // 1px accent frame + inner fill
        RectTransform inner = NewRect("InnerFill", card);
        Stretch(inner, 1f, 1f, 1f, 1f);
        inner.gameObject.AddComponent<Image>().color = CardColor;
        AddLayout(inner.gameObject, false, 6f, new RectOffset(5, 5, 5, 5), TextAnchor.MiddleLeft);

        // Icon box
        RectTransform thumb = NewRect("ThumbBox", inner);
        thumb.gameObject.AddComponent<Image>().color = InsetColor;
        SetLE(thumb.gameObject, 50f, 44f, 0f, 0f);

        RectTransform iconRt = NewRect("Icon", thumb);
        Stretch(iconRt, 3f, 3f, 3f, 3f);
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Info column
        RectTransform info = NewRect("Info", inner);
        SetLE(info.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(info.gameObject, true, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI headerText = NewText("Header", info, header, 6.5f, accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(headerText.gameObject, 0f, 9f, 1f, 0f);

        TextMeshProUGUI nameText = NewText("Name", info, "", 10f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(nameText.gameObject, 0f, 12f, 1f, 0f);

        TextMeshProUGUI statsText = NewText("Stats", info, "", 7f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        SetLE(statsText.gameObject, 0f, 10f, 1f, 0f);

        // Unequip button
        Button unequip = NewButton("UnequipButton", inner, "X", Red, 8f, onUnequip);
        SetLE(unequip.gameObject, 18f, 18f, 0f, 0f);

        return new SlotView { icon = icon, nameText = nameText, statsText = statsText, unequipButton = unequip };
    }

    private GameObject CreateConsumableSlot(Transform parent, int number, ScriptableObject item)
    {
        RectTransform slot = NewRect($"ConsumableSlot_{number}", parent);
        slot.gameObject.AddComponent<Image>().color = BorderColor;
        SetLE(slot.gameObject, 0f, ConsumableSlotHeight, 1f, 0f);

        RectTransform inner = NewRect("InnerFill", slot);
        Stretch(inner, 1f, 1f, 1f, 1f);
        inner.gameObject.AddComponent<Image>().color = InsetColor;
        AddLayout(inner.gameObject, true, 1f, new RectOffset(2, 2, 3, 3), TextAnchor.MiddleCenter);

        TextMeshProUGUI num = NewText("Number", inner, number.ToString(), 10f, Grey, TextAlignmentOptions.Midline, FontStyles.Bold);
        SetLE(num.gameObject, 0f, 12f, 1f, 0f);

        TextMeshProUGUI label = NewText("Label", inner, item != null ? item.name : "EMPTY", 6f, DimText, TextAlignmentOptions.Midline, FontStyles.Normal);
        SetLE(label.gameObject, 0f, 8f, 1f, 0f);

        return slot.gameObject;
    }

    // ---------------- Right column ----------------

    private void BuildRightColumn(Transform parent)
    {
        RectTransform col = NewRect("RightColumn_Stash", parent);
        SetLE(col.gameObject, 0f, 0f, 1f, 1f);
        AddLayout(col.gameObject, true, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        // Tab bar
        RectTransform tabBar = NewRect("TabBar", col);
        SetLE(tabBar.gameObject, 0f, TabHeight, 1f, 0f);
        AddLayout(tabBar.gameObject, false, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        BuildTab(tabBar, TabCategory.Weapons, "WEAPONS");
        BuildTab(tabBar, TabCategory.Equipment, "EQUIPMENT");
        BuildTab(tabBar, TabCategory.Consumables, "CONSUMABLES");

        // Content area: the three views share the same flexible space; only one is active.
        RectTransform content = NewRect("ContentArea", col);
        SetLE(content.gameObject, 0f, 0f, 1f, 1f);
        AddLayout(content.gameObject, true, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        weaponsView = BuildWeaponsView(content);
        equipmentView = BuildPlaceholderView(content, "EquipmentView", "EQUIPMENT\nComing soon");
        consumablesView = BuildPlaceholderView(content, "ConsumablesView", "CONSUMABLES\nComing soon");
    }

    private void BuildTab(Transform parent, TabCategory tab, string label)
    {
        TabCategory captured = tab;
        Button btn = NewButton($"Tab_{label}", parent, label, InactiveTab, 7.5f, () => SwitchTab(captured));
        SetLE(btn.gameObject, 0f, TabHeight, 1f, 0f);
        tabImages[(int)tab] = btn.GetComponent<Image>();
        tabTexts[(int)tab] = btn.GetComponentInChildren<TextMeshProUGUI>();
    }

    private GameObject BuildWeaponsView(Transform parent)
    {
        RectTransform view = NewRect("WeaponsView", parent);
        view.gameObject.AddComponent<Image>().color = InsetColor;
        SetLE(view.gameObject, 0f, 0f, 1f, 1f);

        RectTransform viewport = NewRect("Viewport", view);
        Stretch(viewport, 3f, 3f, 3f, 3f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform list = NewRect("Content", viewport);
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = Vector2.zero;
        list.sizeDelta = Vector2.zero;

        AddLayout(list.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
        ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = list;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 14f;

        weaponsListContainer = list;
        return view.gameObject;
    }

    private GameObject BuildPlaceholderView(Transform parent, string name, string message)
    {
        RectTransform view = NewRect(name, parent);
        view.gameObject.AddComponent<Image>().color = InsetColor;
        SetLE(view.gameObject, 0f, 0f, 1f, 1f);
        AddLayout(view.gameObject, true, 0f, new RectOffset(8, 8, 8, 8), TextAnchor.MiddleCenter);

        TextMeshProUGUI text = NewText("Message", view, message, 9f, DimText, TextAlignmentOptions.Center, FontStyles.Bold);
        SetLE(text.gameObject, 0f, 0f, 1f, 1f);

        return view.gameObject;
    }

    // ---------------- Stash weapon card ----------------

    private GameObject CreateWeaponCard(Transform parent, WeaponData weapon, PlayerInventory inv)
    {
        bool isEquipped = inv.IsWeaponEquipped(weapon, out int equippedSlot);
        Color slotColor = equippedSlot == 0 ? Amber : Blue;

        RectTransform card = NewRect($"WeaponCard_{weapon.weaponName}", parent);
        card.gameObject.AddComponent<Image>().color = isEquipped ? slotColor : BorderColor;
        SetLE(card.gameObject, 0f, WeaponCardHeight, 1f, 0f);

        RectTransform inner = NewRect("InnerFill", card);
        Stretch(inner, 1f, 1f, 1f, 1f);
        inner.gameObject.AddComponent<Image>().color = CardColor;
        AddLayout(inner.gameObject, false, 6f, new RectOffset(4, 4, 3, 3), TextAnchor.MiddleLeft);

        // Thumbnail
        RectTransform thumb = NewRect("ThumbBox", inner);
        thumb.gameObject.AddComponent<Image>().color = InsetColor;
        SetLE(thumb.gameObject, 40f, 30f, 0f, 0f);

        RectTransform iconRt = NewRect("Icon", thumb);
        Stretch(iconRt, 3f, 3f, 3f, 3f);
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = weapon.weaponSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = weapon.weaponSprite != null;

        // Name + stats
        RectTransform info = NewRect("Info", inner);
        SetLE(info.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(info.gameObject, true, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI title = NewText("Title", info, weapon.weaponName, 9.5f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(title.gameObject, 0f, 12f, 1f, 0f);

        TextMeshProUGUI stats = NewText("Stats", info, FormatStats(weapon), 7f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        SetLE(stats.gameObject, 0f, 10f, 1f, 0f);

        // Actions
        RectTransform actions = NewRect("Actions", inner);
        SetLE(actions.gameObject, 84f, 22f, 0f, 0f);
        AddLayout(actions.gameObject, false, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

        if (isEquipped)
        {
            RectTransform badge = NewRect("Badge", actions);
            badge.gameObject.AddComponent<Image>().color = new Color(slotColor.r, slotColor.g, slotColor.b, 0.25f);
            SetLE(badge.gameObject, 52f, 18f, 0f, 0f);
            AddLayout(badge.gameObject, false, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            TextMeshProUGUI badgeText = NewText("Text", badge, equippedSlot == 0 ? "SLOT 1" : "SLOT 2", 7f, slotColor, TextAlignmentOptions.Midline, FontStyles.Bold);
            SetLE(badgeText.gameObject, 0f, 0f, 1f, 1f);

            // Mirrors PlayerInventory: the primary can only be removed if a secondary exists.
            bool canUnequip = equippedSlot == 1 || inv.SecondaryWeapon != null;
            if (canUnequip)
            {
                int slot = equippedSlot;
                Button x = NewButton("UnequipButton", actions, "X", Red, 8f, () => inv.UnequipWeapon(slot));
                SetLE(x.gameObject, 18f, 18f, 0f, 0f);
            }
        }
        else
        {
            Button s1 = NewButton("EquipSlot1", actions, "SLOT 1", Amber, 7f, () => inv.EquipWeapon(weapon, 0));
            SetLE(s1.gameObject, 40f, 18f, 0f, 0f);
            s1.GetComponentInChildren<TextMeshProUGUI>().color = DarkText;

            Button s2 = NewButton("EquipSlot2", actions, "SLOT 2", Blue, 7f, () => inv.EquipWeapon(weapon, 1));
            SetLE(s2.gameObject, 40f, 18f, 0f, 0f);
        }

        return card.gameObject;
    }

    // =====================================================================
    // Widget / layout helpers
    // =====================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Adds a layout group that controls child sizes; children size themselves via LayoutElement.</summary>
    private static void AddLayout(GameObject go, bool vertical, float spacing, RectOffset padding, TextAnchor align)
    {
        HorizontalOrVerticalLayoutGroup group = vertical
            ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>()
            : go.AddComponent<HorizontalLayoutGroup>();

        group.spacing = spacing;
        group.padding = padding;
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
    }

    /// <summary>Sets LayoutElement values. Pass a negative preferred size to defer to the content.</summary>
    private static void SetLE(GameObject go, float prefW, float prefH, float flexW, float flexH)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = prefW;
        le.preferredHeight = prefH;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        le.minWidth = prefW >= 0f ? prefW : -1f;
        le.minHeight = prefH >= 0f ? prefH : -1f;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private static Button NewButton(string name, Transform parent, string label, Color color, float fontSize, Action onClick)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(() => onClick());

        AddLayout(rt.gameObject, false, 0f, new RectOffset(2, 2, 0, 0), TextAnchor.MiddleCenter);
        TextMeshProUGUI txt = NewText("Text", rt, label, fontSize, Color.white, TextAlignmentOptions.Midline, FontStyles.Bold);
        SetLE(txt.gameObject, 0f, 0f, 1f, 1f);

        return btn;
    }
}
