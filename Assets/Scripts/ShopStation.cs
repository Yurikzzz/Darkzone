using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placed on the Shop object in the safe zone.
/// Manages proximity interaction, randomized stock refreshed every N Darkzone runs,
/// and purchase execution via PlayerInventory.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShopStation : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Key the player presses to open the shop menu.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Prompt displayed above the shop when player is in range.")]
    [SerializeField] private string promptText = "[E] Open Shop";

    [Header("Catalog & Restock Settings")]
    [Tooltip("Catalog defining the item pool, category counts, and default restock interval.")]
    [SerializeField] private ShopCatalogData catalog;

    [Tooltip("Override restock interval in runs (0 uses the catalog's Runs Per Restock value).")]
    [Min(0)]
    [SerializeField] private int overrideRunsPerRestock = 0;

    [Tooltip("Roll initial shop stock automatically on Awake.")]
    [SerializeField] private bool autoRefreshOnAwake = true;

    private readonly List<ShopListingRuntime> currentListings = new List<ShopListingRuntime>();
    private int runsSinceLastRefresh;
    private DarkzoneManager boundDarkzoneManager;
    private bool playerInRange;
    private InteractionPromptUI promptUI;

    public event Action OnStockChanged;

    public ShopCatalogData Catalog => catalog;
    public IReadOnlyList<ShopListingRuntime> CurrentListings => currentListings;
    public int EffectiveRunsPerRestock => overrideRunsPerRestock > 0
        ? overrideRunsPerRestock
        : (catalog != null ? Mathf.Max(1, catalog.runsPerRestock) : 2);
    public int RunsSinceLastRefresh => runsSinceLastRefresh;
    public int RunsUntilRestock => Mathf.Max(1, EffectiveRunsPerRestock - runsSinceLastRefresh);

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        promptUI = GetComponent<InteractionPromptUI>();
        if (promptUI == null)
            promptUI = gameObject.AddComponent<InteractionPromptUI>();

        promptUI.PromptText = promptText;
        promptUI.SetPrompt(interactKey, "OPEN MENU // SHOP", "SHOP");

        if (autoRefreshOnAwake && catalog != null)
            RefreshStock();
    }

    private void Start()
    {
        TrySubscribeDarkzone();
    }

    private void OnDestroy()
    {
        if (boundDarkzoneManager != null)
        {
            boundDarkzoneManager.OnRunCompleted -= HandleRunCompleted;
            boundDarkzoneManager = null;
        }
    }

    private void TrySubscribeDarkzone()
    {
        if (DarkzoneManager.Instance == null || boundDarkzoneManager == DarkzoneManager.Instance)
            return;

        if (boundDarkzoneManager != null)
            boundDarkzoneManager.OnRunCompleted -= HandleRunCompleted;

        boundDarkzoneManager = DarkzoneManager.Instance;
        boundDarkzoneManager.OnRunCompleted += HandleRunCompleted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen && ShopUI.Instance.ActiveStation == this)
                ShopUI.Instance.Close();
        }
    }

    private void Update()
    {
        if (boundDarkzoneManager == null && DarkzoneManager.Instance != null)
            TrySubscribeDarkzone();

        bool shopOpen = ShopUI.Instance != null && ShopUI.Instance.IsOpen;
        bool loadoutOpen = LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen;

        if (promptUI != null)
            promptUI.SetSuppressed(shopOpen || loadoutOpen);

        if (!playerInRange || loadoutOpen) return;

        if (Input.GetKeyDown(interactKey) && ShopUI.Instance != null)
            ShopUI.Instance.Toggle(this);
    }

    /// <summary>
    /// Rolls a fresh randomized stock set from the assigned ShopCatalogData.
    /// </summary>
    public void RefreshStock()
    {
        currentListings.Clear();
        if (catalog != null)
            currentListings.AddRange(catalog.RollListings());

        OnStockChanged?.Invoke();
    }

    private void HandleRunCompleted(int totalRuns)
    {
        runsSinceLastRefresh++;
        if (runsSinceLastRefresh >= EffectiveRunsPerRestock)
        {
            runsSinceLastRefresh = 0;
            RefreshStock();
        }
        else
        {
            OnStockChanged?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to purchase <paramref name="quantity"/> units of <paramref name="listing"/>.
    /// Deducts coins, equips into an empty slot (or stores in stash), and decrements stock.
    /// </summary>
    public bool TryPurchase(ShopListingRuntime listing, int quantity = 1)
    {
        if (listing == null || quantity < 1 || listing.itemAsset == null)
            return false;

        if (!currentListings.Contains(listing))
            return false;

        if (!listing.IsUnlimited && listing.remainingStock < quantity)
            return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null)
            return false;

        int totalCost = listing.price * quantity;
        if (!inv.SpendCoins(totalCost))
            return false;

        inv.AddPurchasedItem(listing.itemAsset, quantity);

        if (!listing.IsUnlimited)
            listing.remainingStock = Mathf.Max(0, listing.remainingStock - quantity);

        OnStockChanged?.Invoke();
        return true;
    }
}
