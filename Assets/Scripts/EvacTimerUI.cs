using UnityEngine;

/// <summary>
/// Timer driver for timed world interactions (e.g. chest opening and evacuation).
/// Drives the screen-space HUDTimedActionUI panel in HUD Canvas/HUDRoot
/// and supports cancellation via [ESC] or leaving range.
/// </summary>
public class EvacTimerUI : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("Total countdown duration in seconds.")]
    [SerializeField] private float duration = 15f;

    [Tooltip("Header label displayed on the Timed Action HUD panel.")]
    [SerializeField] private string actionLabel = "ACTION // IN PROGRESS";

    [Tooltip("When true, pressing Escape during the timed action cancels it.")]
    [SerializeField] private bool allowEscapeCancel = true;

    [SerializeField, HideInInspector] private Vector2 offset = new Vector2(0f, 2.5f);
    [SerializeField, HideInInspector] private float ringRadius = 0.5f;
    [SerializeField, HideInInspector] private float ringThickness = 0.08f;
    [SerializeField, HideInInspector] private int ringSegments = 64;
    [SerializeField, HideInInspector] private Color ringBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField, HideInInspector] private Color ringFillColor = new Color(0f, 0.9f, 1f, 0.9f);
    [SerializeField, HideInInspector] private Color timerTextColor = Color.white;
    [SerializeField, HideInInspector] private float textFontSize = 4f;
    [SerializeField, HideInInspector] private int sortingOrder = 110;

    // ── Events ────────────────────────────────────────────────────────
    public event System.Action OnTimerComplete;
    public event System.Action OnTimerCancelled;

    // ── Runtime ───────────────────────────────────────────────────────
    private bool isRunning;
    private float timeRemaining;

    /// <summary>True while the countdown is active.</summary>
    public bool IsRunning => isRunning;

    /// <summary>Remaining seconds (read-only).</summary>
    public float TimeRemaining => timeRemaining;

    /// <summary>Total countdown duration in seconds.</summary>
    public float Duration
    {
        get => duration;
        set => duration = value;
    }

    /// <summary>Header label displayed on the Timed Action HUD panel.</summary>
    public string ActionLabel
    {
        get => actionLabel;
        set => actionLabel = value;
    }

    /// <summary>Whether pressing Escape cancels this timed action.</summary>
    public bool AllowEscapeCancel
    {
        get => allowEscapeCancel;
        set => allowEscapeCancel = value;
    }

    /// <summary>Legacy vertical offset property retained for compatibility.</summary>
    public Vector2 Offset
    {
        get => offset;
        set => offset = value;
    }

    /// <summary>Legacy fill color property retained for compatibility.</summary>
    public Color RingFillColor
    {
        get => ringFillColor;
        set => ringFillColor = value;
    }

    // ────────────────────────────────────────────────────────────────────

    private void OnDisable()
    {
        if (isRunning)
        {
            isRunning = false;
            HUDTimedActionUI.Instance?.Hide(this);
        }
    }

    private void OnDestroy()
    {
        if (isRunning)
        {
            isRunning = false;
            HUDTimedActionUI.Instance?.Hide(this);
        }
    }

    /// <summary>Start (or restart) the countdown.</summary>
    public void StartTimer()
    {
        timeRemaining = duration;
        isRunning = true;
        string hint = allowEscapeCancel ? "[ESC] CANCEL" : "";
        HUDTimedActionUI.Instance?.Show(this, actionLabel, timeRemaining, duration, hint);
    }

    /// <summary>Cancel the timer and hide the HUD panel.</summary>
    public void CancelTimer()
    {
        if (!isRunning)
            return;

        isRunning = false;
        HUDTimedActionUI.Instance?.Hide(this);
    }

    private void Update()
    {
        if (!isRunning)
            return;

        bool menuOpen = LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen;
        if (allowEscapeCancel && !menuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTimer();
            OnTimerCancelled?.Invoke();
            return;
        }

        timeRemaining -= Time.deltaTime;
        string hint = allowEscapeCancel ? "[ESC] CANCEL" : "";

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            HUDTimedActionUI.Instance?.Hide(this);
            OnTimerComplete?.Invoke();
            return;
        }

        HUDTimedActionUI.Instance?.UpdateProgress(this, actionLabel, timeRemaining, duration, hint);
    }
}
