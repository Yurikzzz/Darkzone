using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the bottom-right Weapon & Ammo HUD module from WeaponController events.
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private WeaponController weaponController;

    [Header("Weapon Ammo Module UI")]
    [SerializeField] private GameObject activeAccentBar;
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private Sprite fallbackWeaponIcon;
    [SerializeField] private TextMeshProUGUI slotLabelText;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI weaponMetaText;
    [SerializeField] private TextMeshProUGUI ammoCurrentText;
    [SerializeField] private TextMeshProUGUI ammoMaxText;
    [SerializeField] private TextMeshProUGUI ammoLabelText;

    [Header("Legacy Fallback (Optional)")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private bool showWeaponName = true;

    [Header("HUDUI Palette")]
    [SerializeField] private Color primaryTextColor = new Color(0.945f, 0.953f, 0.961f, 1f); // #F1F3F5
    [SerializeField] private Color secondaryTextColor = new Color(0.541f, 0.569f, 0.604f, 1f);// #8A919A
    [SerializeField] private Color mutedTextColor = new Color(0.310f, 0.333f, 0.369f, 1f);    // #4F555E
    [SerializeField] private Color reloadingColor = new Color(0.784f, 0.643f, 0.302f, 1f);    // #C8A44D

    private bool isReloading;

    private void Awake()
    {
        EnsureWeaponController();
        if (ammoText != null)
        {
            ammoText.enableAutoSizing = true;
            ammoText.fontSizeMin = 14f;
            ammoText.fontSizeMax = 28f;
        }
    }

    private void OnEnable()
    {
        EnsureWeaponController();
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged -= UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged -= UpdateReloadState;
            weaponController.OnWeaponChanged -= HandleWeaponChanged;

            weaponController.OnAmmoChanged += UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged += UpdateReloadState;
            weaponController.OnWeaponChanged += HandleWeaponChanged;

            isReloading = weaponController.IsReloading;
            RefreshFull();
        }
    }

    private void Start()
    {
        EnsureWeaponController();
        if (weaponController != null)
        {
            isReloading = weaponController.IsReloading;
            RefreshFull();
        }
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged -= UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged -= UpdateReloadState;
            weaponController.OnWeaponChanged -= HandleWeaponChanged;
        }
    }

    private void EnsureWeaponController()
    {
        if (weaponController != null) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            weaponController = player.GetComponent<WeaponController>() ?? player.GetComponentInChildren<WeaponController>();
    }

    private void RefreshFull()
    {
        if (weaponController == null) return;
        WeaponData weapon = weaponController.CurrentWeapon;
        HandleWeaponChanged(weapon);
        if (isReloading)
            UpdateReloadState(true);
    }

    private void HandleWeaponChanged(WeaponData weapon)
    {
        UpdateWeaponInfo(weapon);

        if (weaponController != null && weapon != null)
        {
            UpdateAmmoDisplay(weaponController.CurrentAmmo, weapon.magazineSize);
        }
        else
        {
            isReloading = false;
            UpdateAmmoDisplay(0, 0);
        }
    }

    private void UpdateWeaponInfo(WeaponData weapon)
    {
        bool hasWeapon = weapon != null;

        if (activeAccentBar != null)
            activeAccentBar.SetActive(hasWeapon);

        if (weaponIconImage != null)
        {
            weaponIconImage.sprite = (hasWeapon && weapon.weaponSprite != null) ? weapon.weaponSprite : fallbackWeaponIcon;
            weaponIconImage.color = hasWeapon ? primaryTextColor : mutedTextColor;
            weaponIconImage.enabled = weaponIconImage.sprite != null;
        }

        if (slotLabelText != null)
        {
            if (!hasWeapon)
                slotLabelText.text = "UNARMED";
            else if (weaponController != null && weapon == weaponController.SecondarySlot && weapon != weaponController.PrimarySlot)
                slotLabelText.text = "SLOT 2 // SECONDARY";
            else
                slotLabelText.text = "SLOT 1 // PRIMARY";
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = hasWeapon ? weapon.weaponName.ToUpperInvariant() : "NO WEAPON";
            weaponNameText.color = hasWeapon ? primaryTextColor : mutedTextColor;
        }

        if (weaponMetaText != null)
        {
            if (hasWeapon)
            {
                string fireMode = weapon.isAutomatic ? "AUTO" : "SEMI";
                string cal = string.IsNullOrEmpty(weapon.caliber) ? "9MM" : weapon.caliber.ToUpperInvariant();
                weaponMetaText.text = $"{fireMode}  •  {cal}";
                weaponMetaText.color = secondaryTextColor;
            }
            else
            {
                weaponMetaText.text = "NONE";
                weaponMetaText.color = mutedTextColor;
            }
        }
    }

    private void UpdateAmmoDisplay(int current, int max)
    {
        if (ammoText != null && !isReloading)
        {
            string prefix = (showWeaponName && weaponController != null && weaponController.CurrentWeapon != null)
                ? weaponController.CurrentWeapon.weaponName + "\n"
                : "";
            ammoText.text = prefix + current + "/" + max;
        }

        if (ammoCurrentText != null)
        {
            ammoCurrentText.text = current.ToString();
            if (!isReloading)
                ammoCurrentText.color = weaponController != null && weaponController.CurrentWeapon != null ? primaryTextColor : mutedTextColor;
        }

        if (ammoMaxText != null)
        {
            ammoMaxText.text = $"/ {max}";
            ammoMaxText.color = secondaryTextColor;
        }

        if (ammoLabelText != null && !isReloading)
        {
            ammoLabelText.text = "MAG // CAP";
            ammoLabelText.color = mutedTextColor;
        }
    }

    private void UpdateReloadState(bool reloading)
    {
        isReloading = reloading;

        if (ammoText != null && reloading)
        {
            string prefix = (showWeaponName && weaponController != null && weaponController.CurrentWeapon != null)
                ? weaponController.CurrentWeapon.weaponName + "\n"
                : "";
            ammoText.text = prefix + "Reloading...";
        }

        if (ammoLabelText != null)
        {
            ammoLabelText.text = reloading ? "RELOADING..." : "MAG // CAP";
            ammoLabelText.color = reloading ? reloadingColor : mutedTextColor;
        }

        if (ammoCurrentText != null)
        {
            ammoCurrentText.color = reloading
                ? secondaryTextColor
                : (weaponController != null && weaponController.CurrentWeapon != null ? primaryTextColor : mutedTextColor);
        }
    }
}
