using UnityEngine;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private bool showWeaponName = true;

    private bool isReloading;

    private void Awake()
    {
        if (ammoText != null)
        {
            ammoText.enableAutoSizing = true;
            ammoText.fontSizeMin = 14f;
            ammoText.fontSizeMax = 28f;
        }
    }

    private void OnEnable()
    {
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged += UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged += UpdateReloadState;
            weaponController.OnWeaponChanged += HandleWeaponChanged;

            if (weaponController.CurrentWeapon != null)
            {
                UpdateAmmoDisplay(weaponController.CurrentAmmo, weaponController.CurrentWeapon.magazineSize);
            }
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

    private void HandleWeaponChanged(WeaponData weapon)
    {
        if (weaponController != null && weapon != null)
        {
            UpdateAmmoDisplay(weaponController.CurrentAmmo, weapon.magazineSize);
        }
    }

    private void UpdateAmmoDisplay(int current, int max)
    {
        if (ammoText == null) return;

        if (!isReloading)
        {
            string prefix = (showWeaponName && weaponController != null && weaponController.CurrentWeapon != null)
                ? weaponController.CurrentWeapon.weaponName + "\n"
                : "";
            ammoText.text = prefix + current + "/" + max;
        }
    }

    private void UpdateReloadState(bool reloading)
    {
        isReloading = reloading;
        if (ammoText == null) return;

        if (reloading)
        {
            string prefix = (showWeaponName && weaponController != null && weaponController.CurrentWeapon != null)
                ? weaponController.CurrentWeapon.weaponName + "\n"
                : "";
            ammoText.text = prefix + "Reloading...";
        }
    }
}
