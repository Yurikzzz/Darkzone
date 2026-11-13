using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the authored uGUI Loadout & Inventory station modal window.
/// Binds Left Loadout slots (Primary, Secondary, Vest, Mask, Consumables 1-4) and
/// Right Inventory category tabs/scrollable list to PlayerInventory events.
/// </summary>
public class LoadoutUI : MonoBehaviour, IDropHandler
{
    public static LoadoutUI Instance { get; private set; }

    public enum TabCategory
    {
        Weapons = 0,
        Equipment = 1,
        Consumables = 2
    }

    [Header("Root & Header/Footer Controls")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button swapButton;

    [Header("Left Panel - Loadout Slots")]
    [SerializeField] private LoadoutSlotUI primarySlot;
    [SerializeField] private LoadoutSlotUI secondarySlot;
    [SerializeField] private LoadoutSlotUI vestSlot;
    [SerializeField] private LoadoutSlotUI maskSlot;
    [SerializeField] private LoadoutSlotUI[] consumableSlots;

    [Header("Right Panel - Inventory Tabs & Scroll List")]
    [SerializeField] private RectTransform inventoryDropArea;
    [SerializeField] private Button[] tabButtons = new Button[3];
    [SerializeField] private Image[] tabImages = new Image[3];
    [SerializeField] private TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[3];
    [SerializeField] private ScrollRect inventoryScrollRect;
    [SerializeField] private RectTransform inventoryContentRoot;
    [SerializeField] private GameObject emptyNoticeObj;
    [SerializeField] private TextMeshProUGUI emptyNoticeText;
    [SerializeField] private InventoryItemSlotUI inventoryItemSlotPrefab;

    [Header("Drag Controller")]
    [SerializeField] private InventoryDragController dragController;

    [Header("Tactical Tab Palette")]
    [SerializeField] private Color activeTabColor = new Color(0.90f, 0.66f, 0.18f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.16f, 0.19f, 0.17f, 1f);
    [SerializeField] private Color activeTabTextColor = new Color(0.08f, 0.09f, 0.08f, 1f);
    [SerializeField] private Color inactiveTabTextColor = new Color(0.58f, 0.63f, 0.59f, 1f);

    private TabCategory currentTab = TabCategory.Weapons;
    private PlayerMovement playerMovement;
    private readonly List<GameObject> spawnedItemRows = new List<GameObject>();
    private int openedFrame = -1;

    public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

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

        WireButtons();

        transform.SetAsLastSibling();
        if (modalRoot != null)
            modalRoot.SetActive(false);
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

        // Escape closes the menu. Interact key (E) is handled by LoadoutStation.Toggle().
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(() =>
            {
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.SwapEquippedSlots();
            });
        }

        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                TabCategory captured = (TabCategory)i;
                tabButtons[i].onClick.RemoveAllListeners();
                tabButtons[i].onClick.AddListener(() => SwitchTab(captured));
            }
        }
    }

    // =====================================================================
    // Public API
    // =====================================================================

    /// <summary>Opens the loadout station UI and disables player movement.</summary>
    public void Open()
    {
        openedFrame = Time.frameCount;

        transform.SetAsLastSibling();

        if (modalRoot != null)
        {
            modalRoot.SetActive(true);
        }

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
        if (dragController != null)
            dragController.CancelDragImmediate();

        if (modalRoot != null)
            modalRoot.SetActive(false);

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

    /// <summary>Switches the right-side inventory tab category.</summary>
    public void SwitchTab(TabCategory tab)
    {
        currentTab = tab;

        for (int i = 0; i < tabImages.Length; i++)
        {
            bool active = i == (int)tab;
            if (tabImages[i] != null) tabImages[i].color = active ? activeTabColor : inactiveTabColor;
            if (tabTexts[i] != null) tabTexts[i].color = active ? activeTabTextColor : inactiveTabTextColor;
        }

        if (inventoryScrollRect != null)
            inventoryScrollRect.verticalNormalizedPosition = 1f;

        RefreshUI();
    }

    /// <summary>Refreshes left loadout slots, tab counters, and the active inventory list.</summary>
    public void RefreshUI()
    {
        if (!IsOpen) return;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        // Weapons (Primary cannot be unequipped if it is the only equipped weapon)
        if (primarySlot != null)
            primarySlot.Bind(inv.PrimaryWeapon, inv.SecondaryWeapon != null);
        if (secondarySlot != null)
            secondarySlot.Bind(inv.SecondaryWeapon, inv.SecondaryWeapon != null);

        if (swapButton != null)
            swapButton.interactable = inv.PrimaryWeapon != null && inv.SecondaryWeapon != null;

        // Equipment (Vest & Mask)
        if (vestSlot != null)
            vestSlot.Bind(inv.EquippedVest, inv.EquippedVest != null);
        if (maskSlot != null)
            maskSlot.Bind(inv.EquippedMask, inv.EquippedMask != null);

        // Consumables (4 quick slots)
        var equippedConsumables = inv.EquippedConsumables;
        if (consumableSlots != null)
        {
            for (int i = 0; i < consumableSlots.Length; i++)
            {
                if (consumableSlots[i] == null) continue;
                ScriptableObject item = (equippedConsumables != null && i < equippedConsumables.Count)
                    ? equippedConsumables[i]
                    : null;
                consumableSlots[i].Bind(item, item != null);
            }
        }

        RefreshTabLabels(inv);
        RefreshInventoryList(inv);
    }

    /// <summary>Highlights compatible target loadout slots while dragging an item.</summary>
    public void HighlightCompatibleSlots(ScriptableObject draggedItem, bool active)
    {
        LoadoutSlotUI[] allSlots = GetAllLoadoutSlots();
        for (int i = 0; i < allSlots.Length; i++)
        {
            LoadoutSlotUI slot = allSlots[i];
            if (slot == null) continue;

            if (!active || draggedItem == null)
            {
                slot.SetHighlight(false, false);
            }
            else
            {
                bool valid = slot.IsCompatible(draggedItem);
                slot.SetHighlight(true, valid);
            }
        }
    }

    /// <summary>
    /// Allows dragging an already-equipped item from a Left Loadout slot onto the
    /// Right Inventory area to unequip it cleanly.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (dragController == null || !dragController.IsDragging)
            return;

        LoadoutSlotUI sourceSlot = dragController.SourceLoadoutSlot;
        if (sourceSlot == null || inventoryDropArea == null)
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(
                inventoryDropArea,
                eventData.position,
                eventData.pressEventCamera))
        {
            return;
        }

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        bool unequipped = false;
        switch (sourceSlot.Kind)
        {
            case LoadoutSlotUI.SlotKind.WeaponPrimary:
                unequipped = inv.UnequipWeapon(0);
                break;
            case LoadoutSlotUI.SlotKind.WeaponSecondary:
                unequipped = inv.UnequipWeapon(1);
                break;
            case LoadoutSlotUI.SlotKind.EquipmentVest:
                unequipped = inv.UnequipEquipment(EquipmentSlotType.Vest);
                break;
            case LoadoutSlotUI.SlotKind.EquipmentMask:
                unequipped = inv.UnequipEquipment(EquipmentSlotType.Mask);
                break;
            case LoadoutSlotUI.SlotKind.Consumable:
                unequipped = inv.UnequipConsumable(sourceSlot.SlotIndex);
                break;
        }

        if (unequipped)
            dragController.MarkDropHandled();
    }

    // =====================================================================
    // Refresh helpers
    // =====================================================================

    private LoadoutSlotUI[] GetAllLoadoutSlots()
    {
        int cCount = consumableSlots != null ? consumableSlots.Length : 0;
        LoadoutSlotUI[] list = new LoadoutSlotUI[4 + cCount];
        list[0] = primarySlot;
        list[1] = secondarySlot;
        list[2] = vestSlot;
        list[3] = maskSlot;
        for (int i = 0; i < cCount; i++)
            list[4 + i] = consumableSlots[i];
        return list;
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
        {
            if (list[i] != null) n++;
        }
        return n;
    }

    private void SetTabLabel(TabCategory tab, string label)
    {
        int idx = (int)tab;
        if (tabTexts != null && idx >= 0 && idx < tabTexts.Length && tabTexts[idx] != null)
            tabTexts[idx].text = label;
    }

    private void RefreshInventoryList(PlayerInventory inv)
    {
        for (int i = 0; i < spawnedItemRows.Count; i++)
        {
            GameObject row = spawnedItemRows[i];
            if (row == null) continue;
            row.transform.SetParent(null, false);
            Destroy(row);
        }
        spawnedItemRows.Clear();

        if (inventoryContentRoot == null || inventoryItemSlotPrefab == null)
            return;

        int spawnedCount = 0;

        if (currentTab == TabCategory.Weapons)
        {
            IReadOnlyList<WeaponData> weapons = inv.OwnedWeapons;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (weapons[i] == null) continue;
                    SpawnItemCard(weapons[i], inv);
                    spawnedCount++;
                }
            }
            SetEmptyNotice(spawnedCount == 0, "No weapons in stash.\nOpen Darkzone chests to find weapons.");
        }
        else if (currentTab == TabCategory.Equipment)
        {
            IReadOnlyList<ScriptableObject> equipment = inv.OwnedEquipment;
            if (equipment != null)
            {
                for (int i = 0; i < equipment.Count; i++)
                {
                    if (equipment[i] == null) continue;
                    SpawnItemCard(equipment[i], inv);
                    spawnedCount++;
                }
            }
            SetEmptyNotice(spawnedCount == 0, "No equipment in stash.\nVest & Mask items will appear here.");
        }
        else if (currentTab == TabCategory.Consumables)
        {
            IReadOnlyList<ScriptableObject> consumables = inv.OwnedConsumables;
            if (consumables != null)
            {
                for (int i = 0; i < consumables.Count; i++)
                {
                    if (consumables[i] == null) continue;
                    SpawnItemCard(consumables[i], inv);
                    spawnedCount++;
                }
            }
            SetEmptyNotice(spawnedCount == 0, "No consumables in stash.\nDefeat enemies or open chests to find some!");
        }
    }

    private void SpawnItemCard(ScriptableObject item, PlayerInventory inv)
    {
        InventoryItemSlotUI card = Instantiate(inventoryItemSlotPrefab, inventoryContentRoot);
        card.name = $"ItemCard_{item.name}";
        card.Bind(item, inv, dragController);
        spawnedItemRows.Add(card.gameObject);
    }

    private void SetEmptyNotice(bool visible, string message)
    {
        if (emptyNoticeObj != null)
            emptyNoticeObj.SetActive(visible);
        if (visible && emptyNoticeText != null)
            emptyNoticeText.text = message;
    }
}
