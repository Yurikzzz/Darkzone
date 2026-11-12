using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays quick-slot consumable indicators on the HUD (Slots 1-4, Keys 3, 4, 5, 6).
/// Automatically updates when inventory changes.
/// </summary>
public class ConsumableHUD : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private Vector2 panelPosition = new Vector2(0f, 18f); // Bottom center
    [SerializeField] private Vector2 slotSize = new Vector2(32f, 32f);
    [SerializeField] private float slotSpacing = 6f;

    private readonly List<SlotUI> slots = new List<SlotUI>();
    private GameObject hudRoot;

    private class SlotUI
    {
        public Image background;
        public Image icon;
        public TextMeshProUGUI keyText;
        public TextMeshProUGUI nameText;
    }

    private void Awake()
    {
        BuildHUD();
    }

    private void Start()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged += RefreshHUD;
        }
        RefreshHUD();
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= RefreshHUD;
        }
    }

    private void LateUpdate()
    {
        if (hudRoot == null) return;

        // When loadout menu is open, hide the HUD consumables bar so it is covered by the menu
        bool isMenuOpen = LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen;
        if (hudRoot.activeSelf == isMenuOpen)
        {
            hudRoot.SetActive(!isMenuOpen);
        }
    }

    private void BuildHUD()
    {
        // Container anchored at bottom center of canvas
        hudRoot = new GameObject("ConsumablesBar");
        hudRoot.transform.SetParent(transform, false);

        // Ensure ConsumablesBar is rendered underneath modals like LoadoutMenu
        hudRoot.transform.SetSiblingIndex(2);

        RectTransform rootRt = hudRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = panelPosition;

        HorizontalLayoutGroup hlg = hudRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = slotSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        string[] keyLabels = new string[] { "3", "4", "5", "6" };

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = new GameObject($"Slot_{i + 1}");
            slotObj.transform.SetParent(hudRoot.transform, false);

            RectTransform slotRt = slotObj.AddComponent<RectTransform>();
            slotRt.sizeDelta = slotSize;

            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);

            // Icon Image
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.15f, 0.15f);
            iconRt.anchorMax = new Vector2(0.85f, 0.85f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.clear;
            iconImg.preserveAspect = true;

            // Key Hint (top-left)
            GameObject keyObj = new GameObject("KeyHint");
            keyObj.transform.SetParent(slotObj.transform, false);
            RectTransform keyRt = keyObj.AddComponent<RectTransform>();
            keyRt.anchorMin = new Vector2(0f, 0.5f);
            keyRt.anchorMax = new Vector2(1f, 1f);
            keyRt.offsetMin = new Vector2(2f, 0f);
            keyRt.offsetMax = new Vector2(-2f, -2f);

            TextMeshProUGUI keyTmp = keyObj.AddComponent<TextMeshProUGUI>();
            keyTmp.text = keyLabels[i];
            keyTmp.fontSize = 8f;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = new Color(0.9f, 0.75f, 0.3f, 0.95f);
            keyTmp.alignment = TextAlignmentOptions.TopLeft;

            // Name Hint (bottom)
            GameObject nameObj = new GameObject("NameHint");
            nameObj.transform.SetParent(slotObj.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0.5f);
            nameRt.offsetMin = new Vector2(1f, 1f);
            nameRt.offsetMax = new Vector2(-1f, 0f);

            TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "";
            nameTmp.fontSize = 6f;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;

            slots.Add(new SlotUI
            {
                background = bg,
                icon = iconImg,
                keyText = keyTmp,
                nameText = nameTmp
            });
        }
    }

    public void RefreshHUD()
    {
        if (PlayerInventory.Instance == null) return;

        var equipped = PlayerInventory.Instance.EquippedConsumables;
        for (int i = 0; i < slots.Count; i++)
        {
            ConsumableData item = (equipped != null && i < equipped.Count) ? equipped[i] as ConsumableData : null;
            SlotUI slot = slots[i];

            if (item != null)
            {
                slot.background.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);
                slot.icon.sprite = item.icon;
                slot.icon.color = item.tintColor;
                slot.icon.enabled = true;
                slot.nameText.text = item.consumableName;
            }
            else
            {
                slot.background.color = new Color(0.08f, 0.10f, 0.14f, 0.6f);
                slot.icon.color = Color.clear;
                slot.icon.enabled = false;
                slot.nameText.text = "";
            }
        }
    }
}
