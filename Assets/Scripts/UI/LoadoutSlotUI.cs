using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single equipped loadout slot (Primary, Secondary, Vest, Mask, or Consumable 1-4).
/// Supports drag-and-drop equipping, dragging out to swap/unequip, and clicking X to unequip.
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

    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image thumbBoxImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button unequipButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tactical Palette")]
    [SerializeField] private Color defaultBorderColor = new Color(0.22f, 0.27f, 0.23f, 1f);
    [SerializeField] private Color activeAccentColor = new Color(0.90f, 0.66f, 0.18f, 1f);
    [SerializeField] private Color highlightValidColor = new Color(0.32f, 0.82f, 0.44f, 1f);
    [SerializeField] private Color defaultInnerColor = new Color(0.11f, 0.13f, 0.12f, 1f);
    [SerializeField] private Color highlightInnerColor = new Color(0.14f, 0.24f, 0.17f, 1f);

    [Header("Controllers")]
    [SerializeField] private InventoryDragController dragController;

    private ScriptableObject currentItem;
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

        if (unequipButton != null)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(UnequipThisSlot);
        }
    }

    public void Bind(ScriptableObject item, bool canUnequip)
    {
        currentItem = item;
        canUnequipCurrent = canUnequip;

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
                nameText.color = Color.white;
            }
            if (statsText != null)
            {
                statsText.text = $"<color=#FFB84D>DMG {weapon.damage:0}</color>  <color=#66CCFF>MAG {weapon.magazineSize}</color>  <color=#88FFAA>{(weapon.isAutomatic ? "AUTO" : "SEMI")}</color>";
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
                nameText.color = consumable.tintColor;
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
                iconImage.color = new Color(0.45f, 0.75f, 0.50f, 1f);
                iconImage.enabled = true;
            }
            if (nameText != null)
            {
                nameText.text = item.name;
                nameText.color = Color.white;
            }
            if (statsText != null)
            {
                statsText.text = "Equipped gear";
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
                nameText.color = new Color(0.50f, 0.55f, 0.51f, 1f);
            }
            if (statsText != null)
            {
                statsText.text = GetEmptyHint();
            }
        }

        if (borderImage != null)
            borderImage.color = item != null ? activeAccentColor : defaultBorderColor;

        if (innerImage != null)
            innerImage.color = defaultInnerColor;

        if (unequipButton != null)
            unequipButton.gameObject.SetActive(item != null && canUnequip);
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
            if (borderImage != null)
                borderImage.color = currentItem != null ? activeAccentColor : defaultBorderColor;
            if (innerImage != null)
                innerImage.color = defaultInnerColor;
            return;
        }

        if (isValid)
        {
            if (canvasGroup != null && !isDraggingThisSlot)
                canvasGroup.alpha = 1f;
            if (borderImage != null)
                borderImage.color = highlightValidColor;
            if (innerImage != null)
                innerImage.color = highlightInnerColor;
        }
        else
        {
            if (canvasGroup != null && !isDraggingThisSlot)
                canvasGroup.alpha = 0.55f;
            if (borderImage != null)
                borderImage.color = defaultBorderColor;
            if (innerImage != null)
                innerImage.color = defaultInnerColor;
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
                equipped = inv.EquipConsumable(dropped as ConsumableData, slotIndex);
                break;
        }

        if (equipped)
            dragController.MarkDropHandled();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Reserved for future slot inspection or double-click behaviors;
        // explicit unequip uses the [X] button or dragging out to the inventory panel.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null || dragController == null)
            return;

        isDraggingThisSlot = true;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.45f;
        }

        Sprite icon = iconImage != null ? iconImage.sprite : null;
        Color iconColor = iconImage != null ? iconImage.color : Color.white;
        string label = nameText != null ? nameText.text : currentItem.name;

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

