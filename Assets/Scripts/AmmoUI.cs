using UnityEngine;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private WeaponController weaponController;

    private bool isReloading;

    private void OnEnable()
    {
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged += UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged += UpdateReloadState;
        }
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged -= UpdateAmmoDisplay;
            weaponController.OnReloadStateChanged -= UpdateReloadState;
        }
    }

    private void UpdateAmmoDisplay(int current, int max)
    {
        if (!isReloading)
            ammoText.text = current + "/" + max;
    }

    private void UpdateReloadState(bool reloading)
    {
        isReloading = reloading;
        if (reloading)
            ammoText.text = "Reloading...";
    }
}