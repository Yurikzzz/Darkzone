using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages player inventory, stash, and 2-weapon loadout.
/// Tracks equipped primary and secondary weapons, reserved consumable slots,
/// and owned items in the player's stash.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Equipped Weapons (Max 2)")]
    [Tooltip("Primary equipped weapon (Slot 1). Starts with Pistol.")]
    [SerializeField] private WeaponData primaryWeapon;

    [Tooltip("Secondary equipped weapon (Slot 2). Starts empty until equipped at a Loadout Station.")]
    [SerializeField] private WeaponData secondaryWeapon;

    [Header("Equipped Consumables (Reserved for future)")]
    [Tooltip("Number of quick-access consumable slots.")]
    [SerializeField] private int consumableSlotCount = 4;
    [SerializeField] private List<ScriptableObject> equippedConsumables = new List<ScriptableObject>();

    [Header("Owned Items / Stash")]
    [Tooltip("List of all weapons owned by the player.")]
    [SerializeField] private List<WeaponData> ownedWeapons = new List<WeaponData>();

    [Tooltip("List of all equipment pieces owned by the player (coming soon).")]
    [SerializeField] private List<ScriptableObject> ownedEquipment = new List<ScriptableObject>();

    [Tooltip("List of all consumables owned by the player (coming soon).")]
    [SerializeField] private List<ScriptableObject> ownedConsumables = new List<ScriptableObject>();

    private WeaponController weaponController;

    public event Action OnInventoryChanged;

    public WeaponData PrimaryWeapon => primaryWeapon;
    public WeaponData SecondaryWeapon => secondaryWeapon;
    public int ConsumableSlotCount => consumableSlotCount;
    public IReadOnlyList<ScriptableObject> EquippedConsumables => equippedConsumables;
    public IReadOnlyList<WeaponData> OwnedWeapons => ownedWeapons;
    public IReadOnlyList<ScriptableObject> OwnedEquipment => ownedEquipment;
    public IReadOnlyList<ScriptableObject> OwnedConsumables => ownedConsumables;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        weaponController = GetComponent<WeaponController>() ?? GetComponentInChildren<WeaponController>();

        // Ensure consumable slots list is initialized to the requested count
        while (equippedConsumables.Count < consumableSlotCount)
        {
            equippedConsumables.Add(null);
        }
    }

    private void Start()
    {
        // Auto-find default pistol if primary weapon is not set
        if (primaryWeapon == null && ownedWeapons.Count > 0)
        {
            primaryWeapon = ownedWeapons.Find(w => w != null && w.name.ToLower().Contains("pistol")) ?? ownedWeapons[0];
        }

        SyncWithWeaponController();
    }

    /// <summary>
    /// Synchronizes the player's 2 equipped weapons with the WeaponController.
    /// </summary>
    public void SyncWithWeaponController()
    {
        if (weaponController == null)
            weaponController = GetComponent<WeaponController>() ?? GetComponentInChildren<WeaponController>();

        if (weaponController == null) return;

        weaponController.SetEquippedSlots(primaryWeapon, secondaryWeapon);
    }

    /// <summary>
    /// Checks whether a weapon is currently equipped in either slot.
    /// </summary>
    public bool IsWeaponEquipped(WeaponData weapon, out int slotIndex)
    {
        if (weapon != null)
        {
            if (primaryWeapon == weapon)
            {
                slotIndex = 0;
                return true;
            }
            if (secondaryWeapon == weapon)
            {
                slotIndex = 1;
                return true;
            }
        }
        slotIndex = -1;
        return false;
    }

    /// <summary>
    /// Equips a weapon into the specified slot (0 = Primary, 1 = Secondary).
    /// If the weapon was already equipped in the other slot, it swaps or moves it.
    /// </summary>
    public bool EquipWeapon(WeaponData weapon, int slotIndex)
    {
        if (weapon == null || (slotIndex != 0 && slotIndex != 1))
            return false;

        // Ensure weapon is in owned weapons
        if (!ownedWeapons.Contains(weapon))
        {
            ownedWeapons.Add(weapon);
        }

        if (slotIndex == 0) // Primary slot
        {
            if (secondaryWeapon == weapon)
            {
                // Swap slots
                secondaryWeapon = primaryWeapon;
            }
            primaryWeapon = weapon;
        }
        else // Secondary slot
        {
            if (primaryWeapon == weapon)
            {
                // Swap slots
                primaryWeapon = secondaryWeapon;
            }
            secondaryWeapon = weapon;
        }

        SyncWithWeaponController();
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Unequips the weapon in the given slot.
    /// Ensures at least one weapon remains equipped.
    /// </summary>
    public bool UnequipWeapon(int slotIndex)
    {
        if (slotIndex == 0)
        {
            if (secondaryWeapon != null)
            {
                primaryWeapon = secondaryWeapon;
                secondaryWeapon = null;
            }
            else
            {
                // Cannot unequip the only remaining weapon
                return false;
            }
        }
        else if (slotIndex == 1)
        {
            secondaryWeapon = null;
        }
        else
        {
            return false;
        }

        SyncWithWeaponController();
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Swaps the Primary and Secondary equipped weapon slots.
    /// </summary>
    public void SwapEquippedSlots()
    {
        WeaponData temp = primaryWeapon;
        primaryWeapon = secondaryWeapon;
        secondaryWeapon = temp;

        SyncWithWeaponController();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Adds a weapon to the player's owned stash.
    /// If the secondary weapon slot is currently empty, automatically equips it.
    /// </summary>
    public void AddOwnedWeapon(WeaponData weapon)
    {
        if (weapon == null) return;

        if (!ownedWeapons.Contains(weapon))
        {
            ownedWeapons.Add(weapon);
        }

        // Auto-equip into secondary slot if empty
        if (secondaryWeapon == null && primaryWeapon != weapon)
        {
            EquipWeapon(weapon, 1);
        }
        else
        {
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Sets the list of all owned weapons (e.g. for initial setup or loading).
    /// </summary>
    public void SetOwnedWeapons(List<WeaponData> weapons)
    {
        ownedWeapons = new List<WeaponData>(weapons);
        OnInventoryChanged?.Invoke();
    }

    // ── Consumables API ─────────────────────────────────────────────────

    /// <summary>
    /// Adds a consumable to the player's owned items.
    /// If an equipped quick-slot is currently empty, automatically equips it.
    /// </summary>
    public void AddConsumable(ConsumableData consumable)
    {
        if (consumable == null) return;

        ownedConsumables.Add(consumable);

        // Auto-equip into first empty quick-slot if available
        for (int i = 0; i < consumableSlotCount; i++)
        {
            if (i >= equippedConsumables.Count)
            {
                equippedConsumables.Add(consumable);
                break;
            }
            else if (equippedConsumables[i] == null)
            {
                equippedConsumables[i] = consumable;
                break;
            }
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Equips a consumable into the specified quick-slot (0-3).
    /// </summary>
    public bool EquipConsumable(ConsumableData consumable, int slotIndex)
    {
        if (consumable == null || slotIndex < 0 || slotIndex >= consumableSlotCount)
            return false;

        while (equippedConsumables.Count < consumableSlotCount)
            equippedConsumables.Add(null);

        if (!ownedConsumables.Contains(consumable))
            ownedConsumables.Add(consumable);

        equippedConsumables[slotIndex] = consumable;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Unequips the consumable from the specified quick-slot.
    /// </summary>
    public bool UnequipConsumable(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedConsumables.Count)
            return false;

        equippedConsumables[slotIndex] = null;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Uses and consumes 1 item from the specified quick-slot.
    /// Returns true if an item was successfully consumed.
    /// </summary>
    public bool ConsumeEquippedSlot(int slotIndex, out ConsumableData consumed)
    {
        consumed = null;
        if (slotIndex < 0 || slotIndex >= equippedConsumables.Count)
            return false;

        ConsumableData item = equippedConsumables[slotIndex] as ConsumableData;
        if (item == null)
            return false;

        consumed = item;

        // Remove 1 instance from owned stash
        ownedConsumables.Remove(item);

        // If no more copies of this consumable exist in stash, clear the slot
        if (!ownedConsumables.Contains(item))
        {
            equippedConsumables[slotIndex] = null;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }
}
