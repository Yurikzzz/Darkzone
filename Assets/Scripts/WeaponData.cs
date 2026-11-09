using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    [Tooltip("Display name of the weapon.")]
    public string weaponName = "Pistol";

    [Tooltip("If true, holding down fire fires continuously. If false, each click fires one shot.")]
    public bool isAutomatic = false;

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
}
