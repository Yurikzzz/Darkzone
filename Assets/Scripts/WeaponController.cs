using UnityEngine;
using System;
using System.Collections.Generic;

public class WeaponController : MonoBehaviour
{
    [Header("Current Weapon")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Weapon Inventory / Loadout")]
    [Tooltip("List of weapons available to the player. Can be switched using 1, 2, 3... or mouse scroll wheel.")]
    [SerializeField] private List<WeaponData> availableWeapons = new List<WeaponData>();
    [SerializeField] private int currentWeaponIndex = 0;

    [Header("Visual References")]
    [Tooltip("SpriteRenderer for the gun model (e.g. Hands/Gun). If left empty, automatically searches for Hands/Gun.")]
    [SerializeField] private SpriteRenderer gunSpriteRenderer;

    [Header("State")]
    [SerializeField] private int currentAmmo;

    private bool isReloading;
    private float nextFireTime;
    private float reloadTimer;
    private Camera mainCam;
    private PlayerMovement playerMovement;
    private PlayerMeleeAttack playerMelee;
    private ContactFilter2D rayFilter;
    private readonly List<RaycastHit2D> rayResults = new List<RaycastHit2D>();
    private readonly Dictionary<WeaponData, int> ammoPerWeapon = new Dictionary<WeaponData, int>();
    private Vector3 defaultFirePointLocalPos;
    private bool hasDefaultFirePointPos;

    public event Action<int, int> OnAmmoChanged;
    public event Action<bool> OnReloadStateChanged;
    public event Action<WeaponData> OnWeaponChanged;

    public WeaponData CurrentWeapon => weaponData;
    public int CurrentAmmo => currentAmmo;
    public List<WeaponData> AvailableWeapons => availableWeapons;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public bool IsReloading => isReloading;

    private void Awake()
    {
        mainCam = Camera.main;
        playerMovement = GetComponentInParent<PlayerMovement>();
        playerMelee = GetComponent<PlayerMeleeAttack>() ?? GetComponentInParent<PlayerMeleeAttack>();

        rayFilter = new ContactFilter2D();
        rayFilter.useTriggers = false;
        rayFilter.SetLayerMask(Physics2D.AllLayers);

        if (firePoint == null)
        {
            Transform fp = transform.Find("Hands/FirePoint");
            if (fp != null) firePoint = fp;
        }

        if (firePoint != null)
        {
            defaultFirePointLocalPos = firePoint.localPosition;
            hasDefaultFirePointPos = true;
        }

        if (gunSpriteRenderer == null)
        {
            Transform gunTrans = transform.Find("Hands/Gun");
            if (gunTrans != null)
                gunSpriteRenderer = gunTrans.GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        // Populate inventory if empty
        if (availableWeapons.Count == 0 && weaponData != null)
        {
            availableWeapons.Add(weaponData);
        }

        // Initialize weapons in the inventory
        foreach (var weapon in availableWeapons)
        {
            if (weapon != null && !ammoPerWeapon.ContainsKey(weapon))
            {
                ammoPerWeapon[weapon] = weapon.magazineSize;
            }
        }

        if (availableWeapons.Count > 0)
        {
            currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, availableWeapons.Count - 1);
            EquipWeaponByIndex(currentWeaponIndex);
        }
        else if (weaponData != null)
        {
            EquipWeapon(weaponData);
        }
    }

    private void Update()
    {
        if (playerMovement != null && !playerMovement.CanMove) return;

        HandleWeaponSwitchInput();

        if (weaponData == null) return;
        if (playerMelee != null && playerMelee.IsAttacking) return;

        HandleReloading();
        if (isReloading) return;

        HandleShooting();

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < weaponData.magazineSize)
            StartReload();
    }

    [Header("Equipped Slots")]
    [SerializeField] private WeaponData primarySlot;
    [SerializeField] private WeaponData secondarySlot;

    public WeaponData PrimarySlot => primarySlot;
    public WeaponData SecondarySlot => secondarySlot;

    private void HandleWeaponSwitchInput()
    {
        // Number key 1: Primary Weapon Slot
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            if (primarySlot != null && weaponData != primarySlot)
            {
                EquipWeapon(primarySlot);
            }
            return;
        }

