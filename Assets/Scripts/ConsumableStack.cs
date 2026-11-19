using System;
using UnityEngine;

/// <summary>
/// Serializable stack representing a consumable item type and its unit count.
/// Used across Storage/Stash, Active Quick-Slots, and the Darkzone Backpack.
/// </summary>
[Serializable]
public class ConsumableStack
{
    public ConsumableData item;

    [Min(0)]
    public int quantity;

    public bool IsEmpty => item == null || quantity <= 0;
    public bool IsStackable => item != null && item.isStackable;
    public int MaxQuickSlotStack => item != null ? (item.isStackable ? Mathf.Max(1, item.maxQuickSlotStack) : 1) : 1;

    public ConsumableStack()
    {
    }

    public ConsumableStack(ConsumableData item, int quantity)
    {
        Set(item, quantity);
    }

    public void Set(ConsumableData newItem, int newQuantity)
    {
        if (newItem == null || newQuantity <= 0)
        {
            Clear();
            return;
        }

        item = newItem.isStackable ? newItem.GetTemplate() : newItem;
        quantity = newQuantity;
    }

    public bool Matches(ConsumableData other)
    {
        return !IsEmpty && other != null && item.Matches(other);
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
    }
}
