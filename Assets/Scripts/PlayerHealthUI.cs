using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the bottom-left 01 // VITALS 10-segment health bar module.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Vitals Module UI")]
    [SerializeField] private Image healthDot;
    [SerializeField] private TextMeshProUGUI healthNumericText;
    [SerializeField] private TextMeshProUGUI healthStatusText;
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private Image[] segmentBorders = new Image[10];
    [SerializeField] private Image[] segmentFills = new Image[10];

    [Header("Legacy Fallback (Optional)")]
    [SerializeField] private Image crossFill;

    [Header("HUDUI Palette")]
    [SerializeField] private Color nominalFillColor = new Color(0.133f, 0.773f, 0.369f, 1f);   // #22C55E
    [SerializeField] private Color nominalBorderColor = new Color(0.086f, 0.396f, 0.204f, 1f); // #166534
    [SerializeField] private Color cautionFillColor = new Color(0.784f, 0.643f, 0.302f, 1f);   // #C8A44D
    [SerializeField] private Color cautionBorderColor = new Color(0.471f, 0.369f, 0.141f, 1f); // #785E24
    [SerializeField] private Color criticalFillColor = new Color(0.937f, 0.267f, 0.267f, 1f);  // #EF4444
    [SerializeField] private Color criticalBorderColor = new Color(0.498f, 0.114f, 0.114f, 1f);// #7F1D1D
    [SerializeField] private Color emptyFillColor = new Color(0.035f, 0.039f, 0.047f, 1f);     // #090A0C
    [SerializeField] private Color emptyBorderColor = new Color(0.137f, 0.153f, 0.176f, 1f);   // #23272D
    [SerializeField] private Color mutedTextColor = new Color(0.310f, 0.333f, 0.369f, 1f);     // #4F555E

    private void Awake()
    {
        EnsurePlayerHealth();
    }

    private void OnEnable()
    {
        EnsurePlayerHealth();
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
            playerHealth.OnHealthChanged += UpdateHealthDisplay;
            UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    private void Start()
    {
        EnsurePlayerHealth();
        if (playerHealth != null)
            UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
    }

    private void EnsurePlayerHealth()
    {
        if (playerHealth != null) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void UpdateHealthDisplay(float current, float max)
    {
        float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (crossFill != null && max > 0f)
            crossFill.fillAmount = pct;

        int litSegments = current <= 0f ? 0 : Mathf.Clamp(Mathf.FloorToInt(pct * 10f), 1, 10);

        Color activeFill;
        Color activeBorder;
        string statusStr;

        if (current <= 0f)
        {
            activeFill = emptyFillColor;
            activeBorder = emptyBorderColor;
            statusStr = "STATUS // OFFLINE";
        }
        else if (pct <= 0.25f)
        {
            activeFill = criticalFillColor;
            activeBorder = criticalBorderColor;
            statusStr = "STATUS // CRITICAL";
        }
        else if (pct <= 0.50f)
        {
            activeFill = cautionFillColor;
            activeBorder = cautionBorderColor;
            statusStr = "STATUS // CAUTION";
        }
        else
        {
            activeFill = nominalFillColor;
            activeBorder = nominalBorderColor;
            statusStr = "STATUS // NOMINAL";
        }

        Color indicatorColor = current <= 0f ? mutedTextColor : activeFill;

        if (healthDot != null)
            healthDot.color = indicatorColor;

        if (healthNumericText != null)
            healthNumericText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

        if (healthPercentText != null)
            healthPercentText.text = $"{Mathf.RoundToInt(pct * 100f)}%";

        if (healthStatusText != null)
        {
            healthStatusText.text = statusStr;
            healthStatusText.color = indicatorColor;
        }

        int segCount = segmentFills != null ? segmentFills.Length : 0;
        for (int i = 0; i < segCount; i++)
        {
            bool lit = i < litSegments;
            if (segmentFills[i] != null)
                segmentFills[i].color = lit ? activeFill : emptyFillColor;
            if (segmentBorders != null && i < segmentBorders.Length && segmentBorders[i] != null)
                segmentBorders[i].color = lit ? activeBorder : emptyBorderColor;
        }
    }
}
