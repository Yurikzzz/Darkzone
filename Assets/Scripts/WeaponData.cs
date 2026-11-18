using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    [Tooltip("Display name of the weapon.")]
    public string weaponName = "Pistol";

    [Tooltip("If true, holding down fire fires continuously. If false, each click fires one shot.")]
    public bool isAutomatic = false;

    [Tooltip("Ammunition caliber label shown on the HUD.")]
    public string caliber = "9MM";

    [Header("Firing Parameters")]
    [Tooltip("Shots per second. For semi-auto, sets the maximum fire rate limit.")]
    [Min(0.01f)]
    public float fireRate = 5f;

    [Tooltip("Number of bullets/projectiles fired per trigger pull.")]
    [Min(1)]
    public int bulletsPerShot = 1;

    [Tooltip("Maximum spread deviation in degrees from aim direction.")]
    [Min(0f)]
    public float bulletSpread = 2f;

    [Header("Bullet Stats")]
    [Tooltip("Speed factor of the bullet.")]
    [Min(1f)]
    public float bulletSpeed = 25f;

    [Tooltip("Maximum hitscan range in world units.")]
    [Min(1f)]
    public float bulletRange = 30f;

    [Tooltip("Damage dealt per bullet hit.")]
    [Min(0f)]
    public float damage = 10f;

    [Header("Magazine & Reload")]
    [Tooltip("Maximum ammo capacity in a magazine.")]
    [Min(1)]
    public int magazineSize = 15;

    [Tooltip("Time in seconds to reload a full magazine.")]
    [Min(0.05f)]
    public float reloadTime = 1.5f;

    [Header("Visuals & FX")]
    [Tooltip("Sprite for this weapon, displayed on the player's hands.")]
    public Sprite weaponSprite;

    [Tooltip("Fire point offset relative to default position.")]
    public Vector2 firePointOffset = Vector2.zero;

    [Tooltip("Color of the bullet tracer.")]
    public Color bulletColor = Color.yellow;

    [Tooltip("Width of the bullet tracer.")]
    [Range(0.05f, 2f)]
    public float trailWidth = 0.5f;

    [Tooltip("Duration the tracer remains visible before fading out.")]
    [Range(0.01f, 1f)]
    public float trailFadeTime = 0.25f;

    [Header("Muzzle Flash")]
    [Tooltip("If true, plays the procedural pixel-art muzzle flash when firing this weapon.")]
    public bool enableMuzzleFlash = true;

    [Tooltip("Overall scale of the muzzle flash, glow, sparks, and smoke.")]
    [Range(0.25f, 2.5f)]
    public float muzzleFlashSize = 1f;

    [Tooltip("Number of sparks spawned per shot (-1 = auto based on muzzle flash size).")]
    [Min(-1)]
    public int muzzleFlashSparks = -1;

    [Tooltip("If true, spawns muzzle smoke puffs and rising wisps.")]
    public bool muzzleFlashSmoke = true;

    [Tooltip("If true, ejects a spinning brass shell casing.")]
    public bool muzzleFlashCasing = true;

    [Tooltip("Visual gun recoil kick multiplier applied to the player's hands on shot (0 = no kick).")]
    [Range(0f, 3f)]
    public float muzzleFlashKick = 1f;

    [Header("Screen Shake")]
    [Tooltip("Subtle camera shake intensity when firing this weapon (0 = no shake).")]
    [Range(0f, 0.5f)]
    public float screenShakeIntensity = 0.05f;

    [Tooltip("Duration in seconds of the gunshot screen shake.")]
    [Range(0.01f, 0.5f)]
    public float screenShakeDuration = 0.09f;

    /// <summary>
    /// True when this object is an independent runtime instance cloned for an inventory item.
    /// </summary>
    [System.NonSerialized]
    public bool isRuntimeInstance;

    /// <summary>
    /// Creates an independent runtime instance of this weapon with its own separate stats.
    /// </summary>
    public WeaponData CreateRuntimeInstance()
    {
        WeaponData instance = Instantiate(this);
        instance.name = name;
        instance.isRuntimeInstance = true;
        return instance;
    }
}
