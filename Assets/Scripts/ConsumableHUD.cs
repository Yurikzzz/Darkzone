using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;

/// <summary>
/// Drives the bottom-center 02 // CONSUMABLES quick-slots (Keys 3-6) and top-right CREDITS counter
/// from PlayerInventory events.
/// </summary>
public class ConsumableHUD : MonoBehaviour
{
    [Serializable]
    public class ConsumableSlotView
    {
        public TextMeshProUGUI keybindText;
        public TextMeshProUGUI slotLabelText;
        public TextMeshProUGUI countText;
        public TextMeshProUGUI itemNameText;
    }

    [Header("Consumable Quick Slots (1-4 / Keys 3-6)")]
    [SerializeField] private ConsumableSlotView[] slotViews = new ConsumableSlotView[4];

    [Header("Top-Right Coin Counter")]
    [SerializeField] private TextMeshProUGUI coinValueText;

    [Header("HUDUI Palette")]
    [SerializeField] private Color primaryTextColor = new Color(0.945f, 0.953f, 0.961f, 1f);  // #F1F3F5
    [SerializeField] private Color secondaryTextColor = new Color(0.541f, 0.569f, 0.604f, 1f);// #8A919A
    [SerializeField] private Color mutedTextColor = new Color(0.310f, 0.333f, 0.369f, 1f);    // #4F555E
    [SerializeField] private Color accentGreenColor = new Color(0.133f, 0.773f, 0.369f, 1f);  // #22C55E

    private PlayerInventory boundInventory;

    private void OnEnable()
    {
        TrySubscribe();
        RefreshHUD();
    }

    private void Start()
    {
        TrySubscribe();
        RefreshHUD();
    }

    private void OnDestroy()
    {
        if (boundInventory != null)
        {
            boundInventory.OnInventoryChanged -= RefreshHUD;
            boundInventory.OnCoinsChanged -= HandleCoinsChanged;
            boundInventory = null;
        }
    }

    private void Update()
    {
        if (boundInventory == null && PlayerInventory.Instance != null)
        {
            TrySubscribe();
            RefreshHUD();
        }
    }

    private void TrySubscribe()
    {
        if (PlayerInventory.Instance == null || boundInventory == PlayerInventory.Instance)
            return;

        boundInventory = PlayerInventory.Instance;
        boundInventory.OnInventoryChanged += RefreshHUD;
        boundInventory.OnCoinsChanged += HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int newCoins)
    {
        if (coinValueText != null)
            coinValueText.text = FormatCoins(newCoins);
    }

    public void RefreshHUD()
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        if (coinValueText != null)
            coinValueText.text = FormatCoins(inv.Coins);

        var equippedStacks = inv.EquippedConsumableStacks;

        if (slotViews == null) return;

        for (int i = 0; i < slotViews.Length; i++)
        {
            ConsumableSlotView view = slotViews[i];
            if (view == null) continue;

            ConsumableStack stack = (equippedStacks != null && i < equippedStacks.Count) ? equippedStacks[i] : null;
            ConsumableData item = (stack != null && !stack.IsEmpty) ? stack.item : null;

            if (item != null)
            {
                if (view.keybindText != null) view.keybindText.color = primaryTextColor;
                if (view.slotLabelText != null) view.slotLabelText.color = secondaryTextColor;
                if (view.countText != null)
                {
                    if (item.isStackable)
                    {
                        view.countText.gameObject.SetActive(true);
                        view.countText.text = $"{stack.quantity}x";
                        view.countText.color = accentGreenColor;
                    }
                    else
                    {
                        view.countText.text = "";
                        view.countText.gameObject.SetActive(false);
                    }
                }
                if (view.itemNameText != null)
                {
                    view.itemNameText.text = item.consumableName.ToUpperInvariant();
                    view.itemNameText.color = primaryTextColor;
                }
            }
            else
            {
                if (view.keybindText != null) view.keybindText.color = mutedTextColor;
                if (view.slotLabelText != null) view.slotLabelText.color = mutedTextColor;
                if (view.countText != null)
                {
                    view.countText.gameObject.SetActive(true);
                    view.countText.text = "0x";
                    view.countText.color = mutedTextColor;
                }
                if (view.itemNameText != null)
                {
                    view.itemNameText.text = "EMPTY";
                    view.itemNameText.color = mutedTextColor;
                }
            }
        }
    }

    private static string FormatCoins(int coins)
    {
        return Mathf.Max(0, coins).ToString("00,000", CultureInfo.InvariantCulture);
    }
}
