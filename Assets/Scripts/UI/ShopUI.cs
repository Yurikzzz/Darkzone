using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the authored uGUI Black Market // Armory Shop modal window.
/// Mirrors the HUDUI.pen template: 3 scrollable category columns (Weapons, Equipment, Consumables),
/// live credits balance, restock countdown badge, quantity stepper, in-stock filter, and RMB inspect panel.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Serializable]
    public class ColumnWidgets
    {
        public TextMeshProUGUI sectionCountText;
        public ScrollRect scrollRect;
        public RectTransform contentRoot;
        public GameObject emptyNoticeObj;
        public TextMeshProUGUI emptyNoticeText;
    }

    [Header("Root & Header")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TextMeshProUGUI stockTagText;
    [SerializeField] private TextMeshProUGUI balanceValueText;
    [SerializeField] private Button closeButton;

    [Header("Shop Columns")]
    [SerializeField] private ColumnWidgets weaponsColumn;
    [SerializeField] private ColumnWidgets equipmentColumn;
    [SerializeField] private ColumnWidgets consumablesColumn;
    [SerializeField] private ShopItemUI shopItemPrefab;

    [Header("Fallback Category Icons")]
    [SerializeField] private Sprite weaponsFallbackIcon;
    [SerializeField] private Sprite equipmentFallbackIcon;
    [SerializeField] private Sprite consumablesFallbackIcon;

    [Header("Footer Controls")]
    [SerializeField] private TextMeshProUGUI selectedNameText;
    [SerializeField] private TextMeshProUGUI afterPurchaseText;
    [SerializeField] private Button inspectHintButton;
    [SerializeField] private Button inStockOnlyButton;
    [SerializeField] private Image inStockOnlyKeyBorder;
    [SerializeField] private TextMeshProUGUI inStockOnlyKeyText;
    [SerializeField] private TextMeshProUGUI inStockOnlyLabel;
    [SerializeField] private Button qtyDecreaseButton;
    [SerializeField] private Image qtyDecreaseBorder;
    [SerializeField] private Image qtyDecreaseIcon;
    [SerializeField] private TextMeshProUGUI qtyValueText;
    [SerializeField] private Button qtyIncreaseButton;
    [SerializeField] private Image qtyIncreaseBorder;
    [SerializeField] private Image qtyIncreaseIcon;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Image purchaseButtonImage;
    [SerializeField] private Image purchaseIcon;
    [SerializeField] private TextMeshProUGUI purchaseLabelText;
    [SerializeField] private Image purchaseDivider;
    [SerializeField] private Image purchaseCoinIcon;
    [SerializeField] private TextMeshProUGUI purchasePriceText;

    [Header("Inspect Overlay")]
    [SerializeField] private GameObject inspectOverlayRoot;
    [SerializeField] private Button inspectDismissButton;
    [SerializeField] private TextMeshProUGUI inspectTitleText;
    [SerializeField] private TextMeshProUGUI inspectCategoryText;
    [SerializeField] private TextMeshProUGUI inspectPriceStockText;
    [SerializeField] private TextMeshProUGUI inspectDetailsText;
    [SerializeField] private Button inspectCloseButton;

    // Exact HUDUI.pen Palette
    private static readonly Color BgCanvas = Hex("#070809");
    private static readonly Color BgSurface = Hex("#0F1114");
    private static readonly Color BorderSubtle = Hex("#23272D");
    private static readonly Color BorderDim = Hex("#191C21");
    private static readonly Color TextPrimary = Hex("#F1F3F5");
    private static readonly Color TextSecondary = Hex("#8A919A");
    private static readonly Color TextMuted = Hex("#4F555E");
    private static readonly Color AccentCoin = Hex("#C8A44D");
    private static readonly Color DangerText = Hex("#EF4444");

    private ShopStation activeStation;
    private ShopListingRuntime selectedListing;
    private int selectedQuantity = 1;
    private bool inStockOnly;
    private int openedFrame = -1;
    private PlayerMovement playerMovement;
    private PlayerInventory subscribedInventory;
    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
    public ShopStation ActiveStation => activeStation;
    public ShopListingRuntime SelectedListing => selectedListing;
    public int SelectedQuantity => selectedQuantity;
    public bool InStockOnly => inStockOnly;
    public bool IsInspectOpen => inspectOverlayRoot != null && inspectOverlayRoot.activeSelf;

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        WireButtons();

        transform.SetAsLastSibling();
        if (inspectOverlayRoot != null)
            inspectOverlayRoot.SetActive(false);
        if (modalRoot != null)
            modalRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsInspectOpen)
                CloseInspect();
            else
                Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleInStockOnly();
        }
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (inspectHintButton != null)
        {
            inspectHintButton.onClick.RemoveAllListeners();
            inspectHintButton.onClick.AddListener(() =>
            {
                if (selectedListing != null)
                    OpenInspect(selectedListing);
            });
        }

        if (inStockOnlyButton != null)
        {
            inStockOnlyButton.onClick.RemoveAllListeners();
            inStockOnlyButton.onClick.AddListener(ToggleInStockOnly);
        }

        if (qtyDecreaseButton != null)
        {
            qtyDecreaseButton.onClick.RemoveAllListeners();
            qtyDecreaseButton.onClick.AddListener(() => AdjustQuantity(-1));
        }

        if (qtyIncreaseButton != null)
        {
            qtyIncreaseButton.onClick.RemoveAllListeners();
            qtyIncreaseButton.onClick.AddListener(() => AdjustQuantity(1));
        }

        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(HandlePurchaseClicked);
        }

        if (inspectDismissButton != null)
        {
            inspectDismissButton.onClick.RemoveAllListeners();
            inspectDismissButton.onClick.AddListener(CloseInspect);
        }

        if (inspectCloseButton != null)
        {
            inspectCloseButton.onClick.RemoveAllListeners();
            inspectCloseButton.onClick.AddListener(CloseInspect);
        }
    }

    public void Open(ShopStation station)
    {
        if (station == null) return;

        if (LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen)
            LoadoutUI.Instance.Close();

        UnsubscribeEvents();

        activeStation = station;
        openedFrame = Time.frameCount;
        selectedListing = null;
        selectedQuantity = 1;

        transform.SetAsLastSibling();

        if (inspectOverlayRoot != null)
            inspectOverlayRoot.SetActive(false);

        if (modalRoot != null)
            modalRoot.SetActive(true);

        if (playerMovement == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerMovement = player.GetComponent<PlayerMovement>();
        }

        if (playerMovement != null)
            playerMovement.CanMove = false;

        SubscribeEvents();
        ResetScrollPositions();
        RefreshUI();
    }

    public void Close()
    {
        CloseInspect();
        UnsubscribeEvents();
        activeStation = null;
        selectedListing = null;
        selectedQuantity = 1;

        if (modalRoot != null)
            modalRoot.SetActive(false);

        if (playerMovement != null)
            playerMovement.CanMove = true;
    }

    public void Toggle(ShopStation station)
    {
        if (IsOpen)
        {
            if (Time.frameCount != openedFrame)
                Close();
        }
        else
        {
            Open(station);
        }
    }

    public void ToggleInStockOnly()
    {
        inStockOnly = !inStockOnly;
        RefreshUI();
    }

    public void SetInStockOnly(bool value)
    {
        if (inStockOnly == value) return;
        inStockOnly = value;
        RefreshUI();
    }

    public void SelectListing(ShopListingRuntime listing)
    {
        if (listing == null) return;
        selectedListing = listing;
        selectedQuantity = 1;
        RefreshUI();
    }

    public void AdjustQuantity(int delta)
    {
        if (selectedListing == null || delta == 0) return;

        int maxQty = GetMaxSelectableQuantity(selectedListing);
        int next = Mathf.Clamp(selectedQuantity + delta, 1, maxQty);
        if (next == selectedQuantity) return;

        selectedQuantity = next;
        RefreshFooter();
    }

    public void OpenInspect(ShopListingRuntime listing)
    {
        if (listing == null || inspectOverlayRoot == null) return;

        if (inspectTitleText != null)
            inspectTitleText.text = listing.GetDisplayName();

        if (inspectCategoryText != null)
            inspectCategoryText.text = listing.GetCategoryTag();

        if (inspectPriceStockText != null)
        {
            string stockPart = listing.IsSoldOut
                ? "SOLD OUT"
                : (listing.IsUnlimited ? "STOCK: UNLIMITED" : $"STOCK: {listing.remainingStock}");
            inspectPriceStockText.text = $"{listing.price.ToString("N0", CultureInfo.InvariantCulture)} CREDITS  •  {stockPart}";
        }

        if (inspectDetailsText != null)
            inspectDetailsText.text = listing.GetInspectDetails();

        inspectOverlayRoot.SetActive(true);
        inspectOverlayRoot.transform.SetAsLastSibling();
    }

    public void CloseInspect()
    {
        if (inspectOverlayRoot != null)
            inspectOverlayRoot.SetActive(false);
    }

    public bool PurchaseSelected()
    {
        if (activeStation == null || selectedListing == null)
            return false;

        int qty = Mathf.Clamp(selectedQuantity, 1, GetMaxSelectableQuantity(selectedListing));
        bool success = activeStation.TryPurchase(selectedListing, qty);
        if (success)
        {
            selectedQuantity = 1;
            if (IsInspectOpen)
                OpenInspect(selectedListing);
            RefreshUI();
        }
        return success;
    }

    private void HandlePurchaseClicked()
    {
        PurchaseSelected();
    }

    private void SubscribeEvents()
    {
        if (activeStation != null)
            activeStation.OnStockChanged += HandleStockChanged;

        if (PlayerInventory.Instance != null)
        {
            subscribedInventory = PlayerInventory.Instance;
            subscribedInventory.OnCoinsChanged += HandleCoinsChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (activeStation != null)
            activeStation.OnStockChanged -= HandleStockChanged;

        if (subscribedInventory != null)
        {
            subscribedInventory.OnCoinsChanged -= HandleCoinsChanged;
            subscribedInventory = null;
        }
    }

    private void HandleStockChanged()
    {
        if (IsOpen)
            RefreshUI();
    }

    private void HandleCoinsChanged(int _)
    {
        if (IsOpen)
            RefreshUI();
    }

    private void ResetScrollPositions()
    {
        if (weaponsColumn != null && weaponsColumn.scrollRect != null)
            weaponsColumn.scrollRect.verticalNormalizedPosition = 1f;
        if (equipmentColumn != null && equipmentColumn.scrollRect != null)
            equipmentColumn.scrollRect.verticalNormalizedPosition = 1f;
        if (consumablesColumn != null && consumablesColumn.scrollRect != null)
            consumablesColumn.scrollRect.verticalNormalizedPosition = 1f;
    }

    public void RefreshUI()
    {
        ClearSpawnedCards();

        IReadOnlyList<ShopListingRuntime> listings = activeStation != null
            ? activeStation.CurrentListings
            : Array.Empty<ShopListingRuntime>();

        EnsureValidSelection(listings);

        RefreshHeader();
        PopulateColumn(ShopCategory.Weapons, weaponsColumn, listings, weaponsFallbackIcon, "NO WEAPONS IN STOCK");
        PopulateColumn(ShopCategory.Equipment, equipmentColumn, listings, equipmentFallbackIcon, "EQUIPMENT COMING SOON");
        PopulateColumn(ShopCategory.Consumables, consumablesColumn, listings, consumablesFallbackIcon, "NO CONSUMABLES IN STOCK");
        RefreshFooter();
    }

    private void EnsureValidSelection(IReadOnlyList<ShopListingRuntime> listings)
    {
        bool selectionValid = false;
        if (selectedListing != null)
        {
            for (int i = 0; i < listings.Count; i++)
            {
                if (listings[i] == selectedListing && (!inStockOnly || !listings[i].IsSoldOut))
                {
                    selectionValid = true;
                    break;
                }
            }
        }

        if (!selectionValid)
        {
            selectedListing = null;
            ShopListingRuntime firstVisible = null;
            for (int i = 0; i < listings.Count; i++)
            {
                ShopListingRuntime item = listings[i];
                if (item == null) continue;
                if (inStockOnly && item.IsSoldOut) continue;

                if (firstVisible == null)
                    firstVisible = item;

                if (!item.IsSoldOut)
                {
                    selectedListing = item;
                    break;
                }
            }

            if (selectedListing == null)
                selectedListing = firstVisible;

            selectedQuantity = 1;
        }
        else
        {
            selectedQuantity = Mathf.Clamp(selectedQuantity, 1, GetMaxSelectableQuantity(selectedListing));
        }
    }

    private void RefreshHeader()
    {
        if (stockTagText != null && activeStation != null)
        {
            int runsLeft = activeStation.RunsUntilRestock;
            stockTagText.text = $"LIMITED STOCK  •  RESTOCK IN {runsLeft} RUN{(runsLeft == 1 ? "" : "S")}";
        }

        int coins = PlayerInventory.Instance != null ? PlayerInventory.Instance.Coins : 0;
        if (balanceValueText != null)
            balanceValueText.text = Mathf.Max(0, coins).ToString("00,000", CultureInfo.InvariantCulture);
    }

    private void PopulateColumn(
        ShopCategory category,
        ColumnWidgets col,
        IReadOnlyList<ShopListingRuntime> listings,
        Sprite fallbackIcon,
        string emptyMessage)
    {
        if (col == null) return;

        int totalInCat = 0;
        int inStockInCat = 0;
        int spawnedCount = 0;

        for (int i = 0; i < listings.Count; i++)
        {
            ShopListingRuntime listing = listings[i];
            if (listing == null || listing.category != category) continue;

            totalInCat++;
            if (!listing.IsSoldOut)
                inStockInCat++;

            if (inStockOnly && listing.IsSoldOut)
                continue;

            if (shopItemPrefab != null && col.contentRoot != null)
            {
                ShopItemUI card = Instantiate(shopItemPrefab, col.contentRoot);
                card.Bind(listing, listing == selectedListing, fallbackIcon, SelectListing, OpenInspect);
                spawnedCards.Add(card.gameObject);
                spawnedCount++;
            }
        }

        if (col.sectionCountText != null)
            col.sectionCountText.text = $"{inStockInCat} / {totalInCat} IN STOCK";

        if (col.emptyNoticeObj != null)
        {
            bool showEmpty = spawnedCount == 0;
            col.emptyNoticeObj.SetActive(showEmpty);
            if (showEmpty && col.emptyNoticeText != null)
            {
                col.emptyNoticeText.text = (totalInCat > 0 && inStockOnly)
                    ? "NO IN-STOCK ITEMS"
                    : emptyMessage;
            }
        }
    }

    private void RefreshFooter()
    {
        int coins = PlayerInventory.Instance != null ? PlayerInventory.Instance.Coins : 0;

        if (inStockOnlyKeyBorder != null)
            inStockOnlyKeyBorder.color = inStockOnly ? AccentCoin : BorderSubtle;
        if (inStockOnlyKeyText != null)
            inStockOnlyKeyText.color = inStockOnly ? AccentCoin : TextPrimary;
        if (inStockOnlyLabel != null)
            inStockOnlyLabel.color = inStockOnly ? AccentCoin : TextMuted;

        if (selectedListing == null)
        {
            if (selectedNameText != null) selectedNameText.text = "NONE";
            if (afterPurchaseText != null)
            {
                afterPurchaseText.text = "SELECT AN ITEM TO PURCHASE";
                afterPurchaseText.color = TextMuted;
            }
            if (qtyValueText != null) qtyValueText.text = "0";
            SetStepperButtonVisuals(qtyDecreaseButton, qtyDecreaseBorder, qtyDecreaseIcon, false);
            SetStepperButtonVisuals(qtyIncreaseButton, qtyIncreaseBorder, qtyIncreaseIcon, false);
            SetPurchaseButtonState(false, 0);
            if (inspectHintButton != null) inspectHintButton.interactable = false;
            return;
        }

        if (inspectHintButton != null)
            inspectHintButton.interactable = true;

        int maxQty = GetMaxSelectableQuantity(selectedListing);
        selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maxQty);
        int totalCost = selectedListing.price * selectedQuantity;
        bool soldOut = selectedListing.IsSoldOut;
        bool enoughStock = selectedListing.IsUnlimited || selectedListing.remainingStock >= selectedQuantity;
        bool enoughCoins = coins >= totalCost;
        bool canBuy = !soldOut && enoughStock && enoughCoins;

        if (selectedNameText != null)
            selectedNameText.text = selectedListing.GetDisplayName();

        if (afterPurchaseText != null)
        {
            if (soldOut)
            {
                afterPurchaseText.text = "OUT OF STOCK";
                afterPurchaseText.color = TextMuted;
            }
            else if (!enoughCoins)
            {
                afterPurchaseText.text = $"INSUFFICIENT CREDITS (NEED {(totalCost - coins).ToString("N0", CultureInfo.InvariantCulture)})";
                afterPurchaseText.color = DangerText;
            }
            else
            {
                int remainingCoins = coins - totalCost;
                afterPurchaseText.text = $"AFTER PURCHASE: {remainingCoins.ToString("N0", CultureInfo.InvariantCulture)} CREDITS";
                afterPurchaseText.color = TextSecondary;
            }
        }

        if (qtyValueText != null)
            qtyValueText.text = soldOut ? "0" : selectedQuantity.ToString(CultureInfo.InvariantCulture);

        bool canDec = !soldOut && selectedQuantity > 1;
        bool canInc = !soldOut && selectedQuantity < maxQty;
        SetStepperButtonVisuals(qtyDecreaseButton, qtyDecreaseBorder, qtyDecreaseIcon, canDec);
        SetStepperButtonVisuals(qtyIncreaseButton, qtyIncreaseBorder, qtyIncreaseIcon, canInc);
        SetPurchaseButtonState(canBuy, totalCost);
    }

    private static int GetMaxSelectableQuantity(ShopListingRuntime listing)
    {
        if (listing == null || listing.IsSoldOut)
            return 1;

        bool isStackableConsumable = listing.itemAsset is ConsumableData c && c.isStackable;
        if (!isStackableConsumable)
            return 1;

        return listing.IsUnlimited ? 99 : Mathf.Max(1, listing.remainingStock);
    }

    private static void SetStepperButtonVisuals(Button btn, Image border, Image icon, bool enabled)
    {
        if (btn != null) btn.interactable = enabled;
        if (border != null) border.color = enabled ? BorderSubtle : BorderDim;
        if (icon != null) icon.color = enabled ? TextPrimary : TextMuted;
    }

    private void SetPurchaseButtonState(bool canBuy, int totalCost)
    {
        if (purchaseButton != null)
            purchaseButton.interactable = canBuy;

        if (purchaseButtonImage != null)
            purchaseButtonImage.color = canBuy ? AccentCoin : BorderSubtle;

        Color contentColor = canBuy ? BgCanvas : TextMuted;
        if (purchaseIcon != null) purchaseIcon.color = contentColor;
        if (purchaseLabelText != null) purchaseLabelText.color = contentColor;
        if (purchaseCoinIcon != null) purchaseCoinIcon.color = contentColor;
        if (purchaseDivider != null)
            purchaseDivider.color = canBuy ? new Color(BgCanvas.r, BgCanvas.g, BgCanvas.b, 0.302f) : TextMuted;
        if (purchasePriceText != null)
        {
            purchasePriceText.color = contentColor;
            purchasePriceText.text = totalCost.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    private void ClearSpawnedCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
            {
                spawnedCards[i].SetActive(false);
                Destroy(spawnedCards[i]);
            }
        }
        spawnedCards.Clear();
    }
}
