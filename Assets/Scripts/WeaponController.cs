using UnityEngine;
using System;
using System.Collections.Generic;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("State")]
    [SerializeField] private int currentAmmo;

    private bool isReloading;
    private float nextFireTime;
    private float reloadTimer;
    private Camera mainCam;
    private PlayerMovement playerMovement;
    private ContactFilter2D rayFilter;
    private readonly List<RaycastHit2D> rayResults = new List<RaycastHit2D>();

    public event Action<int, int> OnAmmoChanged;
    public event Action<bool> OnReloadStateChanged;
    public WeaponData CurrentWeapon => weaponData;

    private void Awake()
    {
        mainCam = Camera.main;
        playerMovement = GetComponentInParent<PlayerMovement>();

        rayFilter = new ContactFilter2D();
        rayFilter.useTriggers = false;
        rayFilter.SetLayerMask(Physics2D.AllLayers);
    }

    private void Start()
    {
        if (weaponData != null)
            currentAmmo = weaponData.magazineSize;
        NotifyAmmoChanged();
    }

    private void Update()
    {
        if (weaponData == null) return;

        HandleReloading();
        if (isReloading) return;

        HandleShooting();

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < weaponData.magazineSize)
            StartReload();
    }

    private void HandleShooting()
    {
        if (playerMovement != null && playerMovement.IsRunning) return;

        bool fireInput = weaponData.isAutomatic
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (fireInput && Time.time >= nextFireTime && currentAmmo > 0)
        {
            Fire();
            nextFireTime = Time.time + 1f / weaponData.fireRate;
        }
        else if (fireInput && currentAmmo <= 0)
        {
            StartReload();
        }
    }

    private void Fire()
    {
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 origin = (Vector2)firePoint.position;
        Vector2 dir = (mouseWorld - origin).normalized;

        for (int i = 0; i < weaponData.bulletsPerShot; i++)
        {
            float spread = UnityEngine.Random.Range(
                -weaponData.bulletSpread, weaponData.bulletSpread);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spread;
            Vector2 bulletDir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            // Hitscan raycast
            rayResults.Clear();
            Physics2D.Raycast(origin, bulletDir, rayFilter, rayResults, weaponData.bulletRange);

            Vector2 endPoint = origin + bulletDir * weaponData.bulletRange;

            foreach (var hit in rayResults)
            {
                if (hit.collider.CompareTag("Player")) continue;

                endPoint = hit.point;

                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(weaponData.damage);

                break;
            }

            // Spawn trail visual
            GameObject trailObj = Instantiate(bulletPrefab, Vector3.zero, Quaternion.identity);
            Bullet trail = trailObj.GetComponent<Bullet>();
            trail.Init(origin, endPoint);
        }

        currentAmmo--;
        NotifyAmmoChanged();

        if (currentAmmo <= 0)
            StartReload();
    }

    private void StartReload()
    {
        if (isReloading || weaponData == null) return;
        if (currentAmmo >= weaponData.magazineSize) return;

        isReloading = true;
        reloadTimer = weaponData.reloadTime;
        OnReloadStateChanged?.Invoke(true);
    }

    private void HandleReloading()
    {
        if (!isReloading) return;

        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
        {
            currentAmmo = weaponData.magazineSize;
            isReloading = false;
            OnReloadStateChanged?.Invoke(false);
            NotifyAmmoChanged();
        }
    }

    private void NotifyAmmoChanged()
    {
        if (weaponData != null)
            OnAmmoChanged?.Invoke(currentAmmo, weaponData.magazineSize);
    }

    public void EquipWeapon(WeaponData newWeapon)
    {
        weaponData = newWeapon;
        currentAmmo = newWeapon.magazineSize;
        isReloading = false;
        OnReloadStateChanged?.Invoke(false);
        NotifyAmmoChanged();
    }
}