using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the screen-space Interaction Prompt panel under HUD Canvas/HUDRoot.
/// Tracks active unsuppressed InteractionPromptUI triggers in range and displays
/// the nearest one to the player using the HUDUI.pen layout.
/// </summary>
public class HUDInteractionPromptUI : MonoBehaviour
{
    private static HUDInteractionPromptUI instance;

    public static HUDInteractionPromptUI Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<HUDInteractionPromptUI>(FindObjectsInactive.Include);
            return instance;
        }
    }

    private static readonly List<InteractionPromptUI> activePrompts = new List<InteractionPromptUI>();

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image accentBarImage;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI actionLabelText;
    [SerializeField] private TextMeshProUGUI targetNameText;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.12f;

    private float currentAlpha;
    private Transform cachedPlayer;
    private RectTransform panelRect;
    private RectTransform parentRect;
    private Canvas rootCanvas;
    private Vector3 lastTargetWorldPos;
    private bool hasTargetWorldPos;
    private string lastKey;
    private string lastActionLabel;
    private string lastTargetName;

    public static void Register(InteractionPromptUI prompt)
    {
        if (prompt == null || activePrompts.Contains(prompt))
            return;

        activePrompts.Add(prompt);
    }

    public static void Unregister(InteractionPromptUI prompt)
    {
        if (prompt == null)
            return;

        activePrompts.Remove(prompt);
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;

        if (panelRoot != null)
        {
            if (canvasGroup == null)
                canvasGroup = panelRoot.GetComponent<CanvasGroup>();

            panelRect = panelRoot.GetComponent<RectTransform>();
            parentRect = panelRect != null ? panelRect.parent as RectTransform : null;
            rootCanvas = panelRoot.GetComponentInParent<Canvas>(true);

            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0f);
            }

            panelRoot.SetActive(false);
        }

        currentAlpha = 0f;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (panelRoot == null)
            return;

        InteractionPromptUI best = GetBestActivePrompt();
        bool shouldShow = best != null;
        float targetAlpha = shouldShow ? 1f : 0f;

        if (shouldShow)
        {
            if (!panelRoot.activeSelf)
                panelRoot.SetActive(true);

            ApplyPromptText(best);
            lastTargetWorldPos = best.transform.position + (Vector3)best.Offset;
            hasTargetWorldPos = true;
            PositionAboveWorldTarget(lastTargetWorldPos);
        }
        else if (hasTargetWorldPos && panelRoot.activeSelf)
        {
            PositionAboveWorldTarget(lastTargetWorldPos);
        }

        if (fadeDuration > 0f)
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime / fadeDuration);
        else
            currentAlpha = targetAlpha;

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;

        if (!shouldShow && currentAlpha <= 0.01f)
        {
            currentAlpha = 0f;
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            if (panelRoot.activeSelf)
                panelRoot.SetActive(false);

            hasTargetWorldPos = false;
            lastKey = null;
            lastActionLabel = null;
            lastTargetName = null;
        }
    }

    private void PositionAboveWorldTarget(Vector3 worldPos)
    {
        Camera worldCam = Camera.main;
        if (worldCam == null || panelRect == null || parentRect == null)
            return;

        Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f)
            return;

        Camera uiCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out Vector2 localPos))
        {
            panelRect.anchoredPosition = localPos;
        }
    }

    private InteractionPromptUI GetBestActivePrompt()
    {
        InteractionPromptUI best = null;
        float bestDistSq = float.MaxValue;
        Transform player = GetPlayerTransform();

        for (int i = activePrompts.Count - 1; i >= 0; i--)
        {
            InteractionPromptUI p = activePrompts[i];
            if (p == null || !p.IsShowing)
            {
                if (p == null || !p.isActiveAndEnabled)
                    activePrompts.RemoveAt(i);
                continue;
            }

            if (player == null)
            {
                best = p;
                continue;
            }

            float distSq = ((Vector2)p.transform.position - (Vector2)player.position).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = p;
            }
        }

        return best;
    }

    private Transform GetPlayerTransform()
    {
        if (cachedPlayer == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                cachedPlayer = playerObj.transform;
        }
        return cachedPlayer;
    }

    private void ApplyPromptText(InteractionPromptUI prompt)
    {
        string k = string.IsNullOrEmpty(prompt.KeyLabel) ? "E" : prompt.KeyLabel;
        string act = string.IsNullOrEmpty(prompt.ActionLabel) ? "INTERACT // OBJECT" : prompt.ActionLabel;
        string tgt = string.IsNullOrEmpty(prompt.TargetLabel) ? "INTERACT" : prompt.TargetLabel;

        bool changed = k != lastKey || act != lastActionLabel || tgt != lastTargetName;
        if (!changed)
            return;

        lastKey = k;
        lastActionLabel = act;
        lastTargetName = tgt;

        if (keyText != null)
            keyText.text = k;
        if (actionLabelText != null)
            actionLabelText.text = act;
        if (targetNameText != null)
            targetNameText.text = tgt;

        RectTransform rt = panelRoot.GetComponent<RectTransform>();
        if (rt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}
