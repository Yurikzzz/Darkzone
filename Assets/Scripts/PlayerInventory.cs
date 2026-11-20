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

    [Tooltip("Fallback starter pistol granted on respawn if no pistol remains in stash.")]
    [SerializeField] private WeaponData defaultPistol;

    [Header("Equipped Equipment")]
    [Tooltip("Equipped tactical vest.")]
    [SerializeField] private ScriptableObject equippedVest;

    [Tooltip("Equipped protective mask.")]
    [SerializeField] private ScriptableObject equippedMask;

    [Header("Consumables Configuration")]
    [Tooltip("Number of quick-access consumable slots.")]
    [SerializeField] private int consumableSlotCount = 4;

    [Header("Owned Items / Stash")]
    [Tooltip("List of all weapons owned by the player.")]
    [SerializeField] private List<WeaponData> ownedWeapons = new List<WeaponData>();

    [Tooltip("List of all equipment pieces owned by the player (coming soon).")]
    [SerializeField] private List<ScriptableObject> ownedEquipment = new List<ScriptableObject>();

    [Tooltip("Stacked consumables stored safely in the player's stash.")]
    [SerializeField] private List<ConsumableStack> stashedConsumables = new List<ConsumableStack>();

    // Legacy serialized fields kept hidden for automatic scene-data migration.
    [SerializeField, HideInInspector] private List<ScriptableObject> equippedConsumables = new List<ScriptableObject>();
    [SerializeField, HideInInspector] private List<ScriptableObject> ownedConsumables = new List<ScriptableObject>();

    [Header("Currency")]
    [Tooltip("Coins collected by the player.")]
    [Min(0)]
    [SerializeField] private int coins = 0;

    private WeaponController weaponController;
    private PlayerActiveInventory activeInventory;
    private WeaponData cachedPistolTemplate;

    public event Action OnInventoryChanged;
    public event Action<int> OnCoinsChanged;

    public WeaponData PrimaryWeapon => primaryWeapon;
    public WeaponData SecondaryWeapon => secondaryWeapon;
    public ScriptableObject EquippedVest => equippedVest;
    public ScriptableObject EquippedMask => equippedMask;
    public int ConsumableSlotCount => consumableSlotCount;
    public PlayerActiveInventory ActiveInventory => activeInventory;
    public IReadOnlyList<ConsumableStack> StashedConsumables => stashedConsumables;
    public IReadOnlyList<ConsumableStack> EquippedConsumableStacks => activeInventory != null ? activeInventory.QuickSlots : null;
    public IReadOnlyList<WeaponData> OwnedWeapons => ownedWeapons;
    public IReadOnlyList<ScriptableObject> OwnedEquipment => ownedEquipment;
    public int Coins => coins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        weaponController = GetComponent<WeaponController>() ?? GetComponentInChildren<WeaponController>();
        activeInventory = GetComponent<PlayerActiveInventory>() ?? gameObject.AddComponent<PlayerActiveInventory>();
        activeInventory.OnActiveInventoryChanged += HandleActiveInventoryChanged;

        CacheDefaultPistolTemplate();
        MigrateLegacyConsumables();
        InitializeSeparateItemInstances();
    }

    private void CacheDefaultPistolTemplate()
    {
        if (defaultPistol != null)
        {
            cachedPistolTemplate = defaultPistol;
            return;
        }

        if (primaryWeapon != null && !primaryWeapon.isRuntimeInstance && primaryWeapon.name.ToLower().Contains("pistol"))
        {
            cachedPistolTemplate = primaryWeapon;
            return;
        }

        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            WeaponData w = ownedWeapons[i];
            if (w != null && !w.isRuntimeInstance && w.name.ToLower().Contains("pistol"))
            {
                cachedPistolTemplate = w;
                return;
            }
        }
    }

    private WeaponData ResolveDefaultPistolTemplate()
    {
        if (defaultPistol != null) return defaultPistol;
        if (cachedPistolTemplate != null) return cachedPistolTemplate;

        WeaponData[] allWeapons = Resources.FindObjectsOfTypeAll<WeaponData>();
        for (int i = 0; i < allWeapons.Length; i++)
        {
            WeaponData w = allWeapons[i];
            if (w != null && !w.isRuntimeInstance && w.name.ToLower().Contains("pistol"))
            {
                cachedPistolTemplate = w;
                return w;
            }
        }
        return null;
    }

    private void OnDestroy()
    {
        if (activeInventory != null)
            activeInventory.OnActiveInventoryChanged -= HandleActiveInventoryChanged;

        if (Instance == this)
            Instance = null;
    }

    private void HandleActiveInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void MigrateLegacyConsumables()
    {
        if (stashedConsumables.Count == 0 && ownedConsumables != null && ownedConsumables.Count > 0)
        {
            for (int i = 0; i < ownedConsumables.Count; i++)
            {
                if (ownedConsumables[i] is ConsumableData cd)
                    AddConsumableToStashInternal(cd, 1);
            }
            ownedConsumables.Clear();
        }

        if (equippedConsumables != null && equippedConsumables.Count > 0 && activeInventory != null)
        {
            activeInventory.EnsureQuickSlotCapacity();
            for (int i = 0; i < equippedConsumables.Count && i < consumableSlotCount; i++)
            {
                if (equippedConsumables[i] is ConsumableData cd)
                {
                    ConsumableStack slot = activeInventory.GetQuickSlot(i);
                    if (slot != null && slot.IsEmpty)
                    {
                        if (GetStashedConsumableCount(cd) > 0)
                            RemoveConsumableFromStashInternal(cd, 1);
                        activeInventory.SetQuickSlotInternal(i, cd, 1);
                    }
                }
            }
            equippedConsumables.Clear();
        }
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

        // 2. Consumables: normalize stashed stacks (merge stackables, isolate non-stackables)
        NormalizeStashedConsumables();

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
    /// Handles a weapon looted inside the Darkzone:
    /// Equips into an empty Primary/Secondary hand slot if available,
    /// otherwise stores it in the player's Darkzone backpack (not sent to stash until evac).
    /// </summary>
    public void AddLootedWeapon(WeaponData weapon)
    {
        if (weapon == null) return;

        WeaponData instance = EnsureRuntimeWeaponInstance(weapon, forceNewCopy: true);

        if (primaryWeapon == null)
        {
            primaryWeapon = instance;
            SyncWithWeaponController();
            OnInventoryChanged?.Invoke();
        }
        else if (secondaryWeapon == null && primaryWeapon != instance)
        {
            secondaryWeapon = instance;
            SyncWithWeaponController();
            OnInventoryChanged?.Invoke();
        }
        else if (activeInventory != null)
        {
            activeInventory.AddWeaponToBackpack(instance);
        }
        else
        {
            AddOwnedWeapon(instance);
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

    // ── Consumables API (Stash + Active Quick-Slot Transfers) ───────────

    private void NormalizeStashedConsumables()
    {
        List<ConsumableStack> normalized = new List<ConsumableStack>();
        for (int i = 0; i < stashedConsumables.Count; i++)
        {
            ConsumableStack entry = stashedConsumables[i];
            if (entry == null || entry.IsEmpty) continue;

            if (entry.item.isStackable)
            {
                ConsumableData template = entry.item.GetTemplate();
                bool merged = false;
                for (int j = 0; j < normalized.Count; j++)
                {
                    if (normalized[j].Matches(template))
                    {
                        normalized[j].quantity += entry.quantity;
                        merged = true;
                        break;
                    }
                }
                if (!merged)
                    normalized.Add(new ConsumableStack(template, entry.quantity));
            }
            else
            {
                int count = Mathf.Max(1, entry.quantity);
                for (int u = 0; u < count; u++)
                {
                    bool alreadyUsed = normalized.Exists(x => x.item == entry.item);
                    ConsumableData instance = EnsureRuntimeConsumableInstance(entry.item, forceNewCopy: u > 0 || alreadyUsed);
                    normalized.Add(new ConsumableStack(instance, 1));
                }
            }
        }

        stashedConsumables.Clear();
        stashedConsumables.AddRange(normalized);
    }

    /// <summary>
    /// Checks whether a consumable is currently equipped in any active quick-slot.
    /// </summary>
    public bool IsConsumableEquipped(ConsumableData consumable, out int slotIndex)
    {
        if (activeInventory != null)
            return activeInventory.IsConsumableEquipped(consumable, out slotIndex);

        slotIndex = -1;
        return false;
    }

    /// <summary>
    /// Returns the total quantity of matching consumable currently stored in the Stash.
    /// </summary>
    public int GetStashedConsumableCount(ConsumableData consumable)
    {
        if (consumable == null) return 0;

        int total = 0;
        for (int i = 0; i < stashedConsumables.Count; i++)
        {
            ConsumableStack stack = stashedConsumables[i];
            if (stack != null && !stack.IsEmpty && stack.Matches(consumable))
                total += stack.quantity;
        }
        return total;
    }

    /// <summary>
    /// Adds a consumable stack directly to the player's safe Stash.
    /// </summary>
    public void AddConsumableToStash(ConsumableData consumable, int amount = 1)
    {
        if (consumable == null || amount <= 0) return;
        AddConsumableToStashInternal(consumable, amount);
        OnInventoryChanged?.Invoke();
    }

    private void AddConsumableToStashInternal(ConsumableData consumable, int amount)
    {
        if (consumable == null || amount <= 0) return;

        if (consumable.isStackable)
        {
            ConsumableData template = consumable.GetTemplate();
            for (int i = 0; i < stashedConsumables.Count; i++)
            {
                ConsumableStack stack = stashedConsumables[i];
                if (stack != null && !stack.IsEmpty && stack.Matches(template))
                {
                    stack.quantity += amount;
                    return;
                }
            }
            stashedConsumables.Add(new ConsumableStack(template, amount));
        }
        else
        {
            for (int i = 0; i < amount; i++)
            {
                ConsumableData instance = (i == 0 && consumable.isRuntimeInstance)
                    ? consumable
                    : EnsureRuntimeConsumableInstance(consumable, forceNewCopy: true);
                stashedConsumables.Add(new ConsumableStack(instance, 1));
            }
        }
    }

    private int RemoveConsumableFromStashInternal(ConsumableData consumable, int maxToRemove)
    {
        if (consumable == null || maxToRemove <= 0) return 0;

        int remainingToRemove = maxToRemove;
        int totalRemoved = 0;

        for (int i = stashedConsumables.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            ConsumableStack stack = stashedConsumables[i];
            if (stack == null || stack.IsEmpty || !stack.Matches(consumable))
                continue;

            int take = Mathf.Min(stack.quantity, remainingToRemove);
            stack.quantity -= take;
            remainingToRemove -= take;
            totalRemoved += take;

            if (stack.quantity <= 0)
                stashedConsumables.RemoveAt(i);
        }

        return totalRemoved;
    }

    /// <summary>
    /// Handles a consumable picked up inside the Darkzone:
    /// Routes to PlayerActiveInventory (adds to equipped quick-slot even above 5, or empty slot, or backpack).
    /// </summary>
    public void AddConsumable(ConsumableData consumable, int amount = 1)
    {
        if (consumable == null || amount <= 0) return;

        if (activeInventory != null)
        {
            activeInventory.AddLootedConsumable(consumable, amount);
        }
        else
        {
            AddConsumableToStash(consumable, amount);
        }
    }

    /// <summary>
    /// Equips a consumable from the Stash into the specified quick-slot (0-3).
    /// Enforces single-slot policy: if already equipped in another slot, moves/swaps it there
    /// and tops up up to maxQuickSlotStack (5) from Stash.
    /// </summary>
    public bool EquipConsumable(ConsumableData consumable, int slotIndex)
    {
        if (consumable == null || activeInventory == null || slotIndex < 0 || slotIndex >= consumableSlotCount)
            return false;

        activeInventory.EnsureQuickSlotCapacity();

        int existingSlot = activeInventory.FindEquippedSlotIndex(consumable);
        if (existingSlot >= 0)
        {
            if (existingSlot != slotIndex)
            {
                ConsumableStack src = activeInventory.GetQuickSlot(existingSlot);
                ConsumableStack dst = activeInventory.GetQuickSlot(slotIndex);
                ConsumableData dstItem = dst.item;
                int dstQty = dst.quantity;
                dst.Set(src.item, src.quantity);
                src.Set(dstItem, dstQty);
            }

            if (consumable.isStackable)
            {
                ConsumableStack target = activeInventory.GetQuickSlot(slotIndex);
                int room = Mathf.Max(0, target.MaxQuickSlotStack - target.quantity);
                int avail = GetStashedConsumableCount(consumable);
                int take = Mathf.Min(room, avail);
                if (take > 0)
                {
                    int removed = RemoveConsumableFromStashInternal(consumable, take);
                    target.quantity += removed;
                }
            }

            activeInventory.NotifyChanged();
            return true;
        }

        int stashAvail = GetStashedConsumableCount(consumable);
        if (stashAvail <= 0)
            return false;

        ConsumableStack currentInTarget = activeInventory.GetQuickSlot(slotIndex);
        if (currentInTarget != null && !currentInTarget.IsEmpty)
        {
            AddConsumableToStashInternal(currentInTarget.item, currentInTarget.quantity);
            currentInTarget.Clear();
        }

        int maxPerSlot = consumable.isStackable ? Mathf.Max(1, consumable.maxQuickSlotStack) : 1;
        int toEquip = Mathf.Min(stashAvail, maxPerSlot);
        int taken = RemoveConsumableFromStashInternal(consumable, toEquip);
        if (taken <= 0)
            return false;

        activeInventory.SetQuickSlotInternal(slotIndex, consumable, taken);
        activeInventory.NotifyChanged();
        return true;
    }

    /// <summary>
    /// Swaps or moves two quick-slots in the player's active inventory.
    /// </summary>
    public bool SwapConsumableSlots(int fromSlot, int toSlot)
    {
        if (activeInventory == null) return false;
        return activeInventory.SwapOrMoveQuickSlots(fromSlot, toSlot);
    }

    /// <summary>
    /// Adjusts a stackable equipped quick-slot's quantity by <paramref name="delta"/> (+1 / -1),
    /// transferring units between Stash and Active Inventory within [0 .. maxQuickSlotStack].
    /// </summary>
    public bool AdjustConsumableSlotQuantity(int slotIndex, int delta)
    {
        if (activeInventory == null || delta == 0) return false;

        ConsumableStack slot = activeInventory.GetQuickSlot(slotIndex);
        if (slot == null || slot.IsEmpty || !slot.IsStackable)
            return false;

        if (delta > 0)
        {
            int room = Mathf.Max(0, slot.MaxQuickSlotStack - slot.quantity);
            int avail = GetStashedConsumableCount(slot.item);
            int move = Mathf.Min(delta, room, avail);
            if (move <= 0) return false;

            int removed = RemoveConsumableFromStashInternal(slot.item, move);
            if (removed <= 0) return false;

            slot.quantity += removed;
            activeInventory.NotifyChanged();
            return true;
        }
        else
        {
            int move = Mathf.Min(-delta, slot.quantity);
            if (move <= 0) return false;

            ConsumableData item = slot.item;
            slot.quantity -= move;
            if (slot.quantity <= 0)
                slot.Clear();

            AddConsumableToStashInternal(item, move);
            activeInventory.NotifyChanged();
            return true;
        }
    }

    /// <summary>
    /// Unequips the entire stack from the specified quick-slot and returns it to the Stash.
    /// </summary>
    public bool UnequipConsumable(int slotIndex)
    {
        if (activeInventory == null) return false;

        ConsumableStack slot = activeInventory.GetQuickSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return false;

        ConsumableData item = slot.item;
        int qty = slot.quantity;
        slot.Clear();

        AddConsumableToStashInternal(item, qty);
        activeInventory.NotifyChanged();
        return true;
    }

    /// <summary>
    /// Uses and consumes 1 unit from the specified quick-slot.
    /// Returns true if an item was successfully consumed.
    /// </summary>
    public bool ConsumeEquippedSlot(int slotIndex, out ConsumableData consumed)
    {
        consumed = null;
        if (activeInventory == null) return false;
        return activeInventory.ConsumeQuickSlot(slotIndex, out consumed);
    }

    // ── Darkzone Evacuation & Death Lifecycle ───────────────────────────

    /// <summary>
    /// Called when the player successfully evacuates the Darkzone:
    /// - Excess consumables above maxQuickSlotStack (5) in quick-slots are moved to Stash.
    /// - All backpack consumables and backpack weapons are moved to Stash.
    /// - Held weapons picked up in the zone are registered into ownedWeapons.
    /// </summary>
    public void EvacuateFromZone()
    {
        if (activeInventory == null) return;

        activeInventory.EnsureQuickSlotCapacity();

        // 1. Cap quick-slots back down to maxQuickSlotStack and send excess to Stash
        var quickSlots = activeInventory.QuickSlots;
        for (int i = 0; i < quickSlots.Count; i++)
        {
            ConsumableStack slot = quickSlots[i];
            if (slot == null || slot.IsEmpty || !slot.IsStackable)
                continue;

            int cap = slot.MaxQuickSlotStack;
            if (slot.quantity > cap)
            {
                int excess = slot.quantity - cap;
                slot.quantity = cap;
                AddConsumableToStashInternal(slot.item, excess);
            }
        }

        // 2. Deposit all backpack consumables to Stash
        var bagConsumables = activeInventory.BackpackConsumables;
        for (int i = 0; i < bagConsumables.Count; i++)
        {
            ConsumableStack bagStack = bagConsumables[i];
            if (bagStack != null && !bagStack.IsEmpty)
                AddConsumableToStashInternal(bagStack.item, bagStack.quantity);
        }

        // 3. Register held weapons and deposit backpack weapons to Stash
        if (primaryWeapon != null && !ownedWeapons.Contains(primaryWeapon))
            ownedWeapons.Add(primaryWeapon);

        if (secondaryWeapon != null && !ownedWeapons.Contains(secondaryWeapon))
            ownedWeapons.Add(secondaryWeapon);

        var bagWeapons = activeInventory.BackpackWeapons;
        for (int i = 0; i < bagWeapons.Count; i++)
        {
            WeaponData w = bagWeapons[i];
            if (w != null && !ownedWeapons.Contains(w))
                ownedWeapons.Add(w);
        }

        activeInventory.ClearBackpackInternal();
        activeInventory.NotifyChanged();
    }

    /// <summary>
    /// Called on player death:
    /// Loses the entire backpack, all held weapons (Primary & Secondary), and all equipped consumables.
    /// Grants/equips a pistol so the player always respawns with at least a pistol.
    /// Items kept safely in Stash remain untouched.
    /// </summary>
    public void HandlePlayerDeath()
    {
        if (primaryWeapon != null)
        {
            ownedWeapons.Remove(primaryWeapon);
            primaryWeapon = null;
        }

        if (secondaryWeapon != null)
        {
            ownedWeapons.Remove(secondaryWeapon);
            secondaryWeapon = null;
        }

        // Ensure player respawns with at least a pistol
        WeaponData starterPistol = ownedWeapons.Find(w => w != null && w.name.ToLower().Contains("pistol"));
        if (starterPistol == null)
        {
            WeaponData template = ResolveDefaultPistolTemplate();
            if (template != null)
            {
                starterPistol = EnsureRuntimeWeaponInstance(template, forceNewCopy: true);
                ownedWeapons.Add(starterPistol);
            }
        }
        primaryWeapon = starterPistol;

        SyncWithWeaponController();

        if (activeInventory != null)
            activeInventory.ClearAllOnDeath();
        else
            OnInventoryChanged?.Invoke();
    }

    // ── Shop Purchase API ───────────────────────────────────────────────

    /// <summary>
    /// Grants a purchased item of any supported type into an empty equipped slot if available,
    /// otherwise stores it safely in the player's Stash.
    /// </summary>
    public void AddPurchasedItem(ScriptableObject item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return;

        if (item is WeaponData weapon)
        {
            AddPurchasedWeapon(weapon, quantity);
        }
        else if (item is ConsumableData consumable)
        {
            AddPurchasedConsumable(consumable, quantity);
        }
        else
        {
            AddPurchasedEquipment(item, quantity);
        }
    }

    /// <summary>
    /// Grants purchased weapon(s). Equips into empty Primary or Secondary slots if available;
    /// any additional weapons remain in the owned stash.
    /// </summary>
    public void AddPurchasedWeapon(WeaponData weapon, int quantity = 1)
    {
        if (weapon == null || quantity <= 0) return;

        bool equippedChanged = false;
        for (int i = 0; i < quantity; i++)
        {
            WeaponData instance = EnsureRuntimeWeaponInstance(weapon, forceNewCopy: true);
            ownedWeapons.Add(instance);

            if (primaryWeapon == null)
            {
                primaryWeapon = instance;
                equippedChanged = true;
            }
            else if (secondaryWeapon == null)
            {
                secondaryWeapon = instance;
                equippedChanged = true;
            }
        }

        if (equippedChanged)
            SyncWithWeaponController();

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Grants purchased consumable(s). Equips into a matching quick-slot (up to maxQuickSlotStack)
    /// or first empty quick-slot if available; any overflow goes to Stash.
    /// </summary>
    public void AddPurchasedConsumable(ConsumableData consumable, int quantity = 1)
    {
        if (consumable == null || quantity <= 0) return;

        if (activeInventory == null)
        {
            AddConsumableToStash(consumable, quantity);
            return;
        }

        activeInventory.EnsureQuickSlotCapacity();

        if (consumable.isStackable)
        {
            int remaining = quantity;
            int existingSlot = activeInventory.FindEquippedSlotIndex(consumable);
            if (existingSlot >= 0)
            {
                ConsumableStack slot = activeInventory.GetQuickSlot(existingSlot);
                if (slot != null)
                {
                    int room = Mathf.Max(0, slot.MaxQuickSlotStack - slot.quantity);
                    int toAdd = Mathf.Min(remaining, room);
                    if (toAdd > 0)
                    {
                        slot.quantity += toAdd;
                        remaining -= toAdd;
                    }
                }
            }
            else
            {
                for (int i = 0; i < consumableSlotCount && i < activeInventory.QuickSlots.Count; i++)
                {
                    ConsumableStack slot = activeInventory.GetQuickSlot(i);
                    if (slot != null && slot.IsEmpty)
                    {
                        int maxPerSlot = Mathf.Max(1, consumable.maxQuickSlotStack);
                        int toEquip = Mathf.Min(remaining, maxPerSlot);
                        activeInventory.SetQuickSlotInternal(i, consumable, toEquip);
                        remaining -= toEquip;
                        break;
                    }
                }
            }

            if (remaining > 0)
                AddConsumableToStashInternal(consumable, remaining);
        }
        else
        {
            for (int u = 0; u < quantity; u++)
            {
                ConsumableData instance = EnsureRuntimeConsumableInstance(consumable, forceNewCopy: true);
                bool placedInQuickSlot = false;
                for (int i = 0; i < consumableSlotCount && i < activeInventory.QuickSlots.Count; i++)
                {
                    ConsumableStack slot = activeInventory.GetQuickSlot(i);
                    if (slot != null && slot.IsEmpty)
                    {
                        activeInventory.SetQuickSlotInternal(i, instance, 1);
                        placedInQuickSlot = true;
                        break;
                    }
                }

                if (!placedInQuickSlot)
                    AddConsumableToStashInternal(instance, 1);
            }
        }

        activeInventory.NotifyChanged();
    }

    /// <summary>
    /// Grants purchased equipment piece(s). Equips into empty Vest/Mask slots if available;
    /// otherwise stores in ownedEquipment stash.
    /// </summary>
    public void AddPurchasedEquipment(ScriptableObject equipment, int quantity = 1)
    {
        if (equipment == null || quantity <= 0 || equipment is WeaponData || equipment is ConsumableData)
            return;

        for (int i = 0; i < quantity; i++)
        {
            ScriptableObject instance = EnsureRuntimeScriptableInstance(equipment, forceNewCopy: true);
            ownedEquipment.Add(instance);

            if (equippedVest == null)
                equippedVest = instance;
            else if (equippedMask == null)
                equippedMask = instance;
        }

        OnInventoryChanged?.Invoke();
    }

    // ── Item Removal API ────────────────────────────────────────────────

    /// <summary>
    /// Permanently removes an item of any supported type (weapon, equipment, or consumable)
    /// from the player's stash, clearing any equipped slot that holds it.
    /// </summary>
    public bool RemoveItem(ScriptableObject item)
    {
        if (item == null) return false;
        if (item is WeaponData weapon) return RemoveWeapon(weapon);
        if (item is ConsumableData consumable) return RemoveConsumable(consumable);
        return RemoveEquipment(item);
    }

    /// <summary>
    /// Removes a weapon from ownedWeapons and unequips it if currently equipped.
    /// </summary>
    public bool RemoveWeapon(WeaponData weapon)
    {
        if (weapon == null || !ownedWeapons.Contains(weapon))
            return false;

        bool equippedChanged = false;
        if (primaryWeapon == weapon)
        {
            primaryWeapon = secondaryWeapon;
            secondaryWeapon = null;
            equippedChanged = true;
        }
        else if (secondaryWeapon == weapon)
        {
            secondaryWeapon = null;
            equippedChanged = true;
        }

        ownedWeapons.Remove(weapon);

        if (equippedChanged)
            SyncWithWeaponController();

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes an equipment item from ownedEquipment and clears its slot if equipped.
    /// </summary>
    public bool RemoveEquipment(ScriptableObject equipment)
    {
        if (equipment == null || !ownedEquipment.Contains(equipment))
            return false;

        if (equippedVest == equipment)
            equippedVest = null;
        if (equippedMask == equipment)
            equippedMask = null;

        ownedEquipment.Remove(equipment);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes a specific stashed consumable stack entry from the Stash.
    /// </summary>
    public bool RemoveStashedConsumableStack(ConsumableStack stack)
    {
        if (stack == null || !stashedConsumables.Remove(stack))
            return false;

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes all matching consumables from the Stash (and clears any matching quick-slot).
    /// </summary>
    public bool RemoveConsumable(ConsumableData consumable)
    {
        if (consumable == null) return false;

        bool removedAny = RemoveConsumableFromStashInternal(consumable, int.MaxValue) > 0;

        if (activeInventory != null)
        {
            int slotIndex = activeInventory.FindEquippedSlotIndex(consumable);
            if (slotIndex >= 0)
            {
                activeInventory.SetQuickSlotInternal(slotIndex, null, 0);
                removedAny = true;
            }
        }

        if (removedAny)
            activeInventory?.NotifyChanged();

        return removedAny;
    }

    /// <summary>
    /// Grants coins to the player and notifies listeners.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        coins += amount;
        OnCoinsChanged?.Invoke(coins);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Attempts to spend the specified number of coins. Returns true if the player had enough.
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || coins < amount) return false;

        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        OnInventoryChanged?.Invoke();
        return true;
    }
}
