using UnityEngine;
using TMPro;

/// <summary>
/// Displays a customizable world-space interaction prompt above interactable objects
/// when the player is in range. Attach to any GameObject with a trigger collider.
/// The prompt automatically faces the camera and fades in/out.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class InteractionPromptUI : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("Text shown on the prompt (e.g. '[E] Interact', '[E] Evacuate').")]
    [SerializeField] private string promptText = "[E] Interact";

    [Tooltip("Font size for the prompt text.")]
    [SerializeField] private float fontSize = 3f;

    [Tooltip("Vertical offset above the object (world units).")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);

    [Header("Appearance")]
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("Horizontal padding inside the background (world units).")]
    [SerializeField] private float paddingX = 0.3f;
    [Tooltip("Vertical padding inside the background (world units).")]
    [SerializeField] private float paddingY = 0.15f;
    [SerializeField] private int sortingOrder = 100;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.15f;

    // ── Runtime ────────────────────────────────────────────────────────
    private GameObject promptRoot;
    private TextMeshPro textMesh;
    private SpriteRenderer bgRenderer;
    private bool playerInRange;
    private float currentAlpha;

    /// <summary>
    /// Allows other scripts to change the prompt text at runtime.
    /// </summary>
    public string PromptText
    {
        get => promptText;
        set
        {
            promptText = value;
            if (textMesh != null)
            {
                textMesh.text = promptText;
                ResizeBackground();
            }
        }
    }

    /// <summary>
    /// Allows other scripts to show/hide the prompt independently of trigger logic.
    /// </summary>
    public void SetVisible(bool visible)
    {
        playerInRange = visible;
    }

    // ────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CreatePromptVisuals();
        SetAlpha(0f);
        promptRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (promptRoot != null)
            Destroy(promptRoot);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    private void Update()
    {
        float targetAlpha = playerInRange ? 1f : 0f;

        if (fadeDuration > 0f)
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);
        else
            currentAlpha = targetAlpha;

        if (currentAlpha > 0.01f)
        {
            if (!promptRoot.activeSelf)
                promptRoot.SetActive(true);

            SetAlpha(currentAlpha);
            promptRoot.transform.position = (Vector2)transform.position + offset;
        }
        else if (promptRoot.activeSelf)
        {
            promptRoot.SetActive(false);
        }
    }

    // ── Visual Creation ────────────────────────────────────────────────

    private void CreatePromptVisuals()
    {
        // Root (not parented to this object so it won't rotate with it)
        promptRoot = new GameObject($"{gameObject.name}_InteractPrompt");

        // Background sprite
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptRoot.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = whiteSprite;
        bgRenderer.color = backgroundColor;
        bgRenderer.sortingOrder = sortingOrder;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(promptRoot.transform);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        textMesh = textObj.AddComponent<TextMeshPro>();
        textMesh.text = promptText;
        textMesh.fontSize = fontSize;
        textMesh.color = textColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.sortingOrder = sortingOrder + 1;
        textMesh.enableWordWrapping = false;
        textMesh.overflowMode = TextOverflowModes.Overflow;

        // Force mesh update so we can measure
        textMesh.ForceMeshUpdate();
        ResizeBackground();
    }

    private void ResizeBackground()
    {
        if (textMesh == null || bgRenderer == null) return;

        textMesh.ForceMeshUpdate();
        Vector2 textSize = textMesh.GetRenderedValues(false);
        bgRenderer.transform.localScale = new Vector3(
            textSize.x + paddingX * 2f,
            textSize.y + paddingY * 2f,
            1f
        );
    }

    private void SetAlpha(float alpha)
    {
        if (textMesh != null)
        {
            Color c = textColor;
            c.a = alpha;
            textMesh.color = c;
        }

        if (bgRenderer != null)
        {
            Color c = backgroundColor;
            c.a = backgroundColor.a * alpha;
            bgRenderer.color = c;
        }
    }
}
