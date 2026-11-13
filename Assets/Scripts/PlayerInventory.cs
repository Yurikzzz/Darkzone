using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentSlotType
{
    Vest = 0,
    Mask = 1
}

/// <summary>
/// Manages player inventory, stash, and 2-weapon loadout.
/// Tracks equipped primary and secondary weapons, equipment slots, consumable slots,
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

    [Header("Equipped Equipment")]
    [Tooltip("Equipped tactical vest.")]
    [SerializeField] private ScriptableObject equippedVest;

    [Tooltip("Equipped protective mask.")]
    [SerializeField] private ScriptableObject equippedMask;

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
    public ScriptableObject EquippedVest => equippedVest;
    public ScriptableObject EquippedMask => equippedMask;
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

        InitializeSeparateItemInstances();
    }

    /// <summary>
    /// Converts any shared asset references assigned in the Inspector into independent runtime
    /// instances so every item in the inventory is separate and has its own independent stats.
    /// </summary>
    private void InitializeSeparateItemInstances()
    {
        // 1. Weapons: instantiate each entry in ownedWeapons as a distinct runtime instance
        WeaponData initialPrimary = primaryWeapon;
        WeaponData initialSecondary = secondaryWeapon;
        bool primaryMapped = false;
        bool secondaryMapped = false;

        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            WeaponData original = ownedWeapons[i];
            if (original == null) continue;

            WeaponData instance = EnsureRuntimeWeaponInstance(original, forceNewCopy: true);
            ownedWeapons[i] = instance;

            if (!primaryMapped && initialPrimary == original)
            {
                primaryWeapon = instance;
                primaryMapped = true;
            }
            else if (!secondaryMapped && initialSecondary == original)
            {
                secondaryWeapon = instance;
                secondaryMapped = true;
            }
        }

        if (primaryWeapon != null && !primaryMapped)
        {
            primaryWeapon = EnsureRuntimeWeaponInstance(primaryWeapon, forceNewCopy: true);
            ownedWeapons.Add(primaryWeapon);
        }

        if (secondaryWeapon != null && !secondaryMapped)
        {
            secondaryWeapon = EnsureRuntimeWeaponInstance(secondaryWeapon, forceNewCopy: true);
            ownedWeapons.Add(secondaryWeapon);
        }

        // 2. Consumables: instantiate each entry in ownedConsumables as a distinct runtime instance
        List<ScriptableObject> initialEquippedConsumables = new List<ScriptableObject>(equippedConsumables);
        bool[] equippedMapped = new bool[initialEquippedConsumables.Count];

        for (int i = 0; i < ownedConsumables.Count; i++)
        {
            ScriptableObject original = ownedConsumables[i];
            if (original == null) continue;

            ScriptableObject instance = EnsureRuntimeScriptableInstance(original, forceNewCopy: true);
            ownedConsumables[i] = instance;

            for (int s = 0; s < initialEquippedConsumables.Count; s++)
            {
                if (!equippedMapped[s] && initialEquippedConsumables[s] == original)
                {
                    equippedConsumables[s] = instance;
                    equippedMapped[s] = true;
                    break;
                }
            }
        }

        for (int s = 0; s < equippedConsumables.Count; s++)
        {
            if (equippedConsumables[s] != null && (s >= equippedMapped.Length || !equippedMapped[s]))
            {
                ScriptableObject instance = EnsureRuntimeScriptableInstance(equippedConsumables[s], forceNewCopy: true);
                equippedConsumables[s] = instance;
                ownedConsumables.Add(instance);
            }
        }

        // 3. Equipment: instantiate each entry in ownedEquipment as a distinct runtime instance
        ScriptableObject initialVest = equippedVest;
        ScriptableObject initialMask = equippedMask;
        bool vestMapped = false;
        bool maskMapped = false;

        for (int i = 0; i < ownedEquipment.Count; i++)
        {
            ScriptableObject original = ownedEquipment[i];
            if (original == null) continue;

            ScriptableObject instance = EnsureRuntimeScriptableInstance(original, forceNewCopy: true);
            ownedEquipment[i] = instance;

            if (!vestMapped && initialVest == original)
            {
                equippedVest = instance;
                vestMapped = true;
            }
            else if (!maskMapped && initialMask == original)
            {
                equippedMask = instance;
                maskMapped = true;
            }
        }

        if (equippedVest != null && !vestMapped)
        {
            equippedVest = EnsureRuntimeScriptableInstance(equippedVest, forceNewCopy: true);
            ownedEquipment.Add(equippedVest);
        }

        if (equippedMask != null && !maskMapped)
        {
            equippedMask = EnsureRuntimeScriptableInstance(equippedMask, forceNewCopy: true);
            ownedEquipment.Add(equippedMask);
        }
    }

    private static WeaponData EnsureRuntimeWeaponInstance(WeaponData weapon, bool forceNewCopy)
    {
        if (weapon == null) return null;
        if (!forceNewCopy && weapon.isRuntimeInstance) return weapon;
        return weapon.CreateRuntimeInstance();
    }

    private static ConsumableData EnsureRuntimeConsumableInstance(ConsumableData consumable, bool forceNewCopy)
    {
        if (consumable == null) return null;
        if (!forceNewCopy && consumable.isRuntimeInstance) return consumable;
        return consumable.CreateRuntimeInstance();
    }

    private static ScriptableObject EnsureRuntimeScriptableInstance(ScriptableObject item, bool forceNewCopy)
    {
        if (item == null) return null;
        if (item is ConsumableData cd)
            return EnsureRuntimeConsumableInstance(cd, forceNewCopy);
        if (item is WeaponData wd)
            return EnsureRuntimeWeaponInstance(wd, forceNewCopy);

        ScriptableObject clone = Instantiate(item);
        clone.name = item.name;
        return clone;
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

        // Ensure weapon is in owned weapons as a distinct runtime instance
        if (!ownedWeapons.Contains(weapon))
        {
            weapon = EnsureRuntimeWeaponInstance(weapon, forceNewCopy: false);
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
    /// Adds a weapon to the player's owned stash as a separate item instance with its own stats.
    /// If the secondary weapon slot is currently empty, automatically equips it.
    /// </summary>
    public void AddOwnedWeapon(WeaponData weapon)
    {
        if (weapon == null) return;

        bool alreadyInStash = ownedWeapons.Contains(weapon);
        WeaponData instance = EnsureRuntimeWeaponInstance(weapon, forceNewCopy: alreadyInStash);
        ownedWeapons.Add(instance);

        // Auto-equip into secondary slot if empty
        if (secondaryWeapon == null && primaryWeapon != instance)
        {
            EquipWeapon(instance, 1);
        }
        else
        {
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Sets the list of all owned weapons (e.g. for initial setup or loading),
    /// ensuring each entry is a separate runtime instance.
    /// </summary>
    public void SetOwnedWeapons(List<WeaponData> weapons)
    {
        ownedWeapons.Clear();
        if (weapons != null)
        {
            foreach (var w in weapons)
            {
                if (w == null) continue;
                bool duplicate = ownedWeapons.Contains(w);
                ownedWeapons.Add(EnsureRuntimeWeaponInstance(w, forceNewCopy: duplicate));
            }
        }
        OnInventoryChanged?.Invoke();
    }

    // ── Equipment API ───────────────────────────────────────────────────

    /// <summary>
    /// Checks whether a specific equipment instance is currently equipped in Vest or Mask.
    /// </summary>
    public bool IsEquipmentEquipped(ScriptableObject item, out EquipmentSlotType slotType)
    {
        if (item != null)
        {
            if (equippedVest == item)
            {
                slotType = EquipmentSlotType.Vest;
                return true;
            }
            if (equippedMask == item)
            {
                slotType = EquipmentSlotType.Mask;
                return true;
            }
        }
        slotType = EquipmentSlotType.Vest;
        return false;
    }

    /// <summary>
    /// Equips an equipment item into the specified equipment slot (Vest or Mask).
    /// Swaps slots if already equipped in the other equipment slot.
    /// </summary>
    public bool EquipEquipment(ScriptableObject item, EquipmentSlotType slotType)
    {
        if (item == null || item is WeaponData || item is ConsumableData)
            return false;

        if (!ownedEquipment.Contains(item))
        {
            item = EnsureRuntimeScriptableInstance(item, forceNewCopy: true);
            ownedEquipment.Add(item);
        }

        if (slotType == EquipmentSlotType.Vest)
        {
            if (equippedMask == item)
                equippedMask = equippedVest;
            equippedVest = item;
        }
        else
        {
            if (equippedVest == item)
                equippedVest = equippedMask;
            equippedMask = item;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Unequips the equipment item in the specified slot.
    /// </summary>
    public bool UnequipEquipment(EquipmentSlotType slotType)
    {
        if (slotType == EquipmentSlotType.Vest)
        {
            if (equippedVest == null) return false;
            equippedVest = null;
        }
        else
        {
            if (equippedMask == null) return false;
            equippedMask = null;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Adds an equipment piece to the player's owned stash as a separate item instance with its own stats.
    /// </summary>
    public void AddEquipment(ScriptableObject equipment)
    {
        AddOwnedEquipment(equipment);
    }

    /// <summary>
    /// Adds an equipment piece to the player's owned stash as a separate item instance with its own stats.
    /// </summary>
    public void AddOwnedEquipment(ScriptableObject equipment)
    {
        if (equipment == null) return;

        ScriptableObject instance = EnsureRuntimeScriptableInstance(equipment, forceNewCopy: true);
        ownedEquipment.Add(instance);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Sets the list of all owned equipment, ensuring each entry is a separate runtime instance.
    /// </summary>
    public void SetOwnedEquipment(List<ScriptableObject> equipment)
    {
        ownedEquipment.Clear();
        if (equipment != null)
        {
            foreach (var eq in equipment)
            {
                if (eq == null) continue;
                ownedEquipment.Add(EnsureRuntimeScriptableInstance(eq, forceNewCopy: true));
            }
        }
        OnInventoryChanged?.Invoke();
    }

    // ── Consumables API ─────────────────────────────────────────────────

    /// <summary>
    /// Checks whether a specific consumable instance is currently equipped in any quick-slot.
    /// </summary>
    public bool IsConsumableEquipped(ScriptableObject consumable, out int slotIndex)
    {
        if (consumable != null && equippedConsumables != null)
        {
            for (int i = 0; i < equippedConsumables.Count; i++)
            {
                if (equippedConsumables[i] == consumable)
                {
                    slotIndex = i;
                    return true;
                }
            }
        }
        slotIndex = -1;
        return false;
    }

    /// <summary>
    /// Adds a consumable to the player's owned items as a separate item instance with its own stats.
    /// If an equipped quick-slot is currently empty, automatically equips it.
    /// </summary>
    public void AddConsumable(ConsumableData consumable)
    {
        if (consumable == null) return;

        bool alreadyInStash = ownedConsumables.Contains(consumable);
        ConsumableData instance = EnsureRuntimeConsumableInstance(consumable, forceNewCopy: alreadyInStash);
        ownedConsumables.Add(instance);

        // Auto-equip into first empty quick-slot if available
        for (int i = 0; i < consumableSlotCount; i++)
        {
            if (i >= equippedConsumables.Count)
            {
                equippedConsumables.Add(instance);
                break;
            }
            else if (equippedConsumables[i] == null)
            {
                equippedConsumables[i] = instance;
                break;
            }
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Equips a specific consumable instance into the specified quick-slot (0-3).
    /// If this instance was already equipped in another quick-slot, swaps the slots.
    /// </summary>
    public bool EquipConsumable(ConsumableData consumable, int slotIndex)
    {
        if (consumable == null || slotIndex < 0 || slotIndex >= consumableSlotCount)
            return false;

        while (equippedConsumables.Count < consumableSlotCount)
            equippedConsumables.Add(null);

        if (!ownedConsumables.Contains(consumable))
        {
            consumable = EnsureRuntimeConsumableInstance(consumable, forceNewCopy: false);
            ownedConsumables.Add(consumable);
        }

        // If this exact consumable instance is already in another slot, swap it
        for (int i = 0; i < equippedConsumables.Count; i++)
        {
            if (i != slotIndex && equippedConsumables[i] == consumable)
            {
                equippedConsumables[i] = equippedConsumables[slotIndex];
                break;
            }
        }

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
    /// Uses and consumes the individual item in the specified quick-slot.
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

        // Remove this specific consumable instance from owned stash and clear its slot
        ownedConsumables.Remove(item);
        equippedConsumables[slotIndex] = null;

        OnInventoryChanged?.Invoke();
        return true;
    }
}