        // Number key 2: Secondary Weapon Slot
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (secondarySlot != null && weaponData != secondarySlot)
            {
                EquipWeapon(secondarySlot);
            }
            return;
        }

        if (availableWeapons.Count <= 1) return;

        // Mouse scroll wheel cycling
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.05f)
        {
            int prevIndex = (currentWeaponIndex - 1 + availableWeapons.Count) % availableWeapons.Count;
            EquipWeaponByIndex(prevIndex);
        }
        else if (scroll < -0.05f)
        {
            int nextIndex = (currentWeaponIndex + 1) % availableWeapons.Count;
            EquipWeaponByIndex(nextIndex);
        }

        // Quick-switch cycle key: Q
        if (Input.GetKeyDown(KeyCode.Q))
        {
            int nextIndex = (currentWeaponIndex + 1) % availableWeapons.Count;
            EquipWeaponByIndex(nextIndex);
        }
    }

    /// <summary>
    /// Sets the equipped loadout using Primary and Secondary weapon slots.
    /// </summary>
    public void SetEquippedSlots(WeaponData primary, WeaponData secondary)
    {
        primarySlot = primary;
        secondarySlot = secondary;

        availableWeapons.Clear();
        if (primary != null)
        {
            availableWeapons.Add(primary);
            if (!ammoPerWeapon.ContainsKey(primary))
                ammoPerWeapon[primary] = primary.magazineSize;
        }
        if (secondary != null && secondary != primary)
        {
            availableWeapons.Add(secondary);
            if (!ammoPerWeapon.ContainsKey(secondary))
                ammoPerWeapon[secondary] = secondary.magazineSize;
        }

        if (availableWeapons.Count > 0)
        {
            if (weaponData == null || !availableWeapons.Contains(weaponData))
            {
                currentWeaponIndex = 0;
                EquipWeaponByIndex(0);
            }
            else
            {
                currentWeaponIndex = availableWeapons.IndexOf(weaponData);
                EquipWeapon(weaponData);
            }
        }
        else
        {
            weaponData = null;
            currentWeaponIndex = -1;
            if (gunSpriteRenderer != null)
                gunSpriteRenderer.sprite = null;
            OnWeaponChanged?.Invoke(null);
            OnAmmoChanged?.Invoke(0, 0);
        }
    }

    /// <summary>
    /// Sets the equipped loadout (backward-compatible list overload).
    /// </summary>
    public void SetLoadout(List<WeaponData> loadout)
    {
        WeaponData p = (loadout != null && loadout.Count > 0) ? loadout[0] : null;
        WeaponData s = (loadout != null && loadout.Count > 1) ? loadout[1] : null;
        SetEquippedSlots(p, s);
    }

    public void EquipWeaponByIndex(int index)
    {
        if (index < 0 || index >= availableWeapons.Count) return;
        WeaponData newWeapon = availableWeapons[index];
        if (newWeapon == null) return;

        currentWeaponIndex = index;
        EquipWeapon(newWeapon);
    }

    public void EquipWeapon(WeaponData newWeapon)
    {
        if (newWeapon == null) return;

        // Save current weapon ammo if applicable
        if (weaponData != null)
        {
            ammoPerWeapon[weaponData] = currentAmmo;
        }

        weaponData = newWeapon;

        // Restore or initialize ammo for this weapon
        if (ammoPerWeapon.TryGetValue(weaponData, out int savedAmmo))
        {
            currentAmmo = savedAmmo;
        }
        else
        {
            currentAmmo = weaponData.magazineSize;
            ammoPerWeapon[weaponData] = currentAmmo;
        }

        // Reset reload state on switch
        isReloading = false;
        reloadTimer = 0f;
        OnReloadStateChanged?.Invoke(false);

        // Update visual sprite
        if (gunSpriteRenderer != null && weaponData.weaponSprite != null)
        {
            gunSpriteRenderer.sprite = weaponData.weaponSprite;
        }

        // Update fire point offset
        if (firePoint != null && hasDefaultFirePointPos)
        {
            firePoint.localPosition = defaultFirePointLocalPos + (Vector3)weaponData.firePointOffset;
        }

        // Keep currentWeaponIndex synchronized
        int idx = availableWeapons.IndexOf(newWeapon);
        if (idx >= 0)
        {
            currentWeaponIndex = idx;
        }

        OnWeaponChanged?.Invoke(weaponData);
        NotifyAmmoChanged();
    }

    private void HandleShooting()
    {
        if (playerMovement != null && playerMovement.IsRunning) return;
        if (playerMelee != null && playerMelee.IsAttacking) return;

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

            // Spawn trail visual with custom weapon styling
            if (bulletPrefab != null)
            {
                GameObject trailObj = Instantiate(bulletPrefab, Vector3.zero, Quaternion.identity);
                Bullet trail = trailObj.GetComponent<Bullet>();
                if (trail != null)
                {
                    trail.Init(origin, endPoint, weaponData.bulletColor, weaponData.trailWidth, weaponData.trailFadeTime);
                }
            }
        }

        currentAmmo--;
        ammoPerWeapon[weaponData] = currentAmmo;
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
            ammoPerWeapon[weaponData] = currentAmmo;
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

    /// <summary>
    /// Refills the current magazine back to maximum and cancels any active reload.
    /// Also refills all weapons in the inventory.
    /// </summary>
    public void RefillAmmo()
    {
        foreach (var weapon in availableWeapons)
        {
            if (weapon != null)
                ammoPerWeapon[weapon] = weapon.magazineSize;
        }

        if (weaponData != null)
        {
            currentAmmo = weaponData.magazineSize;
            ammoPerWeapon[weaponData] = currentAmmo;
            isReloading = false;
            OnReloadStateChanged?.Invoke(false);
            NotifyAmmoChanged();
        }
    }
}
