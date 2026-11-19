using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks everything the player actively carries on their person:
/// - 4 quick-access consumable slots (Keys 3-6) with stack counts
/// - A UI-less Darkzone backpack for overflow consumables and looted weapons
/// Handles Darkzone pickups, consumption, evacuation transfers, and death loss.
/// </summary>
public class PlayerActiveInventory : MonoBehaviour
{
    public static PlayerActiveInventory Instance { get; private set; }

    [Header("Equipped Quick-Access Consumables (Max 4 Slots)")]
    [SerializeField] private int quickSlotCount = 4;
    [SerializeField] private List<ConsumableStack> quickSlots = new List<ConsumableStack>();

    [Header("Darkzone Backpack (Unextracted Loot - No UI Yet)")]
    [SerializeField] private List<ConsumableStack> backpackConsumables = new List<ConsumableStack>();
    [SerializeField] private List<WeaponData> backpackWeapons = new List<WeaponData>();

    public event Action OnActiveInventoryChanged;

    public int QuickSlotCount => quickSlotCount;
    public IReadOnlyList<ConsumableStack> QuickSlots => quickSlots;
    public IReadOnlyList<ConsumableStack> BackpackConsumables => backpackConsumables;
    public IReadOnlyList<WeaponData> BackpackWeapons => backpackWeapons;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        EnsureQuickSlotCapacity();
        NormalizeStacks();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void EnsureQuickSlotCapacity()
    {
        if (quickSlotCount < 1)
            quickSlotCount = 4;

        while (quickSlots.Count < quickSlotCount)
            quickSlots.Add(new ConsumableStack());

        for (int i = 0; i < quickSlots.Count; i++)
        {
            if (quickSlots[i] == null)
                quickSlots[i] = new ConsumableStack();
        }
    }

    private void NormalizeStacks()
    {
        for (int i = 0; i < quickSlots.Count; i++)
        {
            ConsumableStack slot = quickSlots[i];
            if (slot == null || slot.IsEmpty)
            {
                slot?.Clear();
                continue;
            }

            if (slot.item.isStackable)
            {
                slot.item = slot.item.GetTemplate();
            }
            else
            {
                if (!slot.item.isRuntimeInstance)
                    slot.item = slot.item.CreateRuntimeInstance();
                slot.quantity = 1;
            }
        }
    }

    public ConsumableStack GetQuickSlot(int slotIndex)
    {
        EnsureQuickSlotCapacity();
        if (slotIndex < 0 || slotIndex >= quickSlots.Count)
            return null;
        return quickSlots[slotIndex];
    }

    /// <summary>
    /// Returns the quick-slot index (0..3) currently holding a matching consumable, or -1 if none.
    /// </summary>
    public int FindEquippedSlotIndex(ConsumableData consumable)
    {
        if (consumable == null) return -1;
        EnsureQuickSlotCapacity();

        for (int i = 0; i < quickSlotCount && i < quickSlots.Count; i++)
        {
            if (quickSlots[i] != null && quickSlots[i].Matches(consumable))
                return i;
        }
        return -1;
    }

    public bool IsConsumableEquipped(ConsumableData consumable, out int slotIndex)
    {
        slotIndex = FindEquippedSlotIndex(consumable);
        return slotIndex >= 0;
    }

    public void SetQuickSlot(int slotIndex, ConsumableData item, int quantity)
    {
        SetQuickSlotInternal(slotIndex, item, quantity);
        NotifyChanged();
    }

    internal void SetQuickSlotInternal(int slotIndex, ConsumableData item, int quantity)
    {
        EnsureQuickSlotCapacity();
        if (slotIndex < 0 || slotIndex >= quickSlotCount)
            return;

        if (item == null || quantity <= 0)
        {
            quickSlots[slotIndex].Clear();
            return;
        }

        quickSlots[slotIndex].Set(item, item.isStackable ? quantity : 1);
    }

