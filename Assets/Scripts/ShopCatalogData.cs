using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum ShopCategory
{
    Weapons = 0,
    Equipment = 1,
    Consumables = 2
}

/// <summary>
/// Per-category randomization rules for rolling a ShopCatalogData refresh.
/// </summary>
[Serializable]
public class ShopCategoryRollConfig
{
    public ShopCategory category = ShopCategory.Weapons;

    [Min(0)]
    public int minListings = 3;

    [Min(0)]
    public int maxListings = 4;

    [Tooltip("If false, each rolled listing in this category is chosen at most once per refresh.")]
    public bool allowDuplicates = false;
}

/// <summary>
/// Configurable pool entry in a ShopCatalogData asset.
/// </summary>
[Serializable]
public class ShopPoolEntry
{
    [Tooltip("WeaponData, ConsumableData, or Equipment ScriptableObject sold by this listing.")]
    public ScriptableObject itemAsset;

    public ShopCategory category = ShopCategory.Weapons;

    [Min(0)]
    public int basePrice = 250;

    [Tooltip("Random +/- price variance percentage applied on refresh (0 = fixed price).")]
    [Range(0f, 0.5f)]
    public float priceVariance = 0f;

    [Tooltip("Relative weight when selecting random listings for this category.")]
    [Min(0f)]
    public float spawnWeight = 1f;

    [Tooltip("If true, this item is guaranteed to appear on every refresh (while room remains in category limits).")]
    public bool alwaysIncluded = false;

    [Min(0)]
    public int minStock = 1;

    [Min(0)]
    public int maxStock = 2;

    [Tooltip("If true, this item has unlimited stock (-1) and never sells out.")]
    public bool unlimitedStock = false;

    [Header("Optional Display Overrides")]
    public string categoryTagOverride;
    public string metaOverride;
    public Sprite iconOverride;
}

/// <summary>
/// Runtime instance of a rolled shop listing owned by a ShopStation.
/// Keeps stock and rolled price decoupled from ScriptableObject assets.
/// </summary>
[Serializable]
public class ShopListingRuntime
{
    public ShopPoolEntry sourceEntry;
    public ScriptableObject itemAsset;
    public ShopCategory category;
    public int price;
    public int initialStock;
    public int remainingStock;

    public bool IsUnlimited => remainingStock < 0;
    public bool IsSoldOut => remainingStock == 0;

    public string GetDisplayName()
    {
        if (itemAsset is WeaponData weapon)
            return (string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.name : weapon.weaponName).ToUpperInvariant();

        if (itemAsset is ConsumableData consumable)
            return (string.IsNullOrWhiteSpace(consumable.consumableName) ? consumable.name : consumable.consumableName).ToUpperInvariant();

        return itemAsset != null ? itemAsset.name.ToUpperInvariant() : "UNKNOWN ITEM";
    }

    public string GetCategoryTag()
    {
        if (sourceEntry != null && !string.IsNullOrWhiteSpace(sourceEntry.categoryTagOverride))
            return sourceEntry.categoryTagOverride.Trim().ToUpperInvariant();

        if (itemAsset is WeaponData weapon)
        {
            string mode = weapon.isAutomatic ? "AUTO" : "SEMI";
            string cal = string.IsNullOrWhiteSpace(weapon.caliber) ? "STD" : weapon.caliber.Trim().ToUpperInvariant();
            return $"{mode} // {cal}";
        }

        if (itemAsset is ConsumableData consumable)
        {
            switch (consumable.type)
            {
                case ConsumableType.Healing:
                    return $"MEDICAL // +{consumable.healAmount.ToString("0", CultureInfo.InvariantCulture)} HP";
                case ConsumableType.Grenade:
                    return "ORDNANCE // HE";
                default:
                    return "SUPPLY // ITEM";
            }
        }

        return "GEAR // SPEC";
    }

    public string GetMetaText()
    {
        if (sourceEntry != null && !string.IsNullOrWhiteSpace(sourceEntry.metaOverride))
            return sourceEntry.metaOverride.Trim().ToUpperInvariant();

        if (itemAsset is WeaponData weapon)
        {
            string dmgPart = weapon.bulletsPerShot > 1
                ? $"DMG {weapon.damage.ToString("0", CultureInfo.InvariantCulture)}x{weapon.bulletsPerShot}"
                : $"DMG {weapon.damage.ToString("0", CultureInfo.InvariantCulture)}";
            return $"{dmgPart}  •  MAG {weapon.magazineSize}";
        }

        if (itemAsset is ConsumableData consumable)
        {
            switch (consumable.type)
            {
                case ConsumableType.Healing:
                    return $"CD {consumable.cooldown.ToString("0.0", CultureInfo.InvariantCulture)}S  •  MAX {consumable.maxQuickSlotStack}";
                case ConsumableType.Grenade:
                    return $"DMG {consumable.grenadeDamage.ToString("0", CultureInfo.InvariantCulture)}  •  {consumable.explosionRadius.ToString("0.0", CultureInfo.InvariantCulture)}M BLAST";
                default:
                    return $"CD {consumable.cooldown.ToString("0.0", CultureInfo.InvariantCulture)}S";
            }
        }

        return "TACTICAL GEAR";
    }

