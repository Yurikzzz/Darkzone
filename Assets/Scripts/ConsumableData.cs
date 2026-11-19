using System;
using UnityEngine;

public enum ConsumableType
{
    Healing,
    Grenade
}

/// <summary>
/// ScriptableObject defining all tweakable properties for a consumable item.
/// Supports healing items, throwable grenades, and future consumable types.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumable", menuName = "Darkzone/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    [Header("General Info")]
    [Tooltip("Display name of the consumable.")]
    public string consumableName = "Consumable";

    [Tooltip("Brief description of the item's effects.")]
    [TextArea(2, 4)]
    public string description = "Consumable item description.";

    [Tooltip("Icon sprite for UI and world drops. Uses white square placeholder if not set.")]
    public Sprite icon;

    [Tooltip("Color tint applied to the placeholder sprite and UI.")]
    public Color tintColor = Color.white;

    [Tooltip("Type of consumable behavior.")]
    public ConsumableType type = ConsumableType.Healing;

    [Tooltip("Minimum time in seconds between uses.")]
    public float cooldown = 0.5f;

    [Header("Stacking Settings")]
    [Tooltip("If true, identical consumables merge into a single stack in stash, active inventory, and HUD.")]
    public bool isStackable = true;

    [Tooltip("Maximum number of units that can be equipped into a single quick-slot from the loadout menu.")]
    [Min(1)]
    public int maxQuickSlotStack = 5;

    [Header("Healing Settings (Only for Healing type)")]
    [Tooltip("Amount of health restored immediately upon use.")]
    public float healAmount = 50f;

    [Tooltip("Color of the healing visual effect.")]
    public Color healEffectColor = new Color(0.2f, 0.95f, 0.35f, 0.9f);

    [Header("Grenade Settings (Only for Grenade type)")]
    [Tooltip("Explosive damage dealt to enemies caught in the blast radius.")]
    public float grenadeDamage = 80f;

    [Tooltip("Radius of the explosion in world units.")]
    public float explosionRadius = 3.5f;

    [Tooltip("Initial flight speed when thrown.")]
    public float throwSpeed = 12f;

    [Tooltip("Seconds before detonation after being thrown.")]
    public float fuseTime = 1.5f;

    [Tooltip("Knockback force applied to enemies caught in the explosion.")]
    public float explosionKnockback = 6f;

    [Tooltip("Color of the explosion blast circle.")]
    public Color explosionColor = new Color(1f, 0.55f, 0.1f, 0.9f);

    [Tooltip("Camera shake intensity at the center of the explosion.")]
    [Range(0f, 1f)]
    public float explosionShakeIntensity = 0.22f;

    [Tooltip("Duration in seconds of the explosion camera shake.")]
    [Range(0.05f, 1f)]
    public float explosionShakeDuration = 0.3f;

    /// <summary>
    /// True when this object is an independent runtime instance cloned for an inventory item.
    /// </summary>
    [NonSerialized]
    public bool isRuntimeInstance;

    /// <summary>
    /// Canonical ScriptableObject template asset used for stack identity matching.
    /// </summary>
    [NonSerialized]
    public ConsumableData templateAsset;

    /// <summary>
    /// Returns the canonical template asset for this consumable (or self if already the template).
    /// </summary>
    public ConsumableData GetTemplate()
    {
        return templateAsset != null ? templateAsset : this;
    }

    /// <summary>
    /// Returns true if this consumable represents the same stackable item type as <paramref name="other"/>.
    /// Non-stackable items only match by exact object reference.
    /// </summary>
    public bool Matches(ConsumableData other)
    {
        if (other == null) return false;
        if (!isStackable || !other.isStackable) return this == other;

        ConsumableData myTemplate = GetTemplate();
        ConsumableData otherTemplate = other.GetTemplate();
        if (myTemplate == otherTemplate) return true;

        return !myTemplate.isRuntimeInstance &&
               !otherTemplate.isRuntimeInstance &&
               myTemplate.type == otherTemplate.type &&
               string.Equals(myTemplate.consumableName, otherTemplate.consumableName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates an independent runtime instance of this consumable with its own separate stats.
    /// </summary>
    public ConsumableData CreateRuntimeInstance()
    {
        ConsumableData instance = Instantiate(this);
        instance.name = name;
        instance.isRuntimeInstance = true;
        instance.templateAsset = GetTemplate();
        return instance;
    }

    /// <summary>
    /// Formats a concise stats string for UI cards.
    /// </summary>
    public string GetStatsSummary()
    {
        switch (type)
        {
            case ConsumableType.Healing:
                return $"<color=#8A919A>HEAL +{healAmount:0} HP</color>";
            case ConsumableType.Grenade:
                return $"<color=#8A919A>DMG {grenadeDamage:0}</color>  <color=#4F555E>•</color>  <color=#8A919A>RAD {explosionRadius:0.1}m</color>  <color=#4F555E>•</color>  <color=#4F555E>FUSE {fuseTime:0.1}s</color>";
            default:
                return "";
        }
    }
}
