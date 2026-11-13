using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents an individual item card in the right-side Inventory stash list.
/// Supports both click-to-equip and drag-and-drop onto compatible LoadoutSlotUI targets.
/// </summary>
public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image thumbBoxImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private Image equippedBadgeBg;
    [SerializeField] private TextMeshProUGUI equippedBadgeText;
    [SerializeField] private Button actionButton;
    [SerializeField] private Image actionButtonImage;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tactical Palette")]
    [SerializeField] private Color defaultBorderColor = new Color(0.22f, 0.27f, 0.23f, 1f);
    [SerializeField] private Color primaryColor = new Color(0.90f, 0.66f, 0.18f, 1f);
    [SerializeField] private Color secondaryColor = new Color(0.29f, 0.56f, 0.78f, 1f);
    [SerializeField] private Color equipmentColor = new Color(0.28f, 0.72f, 0.41f, 1f);
    [SerializeField] private Color equipButtonColor = new Color(0.24f, 0.29f, 0.25f, 1f);
    [SerializeField] private Color unequipButtonColor = new Color(0.65f, 0.22f, 0.22f, 1f);

    private ScriptableObject itemData;
    private PlayerInventory inventory;
    private InventoryDragController dragController;
    private bool isEquipped;
    private bool canUnequip;
    private bool isDraggingThisItem;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (iconImage != null)
            iconImage.raycastTarget = false;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(HandleActionButtonClick);
        }
    }

    public void Bind(ScriptableObject item, PlayerInventory inv, InventoryDragController dragCtrl)
    {
        itemData = item;
        inventory = inv;
        dragController = dragCtrl;

        if (item == null || inv == null)
            return;

        isEquipped = false;
        canUnequip = false;
        Color accentColor = defaultBorderColor;
        string badgeLabel = "";

        if (item is WeaponData weapon)
        {
            isEquipped = inv.IsWeaponEquipped(weapon, out int weaponSlot);
            if (isEquipped)
            {
                accentColor = weaponSlot == 0 ? primaryColor : secondaryColor;
                badgeLabel = weaponSlot == 0 ? "SLOT 1" : "SLOT 2";
                canUnequip = weaponSlot == 1 || inv.SecondaryWeapon != null;
            }

            if (iconImage != null)
            {
                iconImage.sprite = weapon.weaponSprite;
                iconImage.color = Color.white;
                iconImage.enabled = weapon.weaponSprite != null;
            }
            if (titleText != null)
                titleText.text = weapon.weaponName;
            if (statsText != null)
                statsText.text = $"<color=#FFB84D>DMG {weapon.damage:0}</color>  <color=#66CCFF>MAG {weapon.magazineSize}</color>  <color=#88FFAA>{(weapon.isAutomatic ? "AUTO" : "SEMI")}</color>";
        }
        else if (item is ConsumableData consumable)
        {
            isEquipped = inv.IsConsumableEquipped(consumable, out int consumableSlot);
            if (isEquipped)
            {
                accentColor = primaryColor;
                badgeLabel = $"SLOT {consumableSlot + 1}";
                canUnequip = true;
            }

            if (iconImage != null)
            {
                iconImage.sprite = consumable.icon;
                iconImage.color = consumable.tintColor;
                iconImage.enabled = true;
            }
            if (titleText != null)
                titleText.text = consumable.consumableName;
            if (statsText != null)
                statsText.text = consumable.GetStatsSummary();
        }
        else
        {
            isEquipped = inv.IsEquipmentEquipped(item, out EquipmentSlotType eqSlot);
            if (isEquipped)
            {
                accentColor = equipmentColor;
                badgeLabel = eqSlot == EquipmentSlotType.Vest ? "VEST" : "MASK";
                canUnequip = true;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = equipmentColor;
                iconImage.enabled = true;
            }
            if (titleText != null)
                titleText.text = item.name;
            if (statsText != null)
                statsText.text = "Equipment gear";
        }

        if (borderImage != null)
            borderImage.color = isEquipped ? accentColor : defaultBorderColor;

        if (equippedBadge != null)
            equippedBadge.SetActive(isEquipped);
        if (isEquipped && equippedBadgeBg != null)
            equippedBadgeBg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f);
        if (isEquipped && equippedBadgeText != null)
        {
            equippedBadgeText.text = badgeLabel;
            equippedBadgeText.color = accentColor;
        }

        if (actionButton != null)
        {
            if (!isEquipped)
            {
                actionButton.gameObject.SetActive(true);
                if (actionButtonImage != null) actionButtonImage.color = equipButtonColor;
                if (actionButtonText != null) actionButtonText.text = "EQUIP";
            }
            else if (canUnequip)
            {
                actionButton.gameObject.SetActive(true);
                if (actionButtonImage != null) actionButtonImage.color = unequipButtonColor;
                if (actionButtonText != null) actionButtonText.text = "X";
            }
            else
            {
                actionButton.gameObject.SetActive(false);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (actionButton != null && eventData.pointerPress != null &&
            eventData.pointerPress.transform.IsChildOf(actionButton.transform))
        {
            return;
        }

        if (!isEquipped)
        {
            AutoEquip();
        }
    }

    private void HandleActionButtonClick()
    {
        if (isEquipped)
        {
            UnequipCurrent();
        }
        else
        {
            AutoEquip();
        }
    }

    private void AutoEquip()
    {
        if (itemData == null || inventory == null)
            return;

        if (itemData is WeaponData weapon)
        {
            if (inventory.PrimaryWeapon == null)
                inventory.EquipWeapon(weapon, 0);
            else if (inventory.SecondaryWeapon == null && inventory.PrimaryWeapon != weapon)
                inventory.EquipWeapon(weapon, 1);
            else
                inventory.EquipWeapon(weapon, 0);
        }
        else if (itemData is ConsumableData consumable)
        {
            for (int s = 0; s < inventory.ConsumableSlotCount; s++)
            {
                if (s >= inventory.EquippedConsumables.Count || inventory.EquippedConsumables[s] == null)
                {
                    inventory.EquipConsumable(consumable, s);
                    return;
                }
            }
            inventory.EquipConsumable(consumable, 0);
        }
        else
        {
            if (inventory.EquippedVest == null)
                inventory.EquipEquipment(itemData, EquipmentSlotType.Vest);
            else if (inventory.EquippedMask == null)
                inventory.EquipEquipment(itemData, EquipmentSlotType.Mask);
            else
                inventory.EquipEquipment(itemData, EquipmentSlotType.Vest);
        }
    }

    private void UnequipCurrent()
    {
        if (itemData == null || inventory == null || !isEquipped || !canUnequip)
            return;

        if (itemData is WeaponData weapon && inventory.IsWeaponEquipped(weapon, out int wSlot))
        {
            inventory.UnequipWeapon(wSlot);
        }
        else if (itemData is ConsumableData consumable && inventory.IsConsumableEquipped(consumable, out int cSlot))
        {
            inventory.UnequipConsumable(cSlot);
        }
        else if (inventory.IsEquipmentEquipped(itemData, out EquipmentSlotType eqSlot))
        {
            inventory.UnequipEquipment(eqSlot);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null || dragController == null)
            return;

        isDraggingThisItem = true;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.45f;
        }

        Sprite icon = iconImage != null ? iconImage.sprite : null;
        Color iconColor = iconImage != null ? iconImage.color : Color.white;
        string label = titleText != null ? titleText.text : itemData.name;

        dragController.BeginDrag(
            itemData,
            icon,
            iconColor,
            label,
            transform as RectTransform,
            null,
            eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingThisItem || dragController == null)
            return;

        dragController.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingThisItem)
            return;

        isDraggingThisItem = false;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        if (dragController != null)
            dragController.EndDrag(eventData);
    }
}

