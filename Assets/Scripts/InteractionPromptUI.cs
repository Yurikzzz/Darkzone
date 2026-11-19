using UnityEngine;

/// <summary>
/// Trigger driver for the centralized screen-space Interaction Prompt HUD widget.
/// Attach to any GameObject with a 2D trigger collider; when the player is in range
/// and the prompt is not suppressed, registers itself with HUDInteractionPromptUI.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class InteractionPromptUI : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("Legacy fallback text shown on the prompt (e.g. '[E] Open Loadout', '[E] Evacuate').")]
    [SerializeField] private string promptText = "[E] Interact";

    [Tooltip("Keybind label displayed in the HUD keybind box.")]
    [SerializeField] private string keyLabel = "E";

    [Tooltip("Category header displayed above the target name (e.g. 'OPEN MENU // LOADOUT').")]
    [SerializeField] private string actionLabel = "INTERACT // OBJECT";

    [Tooltip("Target name displayed below the category header (e.g. 'LOADOUT').")]
    [SerializeField] private string targetLabel = "INTERACT";

    [Tooltip("World-space offset above the object where the prompt floats.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.9f);

#pragma warning disable CS0414
    [SerializeField, HideInInspector] private float fontSize = 3f;
    [SerializeField, HideInInspector] private Color textColor = Color.white;
    [SerializeField, HideInInspector] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField, HideInInspector] private float paddingX = 0.3f;
    [SerializeField, HideInInspector] private float paddingY = 0.15f;
    [SerializeField, HideInInspector] private int sortingOrder = 100;
    [SerializeField, HideInInspector] private float fadeDuration = 0.15f;
#pragma warning restore CS0414

    // ── Runtime ────────────────────────────────────────────────────────
    private bool playerInRange;
    private bool isSuppressed;
    private bool hasExplicitPrompt;

    public string KeyLabel => keyLabel;
    public string ActionLabel => actionLabel;
    public string TargetLabel => targetLabel;
    public Vector2 Offset => offset;

    public bool IsShowing =>
        isActiveAndEnabled &&
        playerInRange &&
        !isSuppressed &&
        (!string.IsNullOrEmpty(targetLabel) || !string.IsNullOrEmpty(promptText));

    /// <summary>
    /// Allows other scripts to change the prompt text at runtime.
    /// Automatically derives key, category, and target wording from bracketed '[Key] Verb Target' format.
    /// </summary>
    public string PromptText
    {
        get => promptText;
        set
        {
            promptText = value;
            hasExplicitPrompt = false;
            ApplySmartWordingFromPromptText(promptText);
            RefreshRegistration();
        }
    }

    /// <summary>
    /// Explicitly configures the keybind, category header, and target label for the HUD prompt.
    /// </summary>
    public void SetPrompt(KeyCode key, string actionCategory, string targetName)
    {
        SetPrompt(FormatKeyName(key), actionCategory, targetName);
    }

    /// <summary>
    /// Explicitly configures the keybind, category header, and target label for the HUD prompt.
    /// </summary>
    public void SetPrompt(string key, string actionCategory, string targetName)
    {
        hasExplicitPrompt = true;
        keyLabel = string.IsNullOrEmpty(key) ? "E" : key.Trim().ToUpperInvariant();
        actionLabel = string.IsNullOrEmpty(actionCategory) ? "INTERACT // OBJECT" : actionCategory.Trim().ToUpperInvariant();
        targetLabel = string.IsNullOrEmpty(targetName) ? "INTERACT" : targetName.Trim().ToUpperInvariant();
        promptText = $"[{keyLabel}] {targetLabel}";
        RefreshRegistration();
    }

    /// <summary>
    /// Allows other scripts to show/hide the prompt independently of trigger logic.
    /// </summary>
    public void SetVisible(bool visible)
    {
        playerInRange = visible;
        RefreshRegistration();
    }

    /// <summary>
    /// When suppressed, the prompt is hidden regardless of player proximity.
    /// Used when opening chests or evacuating so only the timed action bar is shown.
    /// </summary>
    public void SetSuppressed(bool suppress)
    {
        isSuppressed = suppress;
        RefreshRegistration();
    }

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (!hasExplicitPrompt)
            ApplySmartWordingFromPromptText(promptText);
    }

    private void OnEnable()
    {
        RefreshRegistration();
    }

    private void OnDisable()
    {
        HUDInteractionPromptUI.Unregister(this);
    }

    private void OnDestroy()
    {
        HUDInteractionPromptUI.Unregister(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            RefreshRegistration();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            RefreshRegistration();
        }
    }

    private void RefreshRegistration()
    {
        if (IsShowing)
            HUDInteractionPromptUI.Register(this);
        else
            HUDInteractionPromptUI.Unregister(this);
    }

    private void ApplySmartWordingFromPromptText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            actionLabel = "";
            targetLabel = "";
            return;
        }

        string trimmed = raw.Trim();
        string parsedKey = "E";
        string body = trimmed;

        if (trimmed.StartsWith("["))
        {
            int closeIdx = trimmed.IndexOf(']');
            if (closeIdx > 1)
            {
                parsedKey = trimmed.Substring(1, closeIdx - 1).Trim().ToUpperInvariant();
                body = trimmed.Substring(closeIdx + 1).Trim();
            }
        }

        keyLabel = string.IsNullOrEmpty(parsedKey) ? "E" : parsedKey;
        string lower = body.ToLowerInvariant();

        if (lower.Contains("loadout"))
        {
            actionLabel = "OPEN MENU // LOADOUT";
            targetLabel = "LOADOUT";
        }
        else if (lower.Contains("chest") || lower.Contains("crate"))
        {
            actionLabel = "OPEN // CONTAINER";
            targetLabel = "CHEST";
        }
        else if (lower.Contains("enter darkzone") || lower.Contains("enter zone"))
        {
            actionLabel = "ENTER // ZONE";
            targetLabel = "DARKZONE";
        }
        else if (lower.Contains("evac"))
        {
            actionLabel = "EVACUATE // ZONE";
            targetLabel = "EVACUATION";
        }
        else
        {
            int spaceIdx = body.IndexOf(' ');
            if (spaceIdx > 0 && spaceIdx < body.Length - 1)
            {
                string verb = body.Substring(0, spaceIdx).Trim().ToUpperInvariant();
                string target = body.Substring(spaceIdx + 1).Trim().ToUpperInvariant();
                actionLabel = $"{verb} // {target}";
                targetLabel = target;
            }
            else
            {
                string word = body.ToUpperInvariant();
                actionLabel = $"INTERACT // {word}";
                targetLabel = word;
            }
        }
    }

    private static string FormatKeyName(KeyCode key)
    {
        string raw = key.ToString();
        if (raw.StartsWith("Alpha") && raw.Length == 6)
            return raw.Substring(5);
        if (raw.StartsWith("Keypad") && raw.Length > 6)
            return raw.Substring(6);
        return raw.ToUpperInvariant();
    }
}
