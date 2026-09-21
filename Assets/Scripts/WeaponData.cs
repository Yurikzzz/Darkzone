using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName = "Pistol";
    public bool isAutomatic = false;

    [Header("Firing")]
    public float fireRate = 5f;
    public int bulletsPerShot = 1;
    public float bulletSpread = 2f;

    [Header("Bullet")]
    public float bulletSpeed = 25f;
    public float bulletRange = 30f;
    public float damage = 10f;

    [Header("Magazine")]
    public int magazineSize = 15;
    public float reloadTime = 1.5f;
}