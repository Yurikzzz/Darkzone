using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the top-left OP-01 status badge (STANDBY in safezone, ACTIVE in Darkzone)
/// and hides the active HUD root while LoadoutUI is open.
/// Must be attached to an always-active GameObject (e.g. HUD Canvas), not inside hudRoot.
/// </summary>
public class HUDStatusController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Image statusDot;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Status Colors")]
    [SerializeField] private Color activeDotColor = new Color(0.133f, 0.773f, 0.369f, 1f);  // #22C55E
    [SerializeField] private Color standbyDotColor = new Color(0.784f, 0.643f, 0.302f, 1f); // #C8A44D
    [SerializeField] private Color statusTextColor = new Color(0.541f, 0.569f, 0.604f, 1f); // #8A919A

    private DarkzoneManager boundManager;
    private bool lastInZone;
    private bool hasInitializedStatus;

    private void Start()
    {
        TrySubscribe();
        RefreshStatus();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (hudRoot != null)
        {
            bool menuOpen = LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen;
            if (hudRoot.activeSelf == menuOpen)
                hudRoot.SetActive(!menuOpen);
        }

        if (boundManager == null && DarkzoneManager.Instance != null)
            TrySubscribe();

        bool currentInZone = DarkzoneManager.Instance != null && DarkzoneManager.Instance.IsPlayerInZone;
        if (!hasInitializedStatus || currentInZone != lastInZone)
            RefreshStatus();
    }

    private void TrySubscribe()
    {
        if (DarkzoneManager.Instance == null || boundManager == DarkzoneManager.Instance)
            return;

        Unsubscribe();
        boundManager = DarkzoneManager.Instance;
        boundManager.OnPlayerEnteredZone += RefreshStatus;
        boundManager.OnPlayerLeftZone += RefreshStatus;
    }

    private void Unsubscribe()
    {
        if (boundManager != null)
        {
            boundManager.OnPlayerEnteredZone -= RefreshStatus;
            boundManager.OnPlayerLeftZone -= RefreshStatus;
            boundManager = null;
        }
    }

    public void RefreshStatus()
    {
        bool inZone = DarkzoneManager.Instance != null && DarkzoneManager.Instance.IsPlayerInZone;
        lastInZone = inZone;
        hasInitializedStatus = true;

        if (statusText != null)
        {
            statusText.text = inZone ? "OP-01 // ACTIVE" : "OP-01 // STANDBY";
            statusText.color = statusTextColor;
        }

        if (statusDot != null)
            statusDot.color = inZone ? activeDotColor : standbyDotColor;
    }
}
