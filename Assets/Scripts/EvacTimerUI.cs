using UnityEngine;
using TMPro;

/// <summary>
/// Displays a circular countdown timer above a GameObject (e.g. the evac point).
/// Fully self-contained — creates its own visuals at runtime (no prefab needed).
/// Call StartTimer() to begin the countdown and subscribe to OnTimerComplete.
/// </summary>
public class EvacTimerUI : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("Total countdown duration in seconds.")]
    [SerializeField] private float duration = 15f;

    [Header("Visuals")]
    [Tooltip("Vertical offset above the object (world units).")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 2.5f);

    [SerializeField] private float ringRadius = 0.5f;
    [SerializeField] private float ringThickness = 0.08f;
    [SerializeField] private int ringSegments = 64;
    [SerializeField] private Color ringBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color ringFillColor = new Color(0f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color timerTextColor = Color.white;
    [SerializeField] private float textFontSize = 4f;
    [SerializeField] private int sortingOrder = 110;

    // ── Events ────────────────────────────────────────────────────────
    public event System.Action OnTimerComplete;

    // ── Runtime ───────────────────────────────────────────────────────
    private GameObject timerRoot;
    private TextMeshPro timerText;
    private LineRenderer ringBg;
    private LineRenderer ringFill;
    private bool isRunning;
    private float timeRemaining;

    /// <summary>True while the countdown is active.</summary>
    public bool IsRunning => isRunning;

    /// <summary>Remaining seconds (read-only).</summary>
    public float TimeRemaining => timeRemaining;

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CreateVisuals();
        timerRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (timerRoot != null)
            Destroy(timerRoot);
    }

    /// <summary>Start (or restart) the countdown.</summary>
    public void StartTimer()
    {
        timeRemaining = duration;
        isRunning = true;
        timerRoot.SetActive(true);
        UpdateVisuals();
    }

    /// <summary>Cancel the timer and hide it.</summary>
    public void CancelTimer()
    {
        isRunning = false;
        timerRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isRunning) return;

        timeRemaining -= Time.deltaTime;
        timerRoot.transform.position = (Vector2)transform.position + offset;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            UpdateVisuals();
            timerRoot.SetActive(false);
            OnTimerComplete?.Invoke();
            return;
        }

        UpdateVisuals();
    }

    // ── Visual Construction ────────────────────────────────────────────

    private void CreateVisuals()
    {
        timerRoot = new GameObject($"{gameObject.name}_EvacTimer");

        // ── Background ring (full circle, dim) ──
        GameObject bgObj = new GameObject("RingBackground");
        bgObj.transform.SetParent(timerRoot.transform);
        bgObj.transform.localPosition = Vector3.zero;
        ringBg = bgObj.AddComponent<LineRenderer>();
        ConfigureRing(ringBg, ringBackgroundColor, 1f);

        // ── Fill ring (arc that shrinks as time runs out) ──
        GameObject fillObj = new GameObject("RingFill");
        fillObj.transform.SetParent(timerRoot.transform);
        fillObj.transform.localPosition = Vector3.zero;
        ringFill = fillObj.AddComponent<LineRenderer>();
        ConfigureRing(ringFill, ringFillColor, 1f);

        // ── Timer text ──
        GameObject textObj = new GameObject("TimerText");
        textObj.transform.SetParent(timerRoot.transform);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        timerText = textObj.AddComponent<TextMeshPro>();
        timerText.fontSize = textFontSize;
        timerText.color = timerTextColor;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.sortingOrder = sortingOrder + 2;
        timerText.enableWordWrapping = false;
        timerText.overflowMode = TextOverflowModes.Overflow;
        timerText.rectTransform.sizeDelta = new Vector2(2f, 1f);
    }

    private void ConfigureRing(LineRenderer lr, Color color, float fraction)
    {
        lr.useWorldSpace = false;
        lr.startWidth = ringThickness;
        lr.endWidth = ringThickness;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = sortingOrder;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.material.color = color;
        lr.loop = false;

        SetRingPoints(lr, fraction);
    }

    private void SetRingPoints(LineRenderer lr, float fraction)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(ringSegments * fraction) + 1);
        lr.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            // Start from the top (90°) and go clockwise
            float t = (float)i / (count - 1);
            float angle = (Mathf.PI / 2f) - t * fraction * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * ringRadius;
            float y = Mathf.Sin(angle) * ringRadius;
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void UpdateVisuals()
    {
        float fraction = Mathf.Clamp01(timeRemaining / duration);

        // Update the fill arc
        SetRingPoints(ringFill, fraction);

        // Update the text
        int seconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = seconds.ToString();

        // Pulse the fill color when low
        if (timeRemaining <= 5f)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) + 1f) / 2f;
            Color c = Color.Lerp(ringFillColor, Color.red, pulse);
            ringFill.startColor = c;
            ringFill.endColor = c;
            ringFill.material.color = c;
        }
        else
        {
            ringFill.startColor = ringFillColor;
            ringFill.endColor = ringFillColor;
            ringFill.material.color = ringFillColor;
        }
    }
}
