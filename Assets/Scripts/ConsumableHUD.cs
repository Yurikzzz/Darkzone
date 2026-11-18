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
    [SerializeField] private Vector2 panelPosition = new Vector2(0f, 54f); // Bottom center (1920x1080)
    [SerializeField] private Vector2 slotSize = new Vector2(96f, 96f);
    [SerializeField] private float slotSpacing = 18f;

    private readonly List<SlotUI> slots = new List<SlotUI>();
    private GameObject hudRoot;
    private TextMeshProUGUI coinText;

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
            PlayerInventory.Instance.OnCoinsChanged += HandleCoinsChanged;
        }
        RefreshHUD();
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= RefreshHUD;
            PlayerInventory.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    private void HandleCoinsChanged(int newCoins)
    {
        if (coinText != null)
            coinText.text = newCoins.ToString();
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
            keyRt.offsetMin = new Vector2(6f, 0f);
            keyRt.offsetMax = new Vector2(-6f, -6f);

            TextMeshProUGUI keyTmp = keyObj.AddComponent<TextMeshProUGUI>();
            keyTmp.text = keyLabels[i];
            keyTmp.fontSize = 24f;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = new Color(0.9f, 0.75f, 0.3f, 0.95f);
            keyTmp.alignment = TextAlignmentOptions.TopLeft;

            // Name Hint (bottom)
            GameObject nameObj = new GameObject("NameHint");
            nameObj.transform.SetParent(slotObj.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0.5f);
            nameRt.offsetMin = new Vector2(3f, 3f);
            nameRt.offsetMax = new Vector2(-3f, 0f);

            TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "";
            nameTmp.fontSize = 18f;
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

        BuildCoinCounter();
    }

    private void BuildCoinCounter()
    {
        GameObject coinObj = new GameObject("CoinCounter");
        coinObj.transform.SetParent(hudRoot.transform, false);

        RectTransform coinRt = coinObj.AddComponent<RectTransform>();
        coinRt.sizeDelta = new Vector2(168f, slotSize.y);

        Image bg = coinObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);

        GameObject iconObj = new GameObject("CoinIcon");
        iconObj.transform.SetParent(coinObj.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.18f);
        iconRt.anchorMax = new Vector2(0f, 0.82f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(15f, 0f);
        iconRt.sizeDelta = new Vector2(54f, 0f);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = CoinPickup.GetCoinSprite();
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        GameObject textObj = new GameObject("CoinCount");
        textObj.transform.SetParent(coinObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(72f, 0f);
        textRt.offsetMax = new Vector2(-12f, 0f);

        coinText = textObj.AddComponent<TextMeshProUGUI>();
        coinText.text = "0";
        coinText.fontSize = 32f;
        coinText.fontStyle = FontStyles.Bold;
        coinText.color = new Color(1f, 0.88f, 0.35f, 1f);
        coinText.alignment = TextAlignmentOptions.MidlineRight;
    }

    public void RefreshHUD()
    {
        if (PlayerInventory.Instance == null) return;

        if (coinText != null)
            coinText.text = PlayerInventory.Instance.Coins.ToString();

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
