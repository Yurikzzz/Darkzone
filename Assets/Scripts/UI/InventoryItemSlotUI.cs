using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents an individual item card in the right-side Inventory stash list.
/// Supports click-to-equip, drag-and-drop onto compatible LoadoutSlotUI targets,
/// unequipping via UNEQUIP, and permanent removal via the trash button.
/// </summary>
public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image innerImage;
    [SerializeField] private Image thumbBoxBorderImage;
    [SerializeField] private Image thumbBoxImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Item Actions")]
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private Image equippedBadgeBorder;
    [SerializeField] private Image equippedBadgeBg;
    [SerializeField] private Image equippedBadgeDot;
    [SerializeField] private TextMeshProUGUI equippedBadgeText;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button trashButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("HUDUI Unified Palette")]
    [SerializeField] private Color defaultBorderColor = new Color(35f / 255f, 39f / 255f, 45f / 255f, 1f);      // #23272D
    [SerializeField] private Color equippedBorderColor = new Color(46f / 255f, 52f / 255f, 60f / 255f, 1f);     // #2E343C
    [SerializeField] private Color cardFillColor = new Color(15f / 255f, 17f / 255f, 20f / 255f, 1f);           // #0F1114
    [SerializeField] private Color activeStatusColor = new Color(34f / 255f, 197f / 255f, 94f / 255f, 1f);      // #22C55E
    [SerializeField] private Color activeBadgeBgColor = new Color(10f / 255f, 34f / 255f, 20f / 255f, 1f);      // #0A2214
    [SerializeField] private Color activeBadgeBorderColor = new Color(22f / 255f, 101f / 255f, 52f / 255f, 1f); // #166534

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

        if (unequipButton != null)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(HandleUnequipClick);
        }

        if (trashButton != null)
        {
            trashButton.onClick.RemoveAllListeners();
            trashButton.onClick.AddListener(HandleTrashClick);
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
        string badgeLabel = "";

        if (item is WeaponData weapon)
        {
            isEquipped = inv.IsWeaponEquipped(weapon, out int weaponSlot);
            if (isEquipped)
            {
                badgeLabel = weaponSlot == 0 ? "SLOT 1 · ACTIVE" : "SLOT 2 · ACTIVE";
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
            {
                statsText.text =
                    $"<color=#8A919A>DMG {weapon.damage:0}</color>  <color=#4F555E>•</color>  " +
                    $"<color=#8A919A>MAG {weapon.magazineSize}</color>  <color=#4F555E>•</color>  " +
                    $"<color=#4F555E>{(weapon.isAutomatic ? "AUTO" : "SEMI")}</color>";
            }
        }
        else if (item is ConsumableData consumable)
        {
            isEquipped = inv.IsConsumableEquipped(consumable, out int consumableSlot);
            if (isEquipped)
            {
                badgeLabel = $"SLOT {consumableSlot + 1} · ACTIVE";
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
                badgeLabel = eqSlot == EquipmentSlotType.Vest ? "VEST · ACTIVE" : "MASK · ACTIVE";
                canUnequip = true;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = activeStatusColor;
                iconImage.enabled = true;
            }
            if (titleText != null)
                titleText.text = item.name;
            if (statsText != null)
                statsText.text = "<color=#8A919A>Equipment gear</color>";
        }

        if (borderImage != null)
            borderImage.color = isEquipped ? equippedBorderColor : defaultBorderColor;

        if (innerImage != null)
            innerImage.color = cardFillColor;

        if (equippedBadge != null)
            equippedBadge.SetActive(isEquipped);

        if (isEquipped)
        {
            if (equippedBadgeBorder != null) equippedBadgeBorder.color = activeBadgeBorderColor;
            if (equippedBadgeBg != null) equippedBadgeBg.color = activeBadgeBgColor;
            if (equippedBadgeDot != null) equippedBadgeDot.color = activeStatusColor;
            if (equippedBadgeText != null)
            {
                equippedBadgeText.text = badgeLabel;
                equippedBadgeText.color = activeStatusColor;
            }
        }

        if (unequipButton != null)
            unequipButton.gameObject.SetActive(isEquipped && canUnequip);

        if (trashButton != null)
            trashButton.gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (eventData.pointerPress != null)
        {
            Transform pressTransform = eventData.pointerPress.transform;
            if ((unequipButton != null && pressTransform.IsChildOf(unequipButton.transform)) ||
                (trashButton != null && pressTransform.IsChildOf(trashButton.transform)))
            {
                return;
            }
        }

        if (!isEquipped)
        {
            AutoEquip();
        }
    }

    private void HandleUnequipClick()
    {
        UnequipCurrent();
    }

    private void HandleTrashClick()
    {
        if (itemData == null || inventory == null)
            return;

        inventory.RemoveItem(itemData);
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
            canvasGroup.alpha = 0.4f;
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