    public Sprite GetIconSprite(out Color tint)
    {
        if (sourceEntry != null && sourceEntry.iconOverride != null)
        {
            tint = Color.white;
            return sourceEntry.iconOverride;
        }

        if (itemAsset is WeaponData weapon && weapon.weaponSprite != null)
        {
            tint = Color.white;
            return weapon.weaponSprite;
        }

        if (itemAsset is ConsumableData consumable && consumable.icon != null)
        {
            tint = consumable.tintColor;
            return consumable.icon;
        }

        tint = new Color(138f / 255f, 145f / 255f, 154f / 255f, 1f);
        return null;
    }

    public string GetInspectDetails()
    {
        if (itemAsset is WeaponData weapon)
        {
            string mode = weapon.isAutomatic ? "AUTOMATIC" : "SEMI-AUTO";
            string cal = string.IsNullOrWhiteSpace(weapon.caliber) ? "STANDARD" : weapon.caliber.ToUpperInvariant();
            string dmg = weapon.bulletsPerShot > 1
                ? $"{weapon.damage.ToString("0.#", CultureInfo.InvariantCulture)} x {weapon.bulletsPerShot} PELLETS"
                : weapon.damage.ToString("0.#", CultureInfo.InvariantCulture);

            return
                $"FIRE MODE:  {mode}\n" +
                $"CALIBER:    {cal}\n" +
                $"DAMAGE:     {dmg}\n" +
                $"FIRE RATE:  {weapon.fireRate.ToString("0.##", CultureInfo.InvariantCulture)} / S\n" +
                $"MAGAZINE:   {weapon.magazineSize} RDS\n" +
                $"RELOAD:     {weapon.reloadTime.ToString("0.##", CultureInfo.InvariantCulture)} S\n" +
                $"RANGE:      {weapon.bulletRange.ToString("0.#", CultureInfo.InvariantCulture)} M\n" +
                $"SPREAD:     {weapon.bulletSpread.ToString("0.#", CultureInfo.InvariantCulture)}°";
        }

        if (itemAsset is ConsumableData consumable)
        {
            string desc = string.IsNullOrWhiteSpace(consumable.description)
                ? "Tactical field consumable."
                : consumable.description.Trim();

            string stats;
            if (consumable.type == ConsumableType.Healing)
            {
                stats =
                    $"HEAL AMOUNT:     +{consumable.healAmount.ToString("0", CultureInfo.InvariantCulture)} HP\n" +
                    $"COOLDOWN:        {consumable.cooldown.ToString("0.##", CultureInfo.InvariantCulture)} S\n" +
                    $"QUICK-SLOT CAP:  {consumable.maxQuickSlotStack}";
            }
            else
            {
                stats =
                    $"BLAST DAMAGE:    {consumable.grenadeDamage.ToString("0", CultureInfo.InvariantCulture)}\n" +
                    $"BLAST RADIUS:    {consumable.explosionRadius.ToString("0.0", CultureInfo.InvariantCulture)} M\n" +
                    $"FUSE TIME:       {consumable.fuseTime.ToString("0.0", CultureInfo.InvariantCulture)} S\n" +
                    $"COOLDOWN:        {consumable.cooldown.ToString("0.##", CultureInfo.InvariantCulture)} S\n" +
                    $"QUICK-SLOT CAP:  {consumable.maxQuickSlotStack}";
            }

            return $"{desc}\n\n{stats}";
        }

        return itemAsset != null ? $"ASSET: {itemAsset.name}" : "No item details available.";
    }
}

/// <summary>
/// Tweakable ScriptableObject defining a shop's restock cadence, category roll counts,
/// and weighted random item pool.
/// </summary>
[CreateAssetMenu(fileName = "DefaultShopCatalog", menuName = "Darkzone/Shop Catalog Data")]
public class ShopCatalogData : ScriptableObject
{
    [Header("Restock Cadence")]
    [Tooltip("Number of completed Darkzone runs between automatic shop stock refreshes.")]
    [Min(1)]
    public int runsPerRestock = 2;

