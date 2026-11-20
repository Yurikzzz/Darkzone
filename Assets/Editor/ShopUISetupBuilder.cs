using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility that generates missing Shop UI icons, builds Assets/Prefabs/UI/ShopItemSlot.prefab,
/// creates Assets/Shop/DefaultShopCatalog.asset populated with the project's Weapons and Consumables,
/// builds HUD Canvas/ShopMenu matching the HUDUI.pen template with exact colors,
/// and configures SafeZone/Shop in SampleScene.unity.
/// </summary>
public static class ShopUISetupBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string ShopItemPrefabPath = PrefabFolder + "/ShopItemSlot.prefab";
    private const string ShopFolder = "Assets/Shop";
    private const string DefaultCatalogPath = ShopFolder + "/DefaultShopCatalog.asset";
    private const string SpriteFolder = "Assets/Sprites/UI";
    private const string FontAssetFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";

    // Exact HUDUI.pen Color Tokens
    private static readonly Color BgCanvas = Hex("#070809");
    private static readonly Color BgBackdrop = new Color(BgCanvas.r, BgCanvas.g, BgCanvas.b, 0.85f);
    private static readonly Color BgSurface = Hex("#0F1114");
    private static readonly Color BgInset = Hex("#090A0C");
    private static readonly Color BorderSubtle = Hex("#23272D");
    private static readonly Color BorderDim = Hex("#191C21");
    private static readonly Color TextPrimary = Hex("#F1F3F5");
    private static readonly Color TextSecondary = Hex("#8A919A");
    private static readonly Color TextMuted = Hex("#4F555E");
    private static readonly Color AccentGreen = Hex("#22C55E");
    private static readonly Color AccentCoin = Hex("#C8A44D");
    private static readonly Color StockTagFill = Hex("#221C0D");
    private static readonly Color StockTagStroke = Hex("#6B5523");
    private static readonly Color PurchaseDividerColor = new Color(BgCanvas.r, BgCanvas.g, BgCanvas.b, 0.302f); // #0708094D

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private class ShopAssets
    {
        public TMP_FontAsset uiSemiBold;
        public TMP_FontAsset monoRegular;
        public TMP_FontAsset monoSemiBold;

        public Sprite rounded4Px;
        public Sprite rounded3Px;
        public Sprite rounded1Px;
        public Sprite circleDot;

        public Sprite iconCrosshair;
        public Sprite iconShield;
        public Sprite iconCoins;
        public Sprite iconPackage;
        public Sprite iconShoppingBag;
        public Sprite iconMinus;
        public Sprite iconPlus;
        public Sprite iconClose;
    }

    [MenuItem("Tools/Darkzone/Build Shop UI & Setup Scene")]
    public static void BuildAll()
    {
        EnsureDirectories();

        ShopAssets assets = EnsureAssets();
        ShopCatalogData catalog = EnsureDefaultCatalog();
        ShopItemUI itemPrefab = CreateShopItemPrefab(assets);

        BuildShopMenuHierarchy(itemPrefab, assets);
        ConfigureShopStationInScene(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShopUISetupBuilder] Successfully built ShopItemSlot prefab, DefaultShopCatalog, HUD Canvas/ShopMenu, and configured SafeZone/Shop.");
    }

    private static void EnsureDirectories()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "UI");
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets/Sprites", "UI");
        EnsureFolder("Assets", "Shop");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string combined = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(combined))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static ShopAssets EnsureAssets()
    {
        ShopAssets assets = new ShopAssets();

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/LiberationSans SDF.asset");
        assets.uiSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/Inter-SemiBold SDF.asset") ?? fallback;
        assets.monoRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/JetBrainsMono-Regular SDF.asset") ?? fallback;
        assets.monoSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/JetBrainsMono-SemiBold SDF.asset") ?? assets.monoRegular;

        assets.rounded4Px = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/ui_rounded_4px.png");
        assets.rounded3Px = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/ui_rounded_3px.png");
        assets.rounded1Px = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/ui_rounded_1px.png");
        assets.circleDot = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/ui_circle_dot.png");

        assets.iconCrosshair = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/icon_crosshair.png");
        assets.iconShield = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/icon_shield.png");
        assets.iconCoins = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/icon_coins.png");
        assets.iconClose = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/icon_close.png");

        assets.iconPackage = GenerateSprite("icon_package", 64, 64, DrawPackageIcon);
        assets.iconShoppingBag = GenerateSprite("icon_shopping_bag", 64, 64, DrawShoppingBagIcon);
        assets.iconMinus = GenerateSprite("icon_minus", 64, 64, DrawMinusIcon);
        assets.iconPlus = GenerateSprite("icon_plus", 64, 64, DrawPlusIcon);

        return assets;
    }

    // =====================================================================
    // Default Shop Catalog Asset
    // =====================================================================

    private static ShopCatalogData EnsureDefaultCatalog()
    {
        ShopCatalogData catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogData>(DefaultCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ShopCatalogData>();
            AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
        }

        catalog.runsPerRestock = 2;
        catalog.priceRoundingStep = 10;
        catalog.categoryConfigs = new List<ShopCategoryRollConfig>
        {
            new ShopCategoryRollConfig { category = ShopCategory.Weapons, minListings = 3, maxListings = 4, allowDuplicates = false },
            new ShopCategoryRollConfig { category = ShopCategory.Equipment, minListings = 0, maxListings = 0, allowDuplicates = false },
            new ShopCategoryRollConfig { category = ShopCategory.Consumables, minListings = 2, maxListings = 2, allowDuplicates = false }
        };

        List<ShopPoolEntry> pool = new List<ShopPoolEntry>();
        AddPoolEntry(pool, "Assets/Weapons/Pistol.asset", ShopCategory.Weapons, 250, 1.0f, 1, 2, "SIDEARM // 9MM");
        AddPoolEntry(pool, "Assets/Weapons/SMG.asset", ShopCategory.Weapons, 600, 1.2f, 1, 2, "SMG // 9MM");
        AddPoolEntry(pool, "Assets/Weapons/Shotgun.asset", ShopCategory.Weapons, 750, 1.1f, 1, 2, "SHOTGUN // 12GA");
        AddPoolEntry(pool, "Assets/Weapons/AssaultRifle.asset", ShopCategory.Weapons, 1100, 1.0f, 1, 2, "RIFLE // 5.56MM");
        AddPoolEntry(pool, "Assets/Weapons/SniperRifle.asset", ShopCategory.Weapons, 1400, 0.7f, 1, 1, "SNIPER // .308 WIN");
        AddPoolEntry(pool, "Assets/Consumables/Medkit.asset", ShopCategory.Consumables, 180, 1.5f, 2, 5, "MEDICAL // +50 HP");
        AddPoolEntry(pool, "Assets/Consumables/FragGrenade.asset", ShopCategory.Consumables, 140, 1.2f, 2, 5, "ORDNANCE // HE");

        catalog.itemPool = pool;
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AddPoolEntry(
        List<ShopPoolEntry> pool,
        string assetPath,
        ShopCategory category,
        int basePrice,
        float spawnWeight,
        int minStock,
        int maxStock,
        string tagOverride)
    {
        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning($"[ShopUISetupBuilder] Missing asset at '{assetPath}' while building DefaultShopCatalog.");
            return;
        }

        pool.Add(new ShopPoolEntry
        {
            itemAsset = asset,
            category = category,
            basePrice = basePrice,
            priceVariance = 0.05f,
            spawnWeight = spawnWeight,
            alwaysIncluded = false,
            minStock = minStock,
            maxStock = maxStock,
            unlimitedStock = false,
            categoryTagOverride = tagOverride
        });
    }

    // =====================================================================
    // Prefab: Assets/Prefabs/UI/ShopItemSlot.prefab
    // =====================================================================

    private static ShopItemUI CreateShopItemPrefab(ShopAssets assets)
    {
        GameObject root = new GameObject("ShopItemSlot", typeof(RectTransform));
        try
        {
            Image borderImg = AddRoundedImage(root, assets.rounded4Px, BorderSubtle);
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            ShopItemUI itemComp = root.AddComponent<ShopItemUI>();
            SetLE(root, 0f, 74f, 1f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            Image fillImg = AddRoundedImage(inner.gameObject, assets.rounded4Px, BgSurface);
            AddLayout(inner.gameObject, false, 10f, new RectOffset(10, 12, 10, 10), TextAnchor.MiddleLeft);

            // Active Left Accent Bar (2x40)
            RectTransform activeBarRt = NewRect("ActiveBar", inner);
            Image activeBarImg = AddRoundedImage(activeBarRt.gameObject, assets.rounded1Px, BorderSubtle);
            activeBarImg.raycastTarget = false;
            SetLE(activeBarRt.gameObject, 2f, 40f, 0f, 0f);

            // Icon Box (38x38)
            RectTransform iconBoxBorder = NewRect("ItemIconBox", inner);
            Image iconBoxBorderImg = AddRoundedImage(iconBoxBorder.gameObject, assets.rounded3Px, BorderSubtle);
            SetLE(iconBoxBorder.gameObject, 38f, 38f, 0f, 0f);

            RectTransform iconBoxFill = NewRect("Fill", iconBoxBorder);
            Stretch(iconBoxFill, 1f, 1f, 1f, 1f);
            Image iconBoxFillImg = AddRoundedImage(iconBoxFill.gameObject, assets.rounded3Px, BgInset);

            RectTransform iconRt = NewRect("ItemIcon", iconBoxFill);
            Stretch(iconRt, 6f, 6f, 6f, 6f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Item Info column
            RectTransform info = NewRect("ItemInfo", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 0f);
            AddLayout(info.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            // Top row: Category + Stock
            RectTransform topRow = NewRect("ItemTopRow", info);
            SetLE(topRow.gameObject, 0f, 14f, 1f, 0f);
            AddLayout(topRow.gameObject, false, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI catText = NewText("ItemCategory", topRow, "TYPE // SPEC", 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            catText.characterSpacing = 4f;
            SetLE(catText.gameObject, 0f, 14f, 1f, 0f);

            TextMeshProUGUI stockTxt = NewText("ItemStock", topRow, "STOCK: 2", 9f, AccentGreen, TextAlignmentOptions.MidlineRight, assets.monoSemiBold);
            stockTxt.characterSpacing = 3f;
            SetLE(stockTxt.gameObject, -1f, 14f, 0f, 0f);

            // Item Name
            TextMeshProUGUI nameTxt = NewText("ItemName", info, "ITEM NAME", 13f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
            SetLE(nameTxt.gameObject, 0f, 18f, 1f, 0f);

            // Bottom row: Meta + Price Group
            RectTransform bottomRow = NewRect("ItemBottomRow", info);
            SetLE(bottomRow.gameObject, 0f, 15f, 1f, 0f);
            AddLayout(bottomRow.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI metaTxt = NewText("ItemMeta", bottomRow, "DMG 18  •  MAG 12", 9f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            metaTxt.characterSpacing = 2.5f;
            SetLE(metaTxt.gameObject, 0f, 14f, 1f, 0f);

            RectTransform priceGroup = NewRect("PriceGroup", bottomRow);
            SetLE(priceGroup.gameObject, -1f, 15f, 0f, 0f);
            AddLayout(priceGroup.gameObject, false, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

            RectTransform coinRt = NewRect("CoinIcon", priceGroup);
            Image coinImg = coinRt.gameObject.AddComponent<Image>();
            coinImg.sprite = assets.iconCoins;
            coinImg.color = AccentCoin;
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;
            SetLE(coinRt.gameObject, 11f, 11f, 0f, 0f);

            TextMeshProUGUI priceTxt = NewText("ItemPrice", priceGroup, "500", 11f, AccentCoin, TextAlignmentOptions.MidlineRight, assets.monoSemiBold);
            priceTxt.characterSpacing = 2f;
            SetLE(priceTxt.gameObject, -1f, 15f, 0f, 0f);

            SerializedObject so = new SerializedObject(itemComp);
            so.FindProperty("borderImage").objectReferenceValue = borderImg;
            so.FindProperty("fillImage").objectReferenceValue = fillImg;
            so.FindProperty("activeBarImage").objectReferenceValue = activeBarImg;
            so.FindProperty("iconBoxBorderImage").objectReferenceValue = iconBoxBorderImg;
            so.FindProperty("iconBoxFillImage").objectReferenceValue = iconBoxFillImg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("categoryText").objectReferenceValue = catText;
            so.FindProperty("stockText").objectReferenceValue = stockTxt;
            so.FindProperty("nameText").objectReferenceValue = nameTxt;
            so.FindProperty("metaText").objectReferenceValue = metaTxt;
            so.FindProperty("coinIconImage").objectReferenceValue = coinImg;
            so.FindProperty("priceText").objectReferenceValue = priceTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ShopItemPrefabPath);
            return saved.GetComponent<ShopItemUI>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Scene Hierarchy: HUD Canvas/ShopMenu
    // =====================================================================

    private static void BuildShopMenuHierarchy(ShopItemUI itemPrefab, ShopAssets assets)
    {
        GameObject canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[ShopUISetupBuilder] Could not find 'HUD Canvas' in the active scene.");
            return;
        }

        Transform existing = canvasObj.transform.Find("ShopMenu");
        GameObject shopMenuObj;
        if (existing != null)
        {
            shopMenuObj = existing.gameObject;
            for (int i = shopMenuObj.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(shopMenuObj.transform.GetChild(i).gameObject);
        }
        else
        {
            RectTransform menuRt = NewRect("ShopMenu", canvasObj.transform);
            Stretch(menuRt);
            shopMenuObj = menuRt.gameObject;
        }

        ShopUI shopUI = shopMenuObj.GetComponent<ShopUI>() ?? shopMenuObj.AddComponent<ShopUI>();

        // 1. ModalRoot (full-screen dim overlay, inactive by default)
        RectTransform modalRoot = NewRect("ModalRoot", shopMenuObj.transform);
        Stretch(modalRoot);
        Image overlayImg = modalRoot.gameObject.AddComponent<Image>();
        overlayImg.color = BgBackdrop;

        // WindowBorder (1042 x 582 centered, matching 1040px .pen Shop Menu + 1px border)
        RectTransform windowBorder = NewRect("WindowBorder", modalRoot);
        windowBorder.anchorMin = new Vector2(0.5f, 0.5f);
        windowBorder.anchorMax = new Vector2(0.5f, 0.5f);
        windowBorder.pivot = new Vector2(0.5f, 0.5f);
        windowBorder.sizeDelta = new Vector2(1042f, 582f);
        windowBorder.anchoredPosition = Vector2.zero;
        AddRoundedImage(windowBorder.gameObject, assets.rounded4Px, BorderSubtle);

        RectTransform window = NewRect("Window", windowBorder);
        Stretch(window, 1f, 1f, 1f, 1f);
        AddRoundedImage(window.gameObject, assets.rounded4Px, BgSurface);
        AddLayout(window.gameObject, true, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        // ---------------- Shop Header (54px) ----------------
        RectTransform header = NewRect("ShopHeader", window);
        header.gameObject.AddComponent<Image>().color = BgInset;
        SetLE(header.gameObject, -1f, 54f, 1f, 0f);
        AddLayout(header.gameObject, false, 16f, new RectOffset(20, 20, 12, 12), TextAnchor.MiddleLeft);

        RectTransform headerLeft = NewRect("HeaderLeft", header);
        SetLE(headerLeft.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(headerLeft.gameObject, false, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        RectTransform dotRt = NewRect("LiveDot", headerLeft);
        Image dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.sprite = assets.circleDot;
        dotImg.color = AccentCoin;
        dotImg.raycastTarget = false;
        SetLE(dotRt.gameObject, 6f, 6f, 0f, 0f);

        TextMeshProUGUI menuTitle = NewText("MenuTitle", headerLeft, "BLACK MARKET // ARMORY", 12f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        menuTitle.characterSpacing = 6f;
        SetLE(menuTitle.gameObject, -1f, 20f, 0f, 0f);

        // Stock Tag Pill (#221C0D fill, #6B5523 border, #C8A44D text)
        RectTransform stockTagBorder = NewRect("StockTag", headerLeft);
        AddRoundedImage(stockTagBorder.gameObject, assets.rounded3Px, StockTagStroke);
        SetLE(stockTagBorder.gameObject, -1f, 20f, 0f, 0f);

        RectTransform stockTagFill = NewRect("Fill", stockTagBorder);
        Stretch(stockTagFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(stockTagFill.gameObject, assets.rounded3Px, StockTagFill);
        LayoutElement stFillIgnore = stockTagFill.gameObject.AddComponent<LayoutElement>();
        stFillIgnore.ignoreLayout = true;

        HorizontalLayoutGroup stHlg = stockTagBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        stHlg.padding = new RectOffset(8, 8, 3, 3);
        stHlg.childAlignment = TextAnchor.MiddleCenter;
        stHlg.childControlWidth = true;
        stHlg.childControlHeight = true;
        stHlg.childForceExpandWidth = false;
        stHlg.childForceExpandHeight = false;
        ContentSizeFitter stFitter = stockTagBorder.gameObject.AddComponent<ContentSizeFitter>();
        stFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        stFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        TextMeshProUGUI stockTagText = NewText("StockTagText", stockTagBorder, "LIMITED STOCK  •  RESTOCK IN 2 RUNS", 9f, AccentCoin, TextAlignmentOptions.Center, assets.monoRegular);
        stockTagText.characterSpacing = 4f;
        SetLE(stockTagText.gameObject, -1f, 14f, 0f, 0f);

        // Header Right: BalanceGroup + CloseHint Button
        RectTransform headerRight = NewRect("HeaderRight", header);
        SetLE(headerRight.gameObject, -1f, -1f, 0f, 1f);
        AddLayout(headerRight.gameObject, false, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

        RectTransform balBorder = NewRect("BalanceGroup", headerRight);
        AddRoundedImage(balBorder.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(balBorder.gameObject, -1f, 28f, 0f, 0f);

        RectTransform balFill = NewRect("Fill", balBorder);
        Stretch(balFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(balFill.gameObject, assets.rounded3Px, BgSurface);
        LayoutElement balIgnore = balFill.gameObject.AddComponent<LayoutElement>();
        balIgnore.ignoreLayout = true;

        HorizontalLayoutGroup balHlg = balBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        balHlg.padding = new RectOffset(10, 10, 4, 4);
        balHlg.spacing = 8f;
        balHlg.childAlignment = TextAnchor.MiddleCenter;
        balHlg.childControlWidth = true;
        balHlg.childControlHeight = true;
        balHlg.childForceExpandWidth = false;
        balHlg.childForceExpandHeight = false;
        ContentSizeFitter balFitter = balBorder.gameObject.AddComponent<ContentSizeFitter>();
        balFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        balFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform balCoinRt = NewRect("BalanceCoinIcon", balBorder);
        Image balCoinImg = balCoinRt.gameObject.AddComponent<Image>();
        balCoinImg.sprite = assets.iconCoins;
        balCoinImg.color = AccentCoin;
        balCoinImg.preserveAspect = true;
        balCoinImg.raycastTarget = false;
        SetLE(balCoinRt.gameObject, 13f, 13f, 0f, 0f);

        TextMeshProUGUI balLbl = NewText("BalanceLabel", balBorder, "CREDITS", 9f, TextMuted, TextAlignmentOptions.Center, assets.monoRegular);
        balLbl.characterSpacing = 4f;
        SetLE(balLbl.gameObject, -1f, 14f, 0f, 0f);

        TextMeshProUGUI balVal = NewText("BalanceValue", balBorder, "00,000", 12f, AccentCoin, TextAlignmentOptions.Center, assets.monoSemiBold);
        balVal.characterSpacing = 2.5f;
        SetLE(balVal.gameObject, -1f, 16f, 0f, 0f);

        // Close Hint Button ([ESC] CLOSE)
        RectTransform closeHintRt = NewRect("CloseHintButton", headerRight);
        SetLE(closeHintRt.gameObject, -1f, 26f, 0f, 0f);
        AddLayout(closeHintRt.gameObject, false, 6f, new RectOffset(4, 4, 2, 2), TextAnchor.MiddleCenter);
        ContentSizeFitter closeFitter = closeHintRt.gameObject.AddComponent<ContentSizeFitter>();
        closeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        closeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Image closeHitBg = closeHintRt.gameObject.AddComponent<Image>();
        closeHitBg.color = new Color(1f, 1f, 1f, 0.001f);
        Button closeBtn = closeHintRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeHitBg;
        closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };

        RectTransform escBoxBorder = NewRect("EscKeyBox", closeHintRt);
        AddRoundedImage(escBoxBorder.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(escBoxBorder.gameObject, -1f, 20f, 0f, 0f);

        RectTransform escBoxFill = NewRect("Fill", escBoxBorder);
        Stretch(escBoxFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(escBoxFill.gameObject, assets.rounded3Px, BgSurface);
        LayoutElement escIgnore = escBoxFill.gameObject.AddComponent<LayoutElement>();
        escIgnore.ignoreLayout = true;

        HorizontalLayoutGroup escHlg = escBoxBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        escHlg.padding = new RectOffset(7, 7, 3, 3);
        escHlg.childAlignment = TextAnchor.MiddleCenter;
        escHlg.childControlWidth = true;
        escHlg.childControlHeight = true;
        escHlg.childForceExpandWidth = false;
        escHlg.childForceExpandHeight = false;
        ContentSizeFitter escFitter = escBoxBorder.gameObject.AddComponent<ContentSizeFitter>();
        escFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        escFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        TextMeshProUGUI escKeyTxt = NewText("EscKey", escBoxBorder, "ESC", 9f, TextPrimary, TextAlignmentOptions.Center, assets.monoSemiBold);
        SetLE(escKeyTxt.gameObject, -1f, 12f, 0f, 0f);

        TextMeshProUGUI closeLbl = NewText("CloseLabel", closeHintRt, "CLOSE", 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        closeLbl.characterSpacing = 4f;
        SetLE(closeLbl.gameObject, -1f, 14f, 0f, 0f);

        // Header Divider (1px)
        AddHorizontalDivider("HeaderDivider", window);

        // ---------------- Shop Columns (flex height 1) ----------------
        RectTransform columnsContainer = NewRect("ShopColumns", window);
        SetLE(columnsContainer.gameObject, 0f, 0f, 1f, 1f);
        AddLayout(columnsContainer.gameObject, false, 16f, new RectOffset(20, 20, 18, 18), TextAnchor.UpperLeft);

        ShopUI.ColumnWidgets weaponsCol = BuildColumn("WeaponsColumn", columnsContainer, "01 // WEAPONS", assets.iconCrosshair, assets);
        ShopUI.ColumnWidgets equipmentCol = BuildColumn("EquipmentColumn", columnsContainer, "02 // EQUIPMENT", assets.iconShield, assets);
        ShopUI.ColumnWidgets consumablesCol = BuildColumn("ConsumablesColumn", columnsContainer, "03 // CONSUMABLES", assets.iconPackage, assets);

        // Footer Divider (1px)
        AddHorizontalDivider("FooterDivider", window);

        // ---------------- Shop Footer (56px) ----------------
        RectTransform footer = NewRect("ShopFooter", window);
        footer.gameObject.AddComponent<Image>().color = BgInset;
        SetLE(footer.gameObject, -1f, 56f, 1f, 0f);
        AddLayout(footer.gameObject, false, 12f, new RectOffset(20, 20, 12, 12), TextAnchor.MiddleLeft);

        // Selection Summary (left)
        RectTransform selectionSummary = NewRect("SelectionSummary", footer);
        SetLE(selectionSummary.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(selectionSummary.gameObject, false, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI selectedLbl = NewText("SelectedLabel", selectionSummary, "SELECTED", 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        selectedLbl.characterSpacing = 4f;
        SetLE(selectedLbl.gameObject, -1f, 16f, 0f, 0f);

        TextMeshProUGUI selectedNameTxt = NewText("SelectedName", selectionSummary, "NONE", 10f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        selectedNameTxt.characterSpacing = 2.5f;
        SetLE(selectedNameTxt.gameObject, -1f, 16f, 0f, 0f);

        TextMeshProUGUI dividerDot = NewText("DividerDot", selectionSummary, "•", 10f, TextMuted, TextAlignmentOptions.Center, assets.monoRegular);
        SetLE(dividerDot.gameObject, -1f, 16f, 0f, 0f);

        TextMeshProUGUI afterPurchaseTxt = NewText("AfterPurchaseLabel", selectionSummary, "AFTER PURCHASE: 0 CREDITS", 9f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        afterPurchaseTxt.characterSpacing = 3f;
        SetLE(afterPurchaseTxt.gameObject, 0f, 16f, 1f, 0f);

        // Action Hints & CTA (right)
        RectTransform actionHints = NewRect("ActionHints", footer);
        SetLE(actionHints.gameObject, -1f, -1f, 0f, 1f);
        AddLayout(actionHints.gameObject, false, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

        // [RMB] INSPECT
        Button inspectHintBtn = CreateKeyHintButton(
            "InspectHint", actionHints, "RMB", "INSPECT", assets,
            out _, out _, out _);

        // [R] IN STOCK ONLY
        Button inStockOnlyBtn = CreateKeyHintButton(
            "InStockOnlyHint", actionHints, "R", "IN STOCK ONLY", assets,
            out Image inStockKeyBorder, out TextMeshProUGUI inStockKeyText, out TextMeshProUGUI inStockLabel);

        // QTY Stepper
        RectTransform qtySelector = NewRect("QuantitySelector", actionHints);
        SetLE(qtySelector.gameObject, -1f, 31f, 0f, 0f);
        AddLayout(qtySelector.gameObject, false, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

        TextMeshProUGUI qtyLbl = NewText("QtyLabel", qtySelector, "QTY", 9f, TextMuted, TextAlignmentOptions.Center, assets.monoRegular);
        qtyLbl.characterSpacing = 4f;
        SetLE(qtyLbl.gameObject, -1f, 16f, 0f, 0f);

        RectTransform qtyStepperBorder = NewRect("QtyStepper", qtySelector);
        AddRoundedImage(qtyStepperBorder.gameObject, assets.rounded4Px, BorderSubtle);
        SetLE(qtyStepperBorder.gameObject, -1f, 31f, 0f, 0f);

        RectTransform qtyStepperFill = NewRect("Fill", qtyStepperBorder);
        Stretch(qtyStepperFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(qtyStepperFill.gameObject, assets.rounded4Px, BgSurface);
        LayoutElement qtyFillIgnore = qtyStepperFill.gameObject.AddComponent<LayoutElement>();
        qtyFillIgnore.ignoreLayout = true;

        HorizontalLayoutGroup qtyHlg = qtyStepperBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        qtyHlg.padding = new RectOffset(4, 4, 0, 0);
        qtyHlg.spacing = 2f;
        qtyHlg.childAlignment = TextAnchor.MiddleCenter;
        qtyHlg.childControlWidth = true;
        qtyHlg.childControlHeight = true;
        qtyHlg.childForceExpandWidth = false;
        qtyHlg.childForceExpandHeight = false;
        ContentSizeFitter qtyFitter = qtyStepperBorder.gameObject.AddComponent<ContentSizeFitter>();
        qtyFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        qtyFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Button qtyDecBtn = CreateStepperButton("QtyDecrease", qtyStepperBorder, assets.iconMinus, BorderDim, TextMuted, assets, out Image qtyDecBorder, out Image qtyDecIcon);

        RectTransform qtyValBox = NewRect("QtyValueBox", qtyStepperBorder);
        SetLE(qtyValBox.gameObject, 28f, 22f, 0f, 0f);
        TextMeshProUGUI qtyValTxt = NewText("QtyValue", qtyValBox, "1", 11f, TextPrimary, TextAlignmentOptions.Center, assets.monoSemiBold);
        Stretch(qtyValTxt.rectTransform);

        Button qtyIncBtn = CreateStepperButton("QtyIncrease", qtyStepperBorder, assets.iconPlus, BorderSubtle, TextPrimary, assets, out Image qtyIncBorder, out Image qtyIncIcon);

        // Purchase CTA Button (#C8A44D fill, #070809 content)
        RectTransform purchaseRt = NewRect("PurchaseButton", actionHints);
        Image purchaseImg = AddRoundedImage(purchaseRt.gameObject, assets.rounded4Px, AccentCoin);
        SetLE(purchaseRt.gameObject, -1f, 31f, 0f, 0f);

        HorizontalLayoutGroup purHlg = purchaseRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        purHlg.padding = new RectOffset(16, 16, 8, 8);
        purHlg.spacing = 8f;
        purHlg.childAlignment = TextAnchor.MiddleCenter;
        purHlg.childControlWidth = true;
        purHlg.childControlHeight = true;
        purHlg.childForceExpandWidth = false;
        purHlg.childForceExpandHeight = false;
        ContentSizeFitter purFitter = purchaseRt.gameObject.AddComponent<ContentSizeFitter>();
        purFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        purFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Button purchaseBtn = purchaseRt.gameObject.AddComponent<Button>();
        purchaseBtn.targetGraphic = purchaseImg;
        purchaseBtn.transition = Selectable.Transition.ColorTint;
        purchaseBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock pCb = purchaseBtn.colors;
        pCb.normalColor = Color.white;
        pCb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        pCb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        pCb.disabledColor = Color.white;
        purchaseBtn.colors = pCb;

        RectTransform purIconRt = NewRect("PurchaseIcon", purchaseRt);
        Image purIconImg = purIconRt.gameObject.AddComponent<Image>();
        purIconImg.sprite = assets.iconShoppingBag;
        purIconImg.color = BgCanvas;
        purIconImg.preserveAspect = true;
        purIconImg.raycastTarget = false;
        SetLE(purIconRt.gameObject, 13f, 13f, 0f, 0f);

        TextMeshProUGUI purLabel = NewText("PurchaseLabel", purchaseRt, "PURCHASE", 10f, BgCanvas, TextAlignmentOptions.Center, assets.monoSemiBold);
        purLabel.characterSpacing = 5f;
        SetLE(purLabel.gameObject, -1f, 15f, 0f, 0f);

        RectTransform purDivRt = NewRect("ButtonDivider", purchaseRt);
        Image purDivImg = purDivRt.gameObject.AddComponent<Image>();
        purDivImg.color = PurchaseDividerColor;
        purDivImg.raycastTarget = false;
        SetLE(purDivRt.gameObject, 1f, 12f, 0f, 0f);

        RectTransform purCoinRt = NewRect("ButtonCoinIcon", purchaseRt);
        Image purCoinImg = purCoinRt.gameObject.AddComponent<Image>();
        purCoinImg.sprite = assets.iconCoins;
        purCoinImg.color = BgCanvas;
        purCoinImg.preserveAspect = true;
        purCoinImg.raycastTarget = false;
        SetLE(purCoinRt.gameObject, 12f, 12f, 0f, 0f);

        TextMeshProUGUI purPriceTxt = NewText("PurchasePrice", purchaseRt, "500", 11f, BgCanvas, TextAlignmentOptions.Center, assets.monoSemiBold);
        purPriceTxt.characterSpacing = 2f;
        SetLE(purPriceTxt.gameObject, -1f, 15f, 0f, 0f);

        // 2. Inspect Overlay (hidden by default)
        RectTransform inspectOverlay = NewRect("InspectOverlay", modalRoot);
        Stretch(inspectOverlay);
        Image inspectDim = inspectOverlay.gameObject.AddComponent<Image>();
        inspectDim.color = new Color(BgCanvas.r, BgCanvas.g, BgCanvas.b, 0.72f);
        Button inspectDismissBtn = inspectOverlay.gameObject.AddComponent<Button>();
        inspectDismissBtn.transition = Selectable.Transition.None;
        inspectDismissBtn.navigation = new Navigation { mode = Navigation.Mode.None };

        RectTransform inspectCardBorder = NewRect("InspectCardBorder", inspectOverlay);
        inspectCardBorder.anchorMin = new Vector2(0.5f, 0.5f);
        inspectCardBorder.anchorMax = new Vector2(0.5f, 0.5f);
        inspectCardBorder.pivot = new Vector2(0.5f, 0.5f);
        inspectCardBorder.sizeDelta = new Vector2(440f, 340f);
        AddRoundedImage(inspectCardBorder.gameObject, assets.rounded4Px, AccentCoin);

        RectTransform inspectCard = NewRect("InspectCard", inspectCardBorder);
        Stretch(inspectCard, 1f, 1f, 1f, 1f);
        AddRoundedImage(inspectCard.gameObject, assets.rounded4Px, BgSurface);
        AddLayout(inspectCard.gameObject, true, 12f, new RectOffset(20, 20, 18, 18), TextAnchor.UpperLeft);

        RectTransform inspectHdr = NewRect("InspectHeader", inspectCard);
        SetLE(inspectHdr.gameObject, 0f, 24f, 1f, 0f);
        AddLayout(inspectHdr.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI inspectTitle = NewText("InspectTitle", inspectHdr, "ITEM NAME", 14f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
        SetLE(inspectTitle.gameObject, 0f, 24f, 1f, 0f);

        RectTransform inspectCloseRt = NewRect("InspectCloseButton", inspectHdr);
        AddRoundedImage(inspectCloseRt.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(inspectCloseRt.gameObject, 24f, 24f, 0f, 0f);
        RectTransform inspectCloseFill = NewRect("Fill", inspectCloseRt);
        Stretch(inspectCloseFill, 1f, 1f, 1f, 1f);
        Image inspectCloseFillImg = AddRoundedImage(inspectCloseFill.gameObject, assets.rounded3Px, BgInset);
        Button inspectCloseBtn = inspectCloseRt.gameObject.AddComponent<Button>();
        inspectCloseBtn.targetGraphic = inspectCloseFillImg;
        inspectCloseBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        RectTransform inspectCloseIconRt = NewRect("Icon", inspectCloseFill);
        inspectCloseIconRt.anchorMin = new Vector2(0.5f, 0.5f);
        inspectCloseIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        inspectCloseIconRt.sizeDelta = new Vector2(12f, 12f);
        Image inspectCloseIcon = inspectCloseIconRt.gameObject.AddComponent<Image>();
        inspectCloseIcon.sprite = assets.iconClose;
        inspectCloseIcon.color = TextSecondary;
        inspectCloseIcon.preserveAspect = true;
        inspectCloseIcon.raycastTarget = false;

        TextMeshProUGUI inspectCat = NewText("InspectCategory", inspectCard, "CATEGORY // SPEC", 10f, AccentCoin, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        inspectCat.characterSpacing = 4f;
        SetLE(inspectCat.gameObject, 0f, 16f, 1f, 0f);

        TextMeshProUGUI inspectPriceStock = NewText("InspectPriceStock", inspectCard, "500 CREDITS  •  STOCK: 2", 10f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        inspectPriceStock.characterSpacing = 3f;
        SetLE(inspectPriceStock.gameObject, 0f, 16f, 1f, 0f);

        AddHorizontalDivider("InspectDivider", inspectCard);

        TextMeshProUGUI inspectDetails = NewText("InspectDetails", inspectCard, "Details", 11f, TextPrimary, TextAlignmentOptions.TopLeft, assets.monoRegular);
        inspectDetails.textWrappingMode = TextWrappingModes.Normal;
        inspectDetails.overflowMode = TextOverflowModes.Overflow;
        SetLE(inspectDetails.gameObject, 0f, 0f, 1f, 1f);

        inspectOverlay.gameObject.SetActive(false);
        modalRoot.gameObject.SetActive(false);

        // Wire ShopUI SerializedFields
        SerializedObject so = new SerializedObject(shopUI);
        so.FindProperty("modalRoot").objectReferenceValue = modalRoot.gameObject;
        so.FindProperty("stockTagText").objectReferenceValue = stockTagText;
        so.FindProperty("balanceValueText").objectReferenceValue = balVal;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;

        WireColumnProperty(so.FindProperty("weaponsColumn"), weaponsCol);
        WireColumnProperty(so.FindProperty("equipmentColumn"), equipmentCol);
        WireColumnProperty(so.FindProperty("consumablesColumn"), consumablesCol);

        so.FindProperty("shopItemPrefab").objectReferenceValue = itemPrefab;
        so.FindProperty("weaponsFallbackIcon").objectReferenceValue = assets.iconCrosshair;
        so.FindProperty("equipmentFallbackIcon").objectReferenceValue = assets.iconShield;
        so.FindProperty("consumablesFallbackIcon").objectReferenceValue = assets.iconPackage;

        so.FindProperty("selectedNameText").objectReferenceValue = selectedNameTxt;
        so.FindProperty("afterPurchaseText").objectReferenceValue = afterPurchaseTxt;
        so.FindProperty("inspectHintButton").objectReferenceValue = inspectHintBtn;
        so.FindProperty("inStockOnlyButton").objectReferenceValue = inStockOnlyBtn;
        so.FindProperty("inStockOnlyKeyBorder").objectReferenceValue = inStockKeyBorder;
        so.FindProperty("inStockOnlyKeyText").objectReferenceValue = inStockKeyText;
        so.FindProperty("inStockOnlyLabel").objectReferenceValue = inStockLabel;

        so.FindProperty("qtyDecreaseButton").objectReferenceValue = qtyDecBtn;
        so.FindProperty("qtyDecreaseBorder").objectReferenceValue = qtyDecBorder;
        so.FindProperty("qtyDecreaseIcon").objectReferenceValue = qtyDecIcon;
        so.FindProperty("qtyValueText").objectReferenceValue = qtyValTxt;
        so.FindProperty("qtyIncreaseButton").objectReferenceValue = qtyIncBtn;
        so.FindProperty("qtyIncreaseBorder").objectReferenceValue = qtyIncBorder;
        so.FindProperty("qtyIncreaseIcon").objectReferenceValue = qtyIncIcon;

        so.FindProperty("purchaseButton").objectReferenceValue = purchaseBtn;
        so.FindProperty("purchaseButtonImage").objectReferenceValue = purchaseImg;
        so.FindProperty("purchaseIcon").objectReferenceValue = purIconImg;
        so.FindProperty("purchaseLabelText").objectReferenceValue = purLabel;
        so.FindProperty("purchaseDivider").objectReferenceValue = purDivImg;
        so.FindProperty("purchaseCoinIcon").objectReferenceValue = purCoinImg;
        so.FindProperty("purchasePriceText").objectReferenceValue = purPriceTxt;

        so.FindProperty("inspectOverlayRoot").objectReferenceValue = inspectOverlay.gameObject;
        so.FindProperty("inspectDismissButton").objectReferenceValue = inspectDismissBtn;
        so.FindProperty("inspectTitleText").objectReferenceValue = inspectTitle;
        so.FindProperty("inspectCategoryText").objectReferenceValue = inspectCat;
        so.FindProperty("inspectPriceStockText").objectReferenceValue = inspectPriceStock;
        so.FindProperty("inspectDetailsText").objectReferenceValue = inspectDetails;
        so.FindProperty("inspectCloseButton").objectReferenceValue = inspectCloseBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(shopMenuObj);
        EditorSceneManager.MarkSceneDirty(shopMenuObj.scene);
        EditorSceneManager.SaveScene(shopMenuObj.scene);
    }

    private static void WireColumnProperty(SerializedProperty prop, ShopUI.ColumnWidgets col)
    {
        prop.FindPropertyRelative("sectionCountText").objectReferenceValue = col.sectionCountText;
        prop.FindPropertyRelative("scrollRect").objectReferenceValue = col.scrollRect;
        prop.FindPropertyRelative("contentRoot").objectReferenceValue = col.contentRoot;
        prop.FindPropertyRelative("emptyNoticeObj").objectReferenceValue = col.emptyNoticeObj;
        prop.FindPropertyRelative("emptyNoticeText").objectReferenceValue = col.emptyNoticeText;
    }

    private static ShopUI.ColumnWidgets BuildColumn(
        string name,
        Transform parent,
        string titleLabel,
        Sprite iconSprite,
        ShopAssets assets)
    {
        RectTransform outer = NewRect(name, parent);
        AddRoundedImage(outer.gameObject, assets.rounded4Px, BorderDim);
        SetLE(outer.gameObject, 0f, 0f, 1f, 1f);

        RectTransform fill = NewRect("Fill", outer);
        Stretch(fill, 1f, 1f, 1f, 1f);
        AddRoundedImage(fill.gameObject, assets.rounded4Px, BgInset);
        AddLayout(fill.gameObject, true, 10f, new RectOffset(12, 12, 12, 12), TextAnchor.UpperLeft);

        // Column Header
        RectTransform hdr = NewRect("ColumnHeader", fill);
        SetLE(hdr.gameObject, 0f, 18f, 1f, 0f);
        AddLayout(hdr.gameObject, false, 8f, new RectOffset(2, 2, 0, 2), TextAnchor.MiddleLeft);

        RectTransform titleGroup = NewRect("TitleGroup", hdr);
        SetLE(titleGroup.gameObject, 0f, 16f, 1f, 0f);
        AddLayout(titleGroup.gameObject, false, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        RectTransform iconRt = NewRect("ColIcon", titleGroup);
        Image iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.color = AccentCoin;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        SetLE(iconRt.gameObject, 13f, 13f, 0f, 0f);

        TextMeshProUGUI colTitle = NewText("ColTitle", titleGroup, titleLabel, 10f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        colTitle.characterSpacing = 5f;
        SetLE(colTitle.gameObject, 0f, 16f, 1f, 0f);

        TextMeshProUGUI sectionCount = NewText("SectionCount", hdr, "0 / 0 IN STOCK", 9f, TextMuted, TextAlignmentOptions.MidlineRight, assets.monoRegular);
        sectionCount.characterSpacing = 4f;
        SetLE(sectionCount.gameObject, -1f, 16f, 0f, 0f);

        // Scrollable Items List
        RectTransform scrollView = NewRect("ItemsScrollView", fill);
        Image scrollBg = scrollView.gameObject.AddComponent<Image>();
        scrollBg.color = Color.clear;
        scrollBg.raycastTarget = true;
        SetLE(scrollView.gameObject, 0f, 0f, 1f, 1f);

        RectTransform viewport = NewRect("Viewport", scrollView);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("ItemsList", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        AddLayout(content.gameObject, true, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        RectTransform emptyNotice = NewRect("EmptyNotice", scrollView);
        Stretch(emptyNotice, 16f, 16f, 16f, 16f);
        TextMeshProUGUI emptyText = NewText("Text", emptyNotice, "NO ITEMS", 10f, TextMuted, TextAlignmentOptions.Center, assets.monoRegular);
        emptyText.characterSpacing = 4f;
        emptyText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(emptyText.rectTransform);
        emptyNotice.gameObject.SetActive(false);

        return new ShopUI.ColumnWidgets
        {
            sectionCountText = sectionCount,
            scrollRect = scrollRect,
            contentRoot = content,
            emptyNoticeObj = emptyNotice.gameObject,
            emptyNoticeText = emptyText
        };
    }

    // =====================================================================
    // World Object Setup: SafeZone/Shop
    // =====================================================================

    private static void ConfigureShopStationInScene(ShopCatalogData catalog)
    {
        GameObject shopObj = GameObject.Find("SafeZone/Shop") ?? GameObject.Find("Shop");
        if (shopObj == null)
        {
            Debug.LogWarning("[ShopUISetupBuilder] Could not find 'Shop' GameObject in the active scene.");
            return;
        }

        BoxCollider2D col = shopObj.GetComponent<BoxCollider2D>();
        if (col == null)
            col = shopObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 2f);
        col.offset = Vector2.zero;

        ShopStation station = shopObj.GetComponent<ShopStation>();
        if (station == null)
            station = shopObj.AddComponent<ShopStation>();

        SerializedObject stationSo = new SerializedObject(station);
        stationSo.FindProperty("interactKey").intValue = (int)KeyCode.E;
        stationSo.FindProperty("promptText").stringValue = "[E] Open Shop";
        stationSo.FindProperty("catalog").objectReferenceValue = catalog;
        stationSo.FindProperty("overrideRunsPerRestock").intValue = 0;
        stationSo.FindProperty("autoRefreshOnAwake").boolValue = true;
        stationSo.ApplyModifiedPropertiesWithoutUndo();

        InteractionPromptUI prompt = shopObj.GetComponent<InteractionPromptUI>();
        if (prompt == null)
            prompt = shopObj.AddComponent<InteractionPromptUI>();

        SerializedObject promptSo = new SerializedObject(prompt);
        promptSo.FindProperty("promptText").stringValue = "[E] Open Shop";
        promptSo.FindProperty("keyLabel").stringValue = "E";
        promptSo.FindProperty("actionLabel").stringValue = "OPEN MENU // SHOP";
        promptSo.FindProperty("targetLabel").stringValue = "SHOP";
        promptSo.FindProperty("offset").vector2Value = new Vector2(0f, 0.9f);
        promptSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(shopObj);
        EditorSceneManager.MarkSceneDirty(shopObj.scene);
        EditorSceneManager.SaveScene(shopObj.scene);
    }

    // =====================================================================
    // UI Primitive Helpers
    // =====================================================================

    private static void AddHorizontalDivider(string name, Transform parent)
    {
        RectTransform div = NewRect(name, parent);
        Image img = div.gameObject.AddComponent<Image>();
        img.color = BorderSubtle;
        img.raycastTarget = false;
        SetLE(div.gameObject, -1f, 1f, 1f, 0f);
    }

    private static Button CreateKeyHintButton(
        string name,
        Transform parent,
        string keyText,
        string labelText,
        ShopAssets assets,
        out Image keyBoxBorder,
        out TextMeshProUGUI keyTxt,
        out TextMeshProUGUI labelTxt)
    {
        RectTransform hintRt = NewRect(name, parent);
        SetLE(hintRt.gameObject, -1f, 24f, 0f, 0f);
        AddLayout(hintRt.gameObject, false, 6f, new RectOffset(2, 2, 2, 2), TextAnchor.MiddleCenter);
        ContentSizeFitter fitter = hintRt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Image hitBg = hintRt.gameObject.AddComponent<Image>();
        hitBg.color = new Color(1f, 1f, 1f, 0.001f);
        Button btn = hintRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = hitBg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        RectTransform keyOuter = NewRect("KeyBox", hintRt);
        keyBoxBorder = AddRoundedImage(keyOuter.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(keyOuter.gameObject, -1f, 18f, 0f, 0f);

        RectTransform keyFill = NewRect("Fill", keyOuter);
        Stretch(keyFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(keyFill.gameObject, assets.rounded3Px, BgSurface);
        LayoutElement keyIgnore = keyFill.gameObject.AddComponent<LayoutElement>();
        keyIgnore.ignoreLayout = true;

        HorizontalLayoutGroup keyHlg = keyOuter.gameObject.AddComponent<HorizontalLayoutGroup>();
        keyHlg.padding = new RectOffset(6, 6, 2, 2);
        keyHlg.childAlignment = TextAnchor.MiddleCenter;
        keyHlg.childControlWidth = true;
        keyHlg.childControlHeight = true;
        keyHlg.childForceExpandWidth = false;
        keyHlg.childForceExpandHeight = false;
        ContentSizeFitter keyFitter = keyOuter.gameObject.AddComponent<ContentSizeFitter>();
        keyFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        keyFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        keyTxt = NewText("KeyText", keyOuter, keyText, 9f, TextPrimary, TextAlignmentOptions.Center, assets.monoSemiBold);
        SetLE(keyTxt.gameObject, -1f, 12f, 0f, 0f);

        labelTxt = NewText("HintLabel", hintRt, labelText, 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        labelTxt.characterSpacing = 4f;
        SetLE(labelTxt.gameObject, -1f, 14f, 0f, 0f);

        return btn;
    }

    private static Button CreateStepperButton(
        string name,
        Transform parent,
        Sprite iconSprite,
        Color borderColor,
        Color iconColor,
        ShopAssets assets,
        out Image borderImg,
        out Image iconImg)
    {
        RectTransform outer = NewRect(name, parent);
        borderImg = AddRoundedImage(outer.gameObject, assets.rounded3Px, borderColor);
        SetLE(outer.gameObject, 22f, 22f, 0f, 0f);

        RectTransform fill = NewRect("Fill", outer);
        Stretch(fill, 1f, 1f, 1f, 1f);
        Image fillImg = AddRoundedImage(fill.gameObject, assets.rounded3Px, BgInset);

        Button btn = outer.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = Color.white;
        btn.colors = cb;

        RectTransform iconRt = NewRect("Icon", fill);
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(11f, 11f);
        iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.color = iconColor;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        return btn;
    }

    private static Image AddRoundedImage(GameObject go, Sprite roundedSprite, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        return img;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void AddLayout(GameObject go, bool vertical, float spacing, RectOffset padding, TextAnchor align)
    {
        HorizontalOrVerticalLayoutGroup group = vertical
            ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>()
            : go.AddComponent<HorizontalLayoutGroup>();

        group.spacing = spacing;
        group.padding = padding;
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
    }

    private static void SetLE(GameObject go, float prefW, float prefH, float flexW, float flexH)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = prefW;
        le.preferredHeight = prefH;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        le.minWidth = prefW >= 0f ? prefW : -1f;
        le.minHeight = prefH >= 0f ? prefH : -1f;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // =====================================================================
    // Procedural Anti-Aliased Icons
    // =====================================================================

    private static Sprite GenerateSprite(string fileName, int width, int height, Func<int, int, Color[]> pixelGenerator)
    {
        string assetPath = $"{SpriteFolder}/{fileName}.png";
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels(pixelGenerator(width, height));
        tex.Apply();

        byte[] pngBytes = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        File.WriteAllBytes(fullPath, pngBytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static Color[] BlankPixels(int w, int h)
    {
        Color[] px = new Color[w * h];
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        return px;
    }

    private static void PaintCoverage(Color[] px, int w, int h, Func<float, float, float> signedDist)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = signedDist(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
    }

    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    private static float SdArcUpper(Vector2 p, Vector2 center, float radius)
    {
        if (p.y < center.y)
            return Mathf.Min((p - (center + new Vector2(-radius, 0f))).magnitude, (p - (center + new Vector2(radius, 0f))).magnitude);
        return Mathf.Abs((p - center).magnitude - radius);
    }

    private static Color[] DrawPackageIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.4f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            Vector2 top = new Vector2(32f, 54f);
            Vector2 topL = new Vector2(13f, 43f);
            Vector2 topR = new Vector2(51f, 43f);
            Vector2 mid = new Vector2(32f, 32f);
            Vector2 botL = new Vector2(13f, 21f);
            Vector2 botR = new Vector2(51f, 21f);
            Vector2 bot = new Vector2(32f, 10f);

            float d = SdSegment(p, top, topL);
            d = Mathf.Min(d, SdSegment(p, top, topR));
            d = Mathf.Min(d, SdSegment(p, topL, mid));
            d = Mathf.Min(d, SdSegment(p, topR, mid));
            d = Mathf.Min(d, SdSegment(p, topL, botL));
            d = Mathf.Min(d, SdSegment(p, topR, botR));
            d = Mathf.Min(d, SdSegment(p, mid, bot));
            d = Mathf.Min(d, SdSegment(p, botL, bot));
            d = Mathf.Min(d, SdSegment(p, botR, bot));
            d = Mathf.Min(d, SdSegment(p, new Vector2(22.5f, 48.5f), new Vector2(41.5f, 37.5f)));
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawShoppingBagIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.5f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            // Bag body
            float d = SdSegment(p, new Vector2(15f, 44f), new Vector2(49f, 44f));
            d = Mathf.Min(d, SdSegment(p, new Vector2(15f, 44f), new Vector2(12f, 13f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(49f, 44f), new Vector2(52f, 13f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(12f, 13f), new Vector2(52f, 13f)));
            // Upper handle loop
            d = Mathf.Min(d, SdArcUpper(p, new Vector2(32f, 44f), 9f));
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawMinusIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 3.0f;
        PaintCoverage(px, w, h, (x, y) =>
            SdSegment(new Vector2(x, y), new Vector2(16f, 32f), new Vector2(48f, 32f)) - stroke);
        return px;
    }

    private static Color[] DrawPlusIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 3.0f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float d1 = SdSegment(p, new Vector2(16f, 32f), new Vector2(48f, 32f));
            float d2 = SdSegment(p, new Vector2(32f, 16f), new Vector2(32f, 48f));
            return Mathf.Min(d1, d2) - stroke;
        });
        return px;
    }
}
