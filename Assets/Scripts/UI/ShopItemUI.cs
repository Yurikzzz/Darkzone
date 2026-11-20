using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI card representing a single item listing inside one of the ShopMenu columns.
/// Supports left-click selection and right-click inspection.
/// </summary>
public class ShopItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Card Chrome")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image activeBarImage;
    [SerializeField] private Image iconBoxBorderImage;
    [SerializeField] private Image iconBoxFillImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text Elements")]
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI stockText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI metaText;
    [SerializeField] private Image coinIconImage;
    [SerializeField] private TextMeshProUGUI priceText;

    // Exact HUDUI.pen Palette
    private static readonly Color BgSurface = Hex("#0F1114");
    private static readonly Color BgInset = Hex("#090A0C");
    private static readonly Color BgSelected = Hex("#16140F");
    private static readonly Color BorderSubtle = Hex("#23272D");
    private static readonly Color TextPrimary = Hex("#F1F3F5");
    private static readonly Color TextSecondary = Hex("#8A919A");
    private static readonly Color TextMuted = Hex("#4F555E");
    private static readonly Color AccentGreen = Hex("#22C55E");
    private static readonly Color AccentCoin = Hex("#C8A44D");

    private ShopListingRuntime boundListing;
    private Action<ShopListingRuntime> onSelectCallback;
    private Action<ShopListingRuntime> onInspectCallback;

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (iconImage != null)
            iconImage.raycastTarget = false;
    }

    public void Bind(
        ShopListingRuntime listing,
        bool isSelected,
        Sprite fallbackIcon,
        Action<ShopListingRuntime> onSelect,
        Action<ShopListingRuntime> onInspect)
    {
        boundListing = listing;
        onSelectCallback = onSelect;
        onInspectCallback = onInspect;

        if (listing == null)
            return;

        bool soldOut = listing.IsSoldOut;

        if (canvasGroup != null)
            canvasGroup.alpha = soldOut ? 0.48f : 1f;

        if (borderImage != null)
            borderImage.color = isSelected ? AccentCoin : BorderSubtle;

        if (fillImage != null)
            fillImage.color = isSelected ? BgSelected : BgSurface;

        if (activeBarImage != null)
            activeBarImage.color = isSelected ? AccentCoin : BorderSubtle;

        if (iconBoxBorderImage != null)
            iconBoxBorderImage.color = BorderSubtle;

        if (iconBoxFillImage != null)
            iconBoxFillImage.color = BgInset;

        if (categoryText != null)
        {
            categoryText.text = listing.GetCategoryTag();
            categoryText.color = (!soldOut && isSelected) ? AccentCoin : TextMuted;
        }

        if (stockText != null)
        {
            if (soldOut)
            {
                stockText.text = "SOLD OUT";
                stockText.color = TextMuted;
            }
            else
            {
                stockText.text = listing.IsUnlimited ? "STOCK: ∞" : $"STOCK: {listing.remainingStock}";
                stockText.color = isSelected ? AccentCoin : AccentGreen;
            }
        }

        if (nameText != null)
        {
            nameText.text = listing.GetDisplayName();
            nameText.color = soldOut ? TextSecondary : TextPrimary;
        }

        if (metaText != null)
        {
            metaText.text = listing.GetMetaText();
            metaText.color = soldOut ? TextMuted : TextSecondary;
        }

        Color priceColor = soldOut ? TextMuted : AccentCoin;
        if (coinIconImage != null)
            coinIconImage.color = priceColor;

        if (priceText != null)
        {
            priceText.text = listing.price.ToString("N0", CultureInfo.InvariantCulture);
            priceText.color = priceColor;
        }

        if (iconImage != null)
        {
            Sprite itemSprite = listing.GetIconSprite(out Color itemTint);
            if (itemSprite != null)
            {
                iconImage.sprite = itemSprite;
                iconImage.color = soldOut
                    ? new Color(itemTint.r, itemTint.g, itemTint.b, 0.7f)
                    : itemTint;
                iconImage.enabled = true;
            }
            else if (fallbackIcon != null)
            {
                iconImage.sprite = fallbackIcon;
                iconImage.color = soldOut ? TextMuted : (isSelected ? TextPrimary : TextSecondary);
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundListing == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onSelectCallback?.Invoke(boundListing);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            onSelectCallback?.Invoke(boundListing);
            onInspectCallback?.Invoke(boundListing);
        }
    }
}