    /// <summary>
    /// Swaps or moves the contents of two quick-slots without touching the stash.
    /// </summary>
    public bool SwapOrMoveQuickSlots(int fromSlot, int toSlot)
    {
        EnsureQuickSlotCapacity();
        if (fromSlot < 0 || fromSlot >= quickSlotCount || toSlot < 0 || toSlot >= quickSlotCount || fromSlot == toSlot)
            return false;

        ConsumableStack src = quickSlots[fromSlot];
        ConsumableStack dst = quickSlots[toSlot];

        ConsumableData srcItem = src.item;
        int srcQty = src.quantity;

        src.Set(dst.item, dst.quantity);
        dst.Set(srcItem, srcQty);

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Consumes 1 unit from the specified quick-slot and clears it if depleted.
    /// </summary>
    public bool ConsumeQuickSlot(int slotIndex, out ConsumableData consumed)
    {
        consumed = null;
        EnsureQuickSlotCapacity();
        if (slotIndex < 0 || slotIndex >= quickSlotCount)
            return false;

        ConsumableStack slot = quickSlots[slotIndex];
        if (slot == null || slot.IsEmpty)
            return false;

        consumed = slot.item;
        slot.quantity--;
        if (slot.quantity <= 0)
            slot.Clear();

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Handles picking up a consumable in the Darkzone:
    /// 1. If stackable and already equipped in a quick-slot, adds directly to that slot (uncapped above 5).
    /// 2. Else if an empty quick-slot exists, equips it there (uncapped if stackable).
    /// 3. Else places it into the Darkzone backpack overflow.
    /// </summary>
    public void AddLootedConsumable(ConsumableData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;
        EnsureQuickSlotCapacity();

        if (item.isStackable)
        {
            ConsumableData canonical = item.GetTemplate();

            int existingSlot = FindEquippedSlotIndex(canonical);
            if (existingSlot >= 0)
            {
                quickSlots[existingSlot].quantity += amount;
                NotifyChanged();
                return;
            }

            for (int i = 0; i < quickSlotCount; i++)
            {
                if (quickSlots[i].IsEmpty)
                {
                    quickSlots[i].Set(canonical, amount);
                    NotifyChanged();
                    return;
                }
            }

            AddToBackpackInternal(canonical, amount);
            NotifyChanged();
        }
        else
        {
            for (int u = 0; u < amount; u++)
            {
                ConsumableData instance = (u == 0 && item.isRuntimeInstance)
                    ? item
                    : item.CreateRuntimeInstance();

                bool placedInQuickSlot = false;
                for (int i = 0; i < quickSlotCount; i++)
                {
                    if (quickSlots[i].IsEmpty)
                    {
                        quickSlots[i].Set(instance, 1);
                        placedInQuickSlot = true;
                        break;
                    }
                }

                if (!placedInQuickSlot)
                    AddToBackpackInternal(instance, 1);
            }

            NotifyChanged();
        }
    }

    public void AddToBackpack(ConsumableData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;
        AddToBackpackInternal(item, amount);
        NotifyChanged();
    }

    private void AddToBackpackInternal(ConsumableData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (item.isStackable)
        {
            ConsumableData canonical = item.GetTemplate();
            for (int i = 0; i < backpackConsumables.Count; i++)
            {
                if (backpackConsumables[i] != null && backpackConsumables[i].Matches(canonical))
                {
                    backpackConsumables[i].quantity += amount;
                    return;
                }
            }
            backpackConsumables.Add(new ConsumableStack(canonical, amount));
        }
        else
        {
            for (int u = 0; u < amount; u++)
            {
                ConsumableData instance = (u == 0 && item.isRuntimeInstance)
                    ? item
                    : item.CreateRuntimeInstance();
                backpackConsumables.Add(new ConsumableStack(instance, 1));
            }
        }
    }

    public void AddWeaponToBackpack(WeaponData weapon)
    {
        if (weapon == null) return;
        WeaponData instance = weapon.isRuntimeInstance ? weapon : weapon.CreateRuntimeInstance();
        backpackWeapons.Add(instance);
        NotifyChanged();
    }

    internal void ClearBackpackInternal()
    {
        backpackConsumables.Clear();
        backpackWeapons.Clear();
    }

    /// <summary>
    /// Clears all carried items on player death: backpack, all equipped quick-slot consumables.
    /// </summary>
    public void ClearAllOnDeath()
    {
        EnsureQuickSlotCapacity();
        for (int i = 0; i < quickSlots.Count; i++)
        {
            quickSlots[i]?.Clear();
        }

        backpackConsumables.Clear();
        backpackWeapons.Clear();
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        OnActiveInventoryChanged?.Invoke();
    }
}
