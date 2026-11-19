using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single equipped loadout slot (Primary, Secondary, Vest, Mask, or Consumable 1-4).
/// Styled for the minimal black tactical theme and supports click/drag equipping and unequipping.
/// </summary>
public class LoadoutSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum SlotKind
    {
        WeaponPrimary,
        WeaponSecondary,
        EquipmentVest,
        EquipmentMask,
        Consumable
    }

    [Header("Slot Configuration")]
    [SerializeField] private SlotKind slotKind = SlotKind.WeaponPrimary;
    [SerializeField] private int slotIndex = 0;

    [Header("Card References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private GameObject activeAccentBar;
    [SerializeField] private Image activeAccentBarImage;
    [SerializeField] private Image thumbBoxBorderImage;
    [SerializeField] private Image thumbBoxImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image emptyPlaceholderIcon;
    [SerializeField] private TextMeshProUGUI emptyKeybindText;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private Image statusBadgeBorder;
    [SerializeField] private Image statusBadgeBg;
    [SerializeField] private Image statusBadgeDot;
    [SerializeField] private TextMeshProUGUI statusBadgeText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject bottomRow;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private bool showStatsRow = true;
    [SerializeField] private Button unequipButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Consumable Stack Controls")]
    [SerializeField] private GameObject stackControlGroup;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private Button increaseButton;

    [Header("HUDUI Unified Palette")]
    [SerializeField] private Color emptyBorderColor = new Color(25f / 255f, 28f / 255f, 33f / 255f, 1f);            // #191C21
    [SerializeField] private Color occupiedWeaponBorderColor = new Color(46f / 255f, 52f / 255f, 60f / 255f, 1f);   // #2E343C
    [SerializeField] private Color occupiedGearBorderColor = new Color(35f / 255f, 39f / 255f, 45f / 255f, 1f);     // #23272D
    [SerializeField] private Color emptyInnerColor = new Color(9f / 255f, 10f / 255f, 12f / 255f, 1f);              // #090A0C
    [SerializeField] private Color occupiedInnerColor = new Color(15f / 255f, 17f / 255f, 20f / 255f, 1f);          // #0F1114
    [SerializeField] private Color thumbEmptyBorderColor = new Color(25f / 255f, 28f / 255f, 33f / 255f, 1f);       // #191C21
    [SerializeField] private Color thumbOccupiedBorderColor = new Color(35f / 255f, 39f / 255f, 45f / 255f, 1f);    // #23272D
    [SerializeField] private Color activeStatusColor = new Color(34f / 255f, 197f / 255f, 94f / 255f, 1f);          // #22C55E
    [SerializeField] private Color activeBadgeBgColor = new Color(10f / 255f, 34f / 255f, 20f / 255f, 1f);          // #0A2214
    [SerializeField] private Color activeBadgeBorderColor = new Color(22f / 255f, 101f / 255f, 52f / 255f, 1f);     // #166534
    [SerializeField] private Color inactiveBadgeBgColor = new Color(9f / 255f, 10f / 255f, 12f / 255f, 1f);         // #090A0C
    [SerializeField] private Color inactiveBadgeBorderColor = new Color(25f / 255f, 28f / 255f, 33f / 255f, 1f);    // #191C21
    [SerializeField] private Color inactiveDotColor = new Color(79f / 255f, 85f / 255f, 94f / 255f, 1f);            // #4F555E
    [SerializeField] private Color primaryTextColor = new Color(241f / 255f, 243f / 255f, 245f / 255f, 1f);         // #F1F3F5
    [SerializeField] private Color secondaryTextColor = new Color(138f / 255f, 145f / 255f, 154f / 255f, 1f);       // #8A919A
    [SerializeField] private Color mutedTextColor = new Color(79f / 255f, 85f / 255f, 94f / 255f, 1f);              // #4F555E
    [SerializeField] private Color highlightValidBorderColor = new Color(34f / 255f, 197f / 255f, 94f / 255f, 1f);  // #22C55E
    [SerializeField] private Color highlightValidInnerColor = new Color(10f / 255f, 34f / 255f, 20f / 255f, 1f);    // #0A2214

    [Header("Controllers")]
    [SerializeField] private InventoryDragController dragController;

    private ScriptableObject currentItem;
    private int currentStackQuantity;
    private bool canUnequipCurrent;
    private bool isDraggingThisSlot;

    public SlotKind Kind => slotKind;
    public int SlotIndex => slotIndex;
    public ScriptableObject CurrentItem => currentItem;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (iconImage != null)
            iconImage.raycastTarget = false;
        if (emptyPlaceholderIcon != null)
            emptyPlaceholderIcon.raycastTarget = false;

        if (unequipButton != null)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(UnequipThisSlot);
        }

        if (decreaseButton != null)
        {
            decreaseButton.onClick.RemoveAllListeners();
            decreaseButton.onClick.AddListener(HandleDecreaseClick);
        }

        if (increaseButton != null)
        {
            increaseButton.onClick.RemoveAllListeners();
            increaseButton.onClick.AddListener(HandleIncreaseClick);
        }
    }

    public void BindConsumableStack(ConsumableStack stack, int stashedCount)
    {
        ConsumableData item = (stack != null && !stack.IsEmpty) ? stack.item : null;
        currentStackQuantity = (stack != null && !stack.IsEmpty) ? stack.quantity : 0;

        Bind(item, item != null);

        bool showStackControls = slotKind == SlotKind.Consumable && item != null && item.isStackable;
        if (stackControlGroup != null)
            stackControlGroup.SetActive(showStackControls);

        if (showStackControls)
        {
            if (stackCountText != null)
            {
                stackCountText.text = $"{currentStackQuantity}x";
                stackCountText.color = activeStatusColor;
            }
            bool canDecrease = currentStackQuantity > 0;
            bool canIncrease = stashedCount > 0 && currentStackQuantity < item.maxQuickSlotStack;
            SetStackButtonState(decreaseButton, canDecrease);
            SetStackButtonState(increaseButton, canIncrease);
        }

        bool showUnequip = item != null && canUnequipCurrent;
        if (bottomRow != null)
            bottomRow.SetActive(showStatsRow || showUnequip || showStackControls);
    }

    private void SetStackButtonState(Button btn, bool enabled)
    {
        if (btn == null) return;
        btn.interactable = enabled;
        TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.color = enabled ? primaryTextColor : mutedTextColor;
        Image border = btn.GetComponent<Image>();
        if (border != null)
            border.color = enabled ? occupiedGearBorderColor : emptyBorderColor;
    }

    public void Bind(ScriptableObject item, bool canUnequip)
    {
        currentItem = item;
        canUnequipCurrent = canUnequip;

        bool occupied = item != null;

        if (item is WeaponData weapon)
        {
            if (iconImage != null)
            {
                iconImage.sprite = weapon.weaponSprite;
                iconImage.color = Color.white;
                iconImage.enabled = weapon.weaponSprite != null;
            }
            if (nameText != null)
            {
                nameText.text = weapon.weaponName;
                nameText.color = primaryTextColor;
            }
            if (statsText != null)
            {
                statsText.text =
                    $"<color=#8A919A>DMG {weapon.damage:0}</color>  <color=#4F555E>•</color>  " +
                    $"<color=#8A919A>MAG {weapon.magazineSize}</color>  <color=#4F555E>•</color>  " +
                    $"<color=#4F555E>{(weapon.isAutomatic ? "AUTO" : "SEMI")}</color>";
            }
        }
        else if (item is ConsumableData consumable)
        {
            if (iconImage != null)
            {
                iconImage.sprite = consumable.icon;
                iconImage.color = consumable.tintColor;
                iconImage.enabled = true;
            }
            if (nameText != null)
            {
                nameText.text = consumable.consumableName;
                nameText.color = primaryTextColor;
            }
            if (statsText != null)
            {
                statsText.text = consumable.GetStatsSummary();
            }
        }
        else if (item != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = activeStatusColor;
                iconImage.enabled = true;
            }
            if (nameText != null)
            {
                nameText.text = item.name;
                nameText.color = primaryTextColor;
            }
            if (statsText != null)
            {
                statsText.text = "<color=#8A919A>Equipped gear</color>";
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (nameText != null)
            {
                nameText.text = "EMPTY";
                nameText.color = mutedTextColor;
            }
            if (statsText != null)
            {
                statsText.text = $"<color=#4F555E>{GetEmptyHint()}</color>";
            }
        }

        if (emptyPlaceholderIcon != null)
            emptyPlaceholderIcon.enabled = !occupied && emptyPlaceholderIcon.sprite != null;

        if (emptyKeybindText != null)
            emptyKeybindText.gameObject.SetActive(!occupied);

        if (activeAccentBar != null)
            activeAccentBar.SetActive(occupied);
        if (activeAccentBarImage != null)
            activeAccentBarImage.color = activeStatusColor;

        if (headerText != null)
            headerText.color = occupied ? secondaryTextColor : mutedTextColor;

        UpdateStatusBadge(occupied);
        ApplyRestingCardColors(occupied);

        bool showUnequip = occupied && canUnequip;
        if (unequipButton != null)
            unequipButton.gameObject.SetActive(showUnequip);

        if (stackControlGroup != null)
            stackControlGroup.SetActive(false);

        if (bottomRow != null)
            bottomRow.SetActive(showStatsRow || showUnequip);
    }

    private void HandleDecreaseClick()
    {
        if (dragController != null && dragController.IsDragging)
            return;
        if (slotKind != SlotKind.Consumable || PlayerInventory.Instance == null)
            return;

        PlayerInventory.Instance.AdjustConsumableSlotQuantity(slotIndex, -1);
    }

    private void HandleIncreaseClick()
    {
        if (dragController != null && dragController.IsDragging)
            return;
        if (slotKind != SlotKind.Consumable || PlayerInventory.Instance == null)
            return;

        PlayerInventory.Instance.AdjustConsumableSlotQuantity(slotIndex, 1);
    }

    private void UpdateStatusBadge(bool occupied)
    {
        if (statusBadgeText != null)
        {
            statusBadgeText.text = occupied ? "ACTIVE" : "INACTIVE";
            statusBadgeText.color = occupied ? activeStatusColor : mutedTextColor;
        }
        if (statusBadgeDot != null)
            statusBadgeDot.color = occupied ? activeStatusColor : inactiveDotColor;
        if (statusBadgeBg != null)
            statusBadgeBg.color = occupied ? activeBadgeBgColor : inactiveBadgeBgColor;
        if (statusBadgeBorder != null)
            statusBadgeBorder.color = occupied ? activeBadgeBorderColor : inactiveBadgeBorderColor;
    }

    private void ApplyRestingCardColors(bool occupied)
    {
        bool isWeaponSlot = slotKind == SlotKind.WeaponPrimary || slotKind == SlotKind.WeaponSecondary;
        if (borderImage != null)
            borderImage.color = occupied
                ? (isWeaponSlot ? occupiedWeaponBorderColor : occupiedGearBorderColor)
                : emptyBorderColor;

        if (innerImage != null)
            innerImage.color = occupied ? occupiedInnerColor : emptyInnerColor;

        if (thumbBoxBorderImage != null)
            thumbBoxBorderImage.color = occupied ? thumbOccupiedBorderColor : thumbEmptyBorderColor;
    }

    private string GetEmptyHint()
    {
        switch (slotKind)
        {
            case SlotKind.WeaponPrimary:
            case SlotKind.WeaponSecondary:
                return "No weapon equipped";
            case SlotKind.EquipmentVest:
                return "No vest equipped";
            case SlotKind.EquipmentMask:
                return "No mask equipped";
            default:
                return "Empty slot";
        }
    }

    public bool IsCompatible(ScriptableObject item)
    {
        if (item == null) return false;

        switch (slotKind)
        {
            case SlotKind.WeaponPrimary:
            case SlotKind.WeaponSecondary:
                return item is WeaponData;

            case SlotKind.Consumable:
                return item is ConsumableData;

            case SlotKind.EquipmentVest:
            case SlotKind.EquipmentMask:
                return !(item is WeaponData) && !(item is ConsumableData);

            default:
                return false;
        }
    }

    public void SetHighlight(bool highlight, bool isValid)
    {
        if (!highlight)
        {
            if (canvasGroup != null && !isDraggingThisSlot)
                canvasGroup.alpha = 1f;
            ApplyRestingCardColors(currentItem != null);
            return;
        }

        if (isValid)
        {
            if (canvasGroup != null && !isDraggingThisSlot)
                canvasGroup.alpha = 1f;
            if (borderImage != null)
                borderImage.color = highlightValidBorderColor;
            if (innerImage != null)
                innerImage.color = highlightValidInnerColor;
        }
        else
        {
            if (canvasGroup != null && !isDraggingThisSlot)
                canvasGroup.alpha = 0.45f;
            if (borderImage != null)
                borderImage.color = emptyBorderColor;
            if (innerImage != null)
                innerImage.color = emptyInnerColor;
        }
    }

    public void UnequipThisSlot()
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        switch (slotKind)
        {
            case SlotKind.WeaponPrimary:
                inv.UnequipWeapon(0);
                break;
            case SlotKind.WeaponSecondary:
                inv.UnequipWeapon(1);
                break;
            case SlotKind.EquipmentVest:
                inv.UnequipEquipment(EquipmentSlotType.Vest);
                break;
            case SlotKind.EquipmentMask:
                inv.UnequipEquipment(EquipmentSlotType.Mask);
                break;
            case SlotKind.Consumable:
                inv.UnequipConsumable(slotIndex);
                break;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (dragController == null || !dragController.IsDragging)
            return;

        ScriptableObject dropped = dragController.DraggedItem;
        if (!IsCompatible(dropped))
            return;

        if (dragController.SourceLoadoutSlot == this)
        {
            dragController.MarkDropHandled();
            return;
        }

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        // Prevent moving the sole equipped weapon out of Primary into Secondary and leaving Primary null
        if (slotKind == SlotKind.WeaponSecondary &&
            dragController.SourceLoadoutSlot != null &&
            dragController.SourceLoadoutSlot.Kind == SlotKind.WeaponPrimary &&
            inv.PrimaryWeapon == dropped &&
            inv.SecondaryWeapon == null)
        {
            return;
        }

        bool equipped = false;
        switch (slotKind)
        {
            case SlotKind.WeaponPrimary:
                equipped = inv.EquipWeapon(dropped as WeaponData, 0);
                break;
            case SlotKind.WeaponSecondary:
                equipped = inv.EquipWeapon(dropped as WeaponData, 1);
                break;
            case SlotKind.EquipmentVest:
                equipped = inv.EquipEquipment(dropped, EquipmentSlotType.Vest);
                break;
            case SlotKind.EquipmentMask:
                equipped = inv.EquipEquipment(dropped, EquipmentSlotType.Mask);
                break;
            case SlotKind.Consumable:
                if (dragController.SourceLoadoutSlot != null &&
                    dragController.SourceLoadoutSlot.Kind == SlotKind.Consumable)
                {
                    equipped = inv.SwapConsumableSlots(dragController.SourceLoadoutSlot.SlotIndex, slotIndex);
                }
                else
                {
                    equipped = inv.EquipConsumable(dropped as ConsumableData, slotIndex);
                }
                break;
        }

        if (equipped)
            dragController.MarkDropHandled();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null || dragController == null)
            return;

        if (eventData != null && eventData.pointerPress != null)
        {
            Transform pressTransform = eventData.pointerPress.transform;
            if ((unequipButton != null && pressTransform.IsChildOf(unequipButton.transform)) ||
                (decreaseButton != null && pressTransform.IsChildOf(decreaseButton.transform)) ||
                (increaseButton != null && pressTransform.IsChildOf(increaseButton.transform)))
            {
                return;
            }
        }

        isDraggingThisSlot = true;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.4f;
        }

        Sprite icon = iconImage != null ? iconImage.sprite : null;
        Color iconColor = iconImage != null ? iconImage.color : Color.white;
        string label = nameText != null ? nameText.text : currentItem.name;
        if (slotKind == SlotKind.Consumable && currentItem is ConsumableData cd && cd.isStackable && currentStackQuantity > 0)
            label = $"{currentStackQuantity}x {cd.consumableName}";

        dragController.BeginDrag(
            currentItem,
            icon,
            iconColor,
            label,
            transform as RectTransform,
            this,
            eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingThisSlot || dragController == null)
            return;

        dragController.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingThisSlot)
            return;

        isDraggingThisSlot = false;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        if (dragController != null)
            dragController.EndDrag(eventData);
    }
}
