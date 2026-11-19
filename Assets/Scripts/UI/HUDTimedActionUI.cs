using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the screen-space Timed Action panel under HUD Canvas/HUDRoot.
/// Displays progress bar, countdown timer, action label, and cancel hint
/// matching the HUDUI.pen Timed Action component.
/// </summary>
public class HUDTimedActionUI : MonoBehaviour
{
    private static HUDTimedActionUI instance;

    public static HUDTimedActionUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<HUDTimedActionUI>(FindObjectsInactive.Include);
            return instance;
        }
    }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image activeDotImage;
    [SerializeField] private TextMeshProUGUI actionNameText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private RectTransform progressFillRect;
    [SerializeField] private TextMeshProUGUI cancelHintText;
    [SerializeField] private TextMeshProUGUI progressPercentText;

    private EvacTimerUI currentOwner;

    private void Awake()
    {
        if (instance == null)
            instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void Show(EvacTimerUI owner, string actionName, float timeRemaining, float duration, string cancelHint = "[ESC] CANCEL")
    {
        currentOwner = owner;
        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        UpdateProgress(owner, actionName, timeRemaining, duration, cancelHint);
    }

    public void UpdateProgress(EvacTimerUI owner, string actionName, float timeRemaining, float duration, string cancelHint = "[ESC] CANCEL")
    {
        if (currentOwner != null && currentOwner != owner)
            return;

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (actionNameText != null)
            actionNameText.text = string.IsNullOrEmpty(actionName) ? "ACTION // IN PROGRESS" : actionName.ToUpperInvariant();

        float clampedRemaining = Mathf.Max(0f, timeRemaining);
        if (countdownText != null)
            countdownText.text = $"{clampedRemaining:00.0}s";

        float elapsedFraction = duration > 0.0001f ? Mathf.Clamp01(1f - (clampedRemaining / duration)) : 1f;

        if (progressFillRect != null)
        {
            progressFillRect.anchorMin = new Vector2(0f, 0f);
            progressFillRect.anchorMax = new Vector2(elapsedFraction, 1f);
            progressFillRect.offsetMin = Vector2.zero;
            progressFillRect.offsetMax = Vector2.zero;
        }

        if (progressPercentText != null)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(elapsedFraction * 100f), 0, 100);
            progressPercentText.text = $"{pct}%";
        }

        if (cancelHintText != null)
            cancelHintText.text = cancelHint;
    }

    public void Hide(EvacTimerUI owner)
    {
        if (currentOwner != null && currentOwner != owner)
            return;

        currentOwner = null;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
