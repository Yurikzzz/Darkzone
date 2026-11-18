using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages drag-and-drop state, top-level ghost rendering, slot highlighting,
/// and invalid-drop snap-back for the Loadout & Inventory UI.
/// </summary>
public class InventoryDragController : MonoBehaviour
{
    [Header("Drag Layer & Ghost")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private RectTransform dragGhost;
    [SerializeField] private Image dragGhostBorder;
    [SerializeField] private Image dragGhostInner;
    [SerializeField] private Image dragGhostIcon;
    [SerializeField] private TextMeshProUGUI dragGhostLabel;
    [SerializeField] private LoadoutUI loadoutUI;

    [Header("HUDUI Unified Theme")]
    [SerializeField] private Color ghostBorderColor = new Color(34f / 255f, 197f / 255f, 94f / 255f, 1f);   // #22C55E
    [SerializeField] private Color ghostInnerColor = new Color(15f / 255f, 17f / 255f, 20f / 255f, 0.96f); // #0F1114 @ 96%
    [SerializeField] private Color ghostLabelColor = new Color(241f / 255f, 243f / 255f, 245f / 255f, 1f); // #F1F3F5

    [Header("Snap-Back Animation")]
    [SerializeField] private float snapBackDuration = 0.12f;

    private Coroutine snapBackCoroutine;

    public ScriptableObject DraggedItem { get; private set; }
    public LoadoutSlotUI SourceLoadoutSlot { get; private set; }
    public RectTransform SourceRect { get; private set; }
    public bool DropHandled { get; private set; }
    public bool IsDragging => DraggedItem != null;

    private void Awake()
    {
        EnsureNonBlockingLayer();
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    private void EnsureNonBlockingLayer()
    {
        if (dragLayer != null)
        {
            CanvasGroup cg = dragLayer.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = dragLayer.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        if (dragGhost != null)
        {
            Graphic[] graphics = dragGhost.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }
    }

    public void BeginDrag(
        ScriptableObject item,
        Sprite icon,
        Color iconColor,
        string label,
        RectTransform sourceRect,
        LoadoutSlotUI sourceSlot,
        PointerEventData eventData)
    {
        if (item == null || dragLayer == null || dragGhost == null)
            return;

        if (snapBackCoroutine != null)
        {
            StopCoroutine(snapBackCoroutine);
            snapBackCoroutine = null;
        }

        EnsureNonBlockingLayer();

        DraggedItem = item;
        SourceRect = sourceRect;
        SourceLoadoutSlot = sourceSlot;
        DropHandled = false;

        if (dragGhostBorder != null)
            dragGhostBorder.color = ghostBorderColor;
        if (dragGhostInner != null)
            dragGhostInner.color = ghostInnerColor;

        if (dragGhostIcon != null)
        {
            dragGhostIcon.sprite = icon;
            dragGhostIcon.color = iconColor;
            dragGhostIcon.enabled = icon != null || item is ConsumableData;
        }

        if (dragGhostLabel != null)
        {
            dragGhostLabel.text = label;
            dragGhostLabel.color = ghostLabelColor;
        }

        dragLayer.SetAsLastSibling();
        dragGhost.gameObject.SetActive(true);
        UpdateDrag(eventData);

        if (loadoutUI != null)
            loadoutUI.HighlightCompatibleSlots(item, true);
    }

    public void UpdateDrag(PointerEventData eventData)
    {
        if (!IsDragging || dragLayer == null || dragGhost == null || eventData == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPos))
        {
            dragGhost.anchoredPosition = localPos;
        }
    }

    public void MarkDropHandled()
    {
        DropHandled = true;
        DraggedItem = null;
        SourceLoadoutSlot = null;
        SourceRect = null;

        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        if (loadoutUI != null)
            loadoutUI.HighlightCompatibleSlots(null, false);
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (loadoutUI != null)
            loadoutUI.HighlightCompatibleSlots(null, false);

        // If MarkDropHandled() already completed and cleaned up the drop during OnDrop, nothing left to do.
        if (DropHandled && DraggedItem == null)
        {
            DropHandled = false;
            return;
        }

        bool wasHandled = DropHandled;
        RectTransform returnTarget = SourceRect;

        DraggedItem = null;
        SourceLoadoutSlot = null;
        SourceRect = null;
        DropHandled = false;

        if (dragGhost == null)
            return;

        if (!wasHandled && returnTarget != null && dragGhost.gameObject.activeSelf && gameObject.activeInHierarchy)
        {
            snapBackCoroutine = StartCoroutine(SnapBackRoutine(returnTarget.position));
        }
        else
        {
            dragGhost.gameObject.SetActive(false);
        }
    }

    public void CancelDragImmediate()
    {
        if (snapBackCoroutine != null)
        {
            StopCoroutine(snapBackCoroutine);
            snapBackCoroutine = null;
        }

        DraggedItem = null;
        SourceLoadoutSlot = null;
        SourceRect = null;
        DropHandled = false;

        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        if (loadoutUI != null)
            loadoutUI.HighlightCompatibleSlots(null, false);
    }

    private IEnumerator SnapBackRoutine(Vector3 targetWorldPos)
    {
        Vector3 startPos = dragGhost.position;
        float duration = Mathf.Max(0.01f, snapBackDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            dragGhost.position = Vector3.Lerp(startPos, targetWorldPos, ease);
            yield return null;
        }

        dragGhost.gameObject.SetActive(false);
        snapBackCoroutine = null;
    }
}