    [Tooltip("Rolled prices are rounded to the nearest multiple of this step when priceVariance > 0.")]
    [Min(1)]
    public int priceRoundingStep = 10;

    [Header("Category Roll Configuration")]
    public List<ShopCategoryRollConfig> categoryConfigs = new List<ShopCategoryRollConfig>
    {
        new ShopCategoryRollConfig { category = ShopCategory.Weapons, minListings = 3, maxListings = 4, allowDuplicates = false },
        new ShopCategoryRollConfig { category = ShopCategory.Equipment, minListings = 0, maxListings = 0, allowDuplicates = false },
        new ShopCategoryRollConfig { category = ShopCategory.Consumables, minListings = 2, maxListings = 2, allowDuplicates = false }
    };

    [Header("Item Pool")]
    public List<ShopPoolEntry> itemPool = new List<ShopPoolEntry>();

    /// <summary>
    /// Generates a randomized list of shop listings according to category configs and weights.
    /// </summary>
    public List<ShopListingRuntime> RollListings()
    {
        List<ShopListingRuntime> results = new List<ShopListingRuntime>();
        if (categoryConfigs == null || itemPool == null)
            return results;

        for (int c = 0; c < categoryConfigs.Count; c++)
        {
            ShopCategoryRollConfig cfg = categoryConfigs[c];
            if (cfg == null) continue;

            int min = Mathf.Max(0, cfg.minListings);
            int max = Mathf.Max(min, cfg.maxListings);
            if (max == 0) continue;

            List<ShopPoolEntry> candidates = new List<ShopPoolEntry>();
            for (int i = 0; i < itemPool.Count; i++)
            {
                ShopPoolEntry entry = itemPool[i];
                if (entry != null && entry.itemAsset != null && entry.category == cfg.category && entry.spawnWeight > 0f)
                    candidates.Add(entry);
            }

            if (candidates.Count == 0) continue;

            int targetCount = UnityEngine.Random.Range(min, max + 1);
            if (!cfg.allowDuplicates)
                targetCount = Mathf.Min(targetCount, candidates.Count);

            if (targetCount <= 0) continue;

            List<ShopPoolEntry> available = new List<ShopPoolEntry>(candidates);
            int picked = 0;

            // Guaranteed items first
            for (int i = available.Count - 1; i >= 0 && picked < targetCount; i--)
            {
                if (!available[i].alwaysIncluded) continue;

                results.Add(CreateRuntimeListing(available[i]));
                picked++;
                if (!cfg.allowDuplicates)
                    available.RemoveAt(i);
            }

            // Weighted random selection for remaining slots
            while (picked < targetCount && available.Count > 0)
            {
                int chosenIndex = PickWeightedIndex(available);
                if (chosenIndex < 0) break;

                results.Add(CreateRuntimeListing(available[chosenIndex]));
                picked++;

                if (!cfg.allowDuplicates)
                    available.RemoveAt(chosenIndex);
            }
        }

        return results;
    }

    private ShopListingRuntime CreateRuntimeListing(ShopPoolEntry entry)
    {
        int rolledPrice = Mathf.Max(0, entry.basePrice);
        if (entry.priceVariance > 0f && entry.basePrice > 0)
        {
            float factor = UnityEngine.Random.Range(1f - entry.priceVariance, 1f + entry.priceVariance);
            int step = Mathf.Max(1, priceRoundingStep);
            rolledPrice = Mathf.Max(0, Mathf.RoundToInt((entry.basePrice * factor) / step) * step);
        }

        int rolledStock;
        if (entry.unlimitedStock)
        {
            rolledStock = -1;
        }
        else if (entry.maxStock <= 0)
        {
            rolledStock = 0;
        }
        else
        {
            int lo = Mathf.Max(1, Mathf.Min(entry.minStock, entry.maxStock));
            int hi = Mathf.Max(lo, entry.maxStock);
            rolledStock = UnityEngine.Random.Range(lo, hi + 1);
        }

        return new ShopListingRuntime
        {
            sourceEntry = entry,
            itemAsset = entry.itemAsset,
            category = entry.category,
            price = rolledPrice,
            initialStock = rolledStock,
            remainingStock = rolledStock
        };
    }

    private static int PickWeightedIndex(List<ShopPoolEntry> entries)
    {
        float totalWeight = 0f;
        for (int i = 0; i < entries.Count; i++)
            totalWeight += Mathf.Max(0f, entries[i].spawnWeight);

        if (totalWeight <= 0f)
            return entries.Count > 0 ? 0 : -1;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += Mathf.Max(0f, entries[i].spawnWeight);
            if (roll <= cumulative)
                return i;
        }

        return entries.Count - 1;
    }
}
