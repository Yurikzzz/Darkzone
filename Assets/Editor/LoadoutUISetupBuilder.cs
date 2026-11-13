using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility that builds the reusable Loadout/Inventory UI prefabs under Assets/Prefabs/UI/
/// and constructs the authored uGUI Canvas hierarchy under HUD Canvas/LoadoutMenu in the active scene.
/// </summary>
public static class LoadoutUISetupBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string InventoryItemPrefabPath = PrefabFolder + "/InventoryItemSlot.prefab";
    private const string LoadoutSlotPrefabPath = PrefabFolder + "/LoadoutEquipmentSlot.prefab";

    // Dark tactical / military palette
    private static readonly Color OverlayColor = new Color(0.02f, 0.03f, 0.02f, 0.50f);
    private static readonly Color BorderColor = new Color(0.22f, 0.27f, 0.23f, 1f);
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.08f, 1f);
    private static readonly Color HeaderColor = new Color(0.11f, 0.14f, 0.12f, 1f);
    private static readonly Color CardColor = new Color(0.11f, 0.13f, 0.12f, 1f);
    private static readonly Color InsetColor = new Color(0.045f, 0.055f, 0.05f, 1f);
    private static readonly Color Amber = new Color(0.90f, 0.66f, 0.18f, 1f);
    private static readonly Color SteelBlue = new Color(0.29f, 0.56f, 0.78f, 1f);
    private static readonly Color TacticalGreen = new Color(0.28f, 0.72f, 0.41f, 1f);
    private static readonly Color Crimson = new Color(0.65f, 0.22f, 0.22f, 1f);
    private static readonly Color OliveGrey = new Color(0.24f, 0.29f, 0.25f, 1f);
    private static readonly Color InactiveTab = new Color(0.16f, 0.19f, 0.17f, 1f);
    private static readonly Color DimText = new Color(0.58f, 0.63f, 0.59f, 1f);
    private static readonly Color DarkText = new Color(0.08f, 0.09f, 0.08f, 1f);

    [MenuItem("Tools/Darkzone/Build Loadout UI & Prefabs")]
    public static void BuildAll()
    {
        EnsureDirectories();

        InventoryItemSlotUI itemSlotPrefab = CreateInventoryItemSlotPrefab();
        LoadoutSlotUI loadoutSlotPrefab = CreateLoadoutSlotPrefab();

        BuildSceneHierarchy(itemSlotPrefab, loadoutSlotPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LoadoutUISetupBuilder] Successfully built UI prefabs and HUD Canvas/LoadoutMenu hierarchy.");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    // =====================================================================
    // Prefab 1: Assets/Prefabs/UI/InventoryItemSlot.prefab
    // =====================================================================

    private static InventoryItemSlotUI CreateInventoryItemSlotPrefab()
    {
        GameObject root = new GameObject("InventoryItemSlot", typeof(RectTransform));
        try
        {
            Image borderImg = root.AddComponent<Image>();
            borderImg.color = BorderColor;

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            InventoryItemSlotUI slotComp = root.AddComponent<InventoryItemSlotUI>();
            SetLE(root, 0f, 38f, 1f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            Image innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.color = CardColor;
            AddLayout(inner.gameObject, false, 5f, new RectOffset(4, 4, 3, 3), TextAnchor.MiddleLeft);

            // Thumbnail box + Icon
            RectTransform thumb = NewRect("ThumbBox", inner);
            Image thumbImg = thumb.gameObject.AddComponent<Image>();
            thumbImg.color = InsetColor;
            SetLE(thumb.gameObject, 36f, 30f, 0f, 0f);

            RectTransform iconRt = NewRect("Icon", thumb);
            Stretch(iconRt, 2f, 2f, 2f, 2f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Title + Stats column
            RectTransform info = NewRect("Info", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 1f);
            AddLayout(info.gameObject, true, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI titleText = NewText("TitleText", info, "Item Name", 9f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            SetLE(titleText.gameObject, 0f, 12f, 1f, 0f);

            TextMeshProUGUI statsText = NewText("StatsText", info, "Stats", 6.8f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            SetLE(statsText.gameObject, 0f, 10f, 1f, 0f);

            // Right Actions (Equipped badge + Equip/Unequip button)
            RectTransform actions = NewRect("Actions", inner);
            SetLE(actions.gameObject, -1f, 22f, 0f, 0f);
            AddLayout(actions.gameObject, false, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

            RectTransform badge = NewRect("EquippedBadge", actions);
            Image badgeBg = badge.gameObject.AddComponent<Image>();
            badgeBg.color = new Color(Amber.r, Amber.g, Amber.b, 0.25f);
            SetLE(badge.gameObject, 46f, 18f, 0f, 0f);
            AddLayout(badge.gameObject, false, 0f, new RectOffset(2, 2, 0, 0), TextAnchor.MiddleCenter);

            TextMeshProUGUI badgeText = NewText("Text", badge, "SLOT 1", 6.5f, Amber, TextAlignmentOptions.Center, FontStyles.Bold);
            SetLE(badgeText.gameObject, 0f, 0f, 1f, 1f);
            badge.gameObject.SetActive(false);

            Button actionBtn = NewButton("ActionButton", actions, "EQUIP", OliveGrey, 6.8f);
            SetLE(actionBtn.gameObject, 42f, 18f, 0f, 0f);
            Image actionBtnImg = actionBtn.GetComponent<Image>();
            TextMeshProUGUI actionBtnText = actionBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Wire serialized fields on InventoryItemSlotUI
            SerializedObject so = new SerializedObject(slotComp);
            so.FindProperty("borderImage").objectReferenceValue = borderImg;
            so.FindProperty("innerImage").objectReferenceValue = innerImg;
            so.FindProperty("thumbBoxImage").objectReferenceValue = thumbImg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("equippedBadge").objectReferenceValue = badge.gameObject;
            so.FindProperty("equippedBadgeBg").objectReferenceValue = badgeBg;
            so.FindProperty("equippedBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("actionButton").objectReferenceValue = actionBtn;
            so.FindProperty("actionButtonImage").objectReferenceValue = actionBtnImg;
            so.FindProperty("actionButtonText").objectReferenceValue = actionBtnText;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, InventoryItemPrefabPath);
            return saved.GetComponent<InventoryItemSlotUI>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Prefab 2: Assets/Prefabs/UI/LoadoutEquipmentSlot.prefab
    // =====================================================================

    private static LoadoutSlotUI CreateLoadoutSlotPrefab()
    {
        GameObject root = new GameObject("LoadoutEquipmentSlot", typeof(RectTransform));
        try
        {
            Image borderImg = root.AddComponent<Image>();
            borderImg.color = BorderColor;

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            LoadoutSlotUI slotComp = root.AddComponent<LoadoutSlotUI>();
            SetLE(root, 0f, 42f, 1f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            Image innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.color = CardColor;
            AddLayout(inner.gameObject, false, 4f, new RectOffset(4, 4, 3, 3), TextAnchor.MiddleLeft);

            // Thumbnail box + Icon
            RectTransform thumb = NewRect("ThumbBox", inner);
            Image thumbImg = thumb.gameObject.AddComponent<Image>();
            thumbImg.color = InsetColor;
            SetLE(thumb.gameObject, 34f, 34f, 0f, 0f);

            RectTransform iconRt = NewRect("Icon", thumb);
            Stretch(iconRt, 2f, 2f, 2f, 2f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = false;

            // Info column (Header, Name, Stats)
            RectTransform info = NewRect("Info", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 1f);
            AddLayout(info.gameObject, true, 1f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI headerText = NewText("HeaderText", info, "SLOT", 6f, Amber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            SetLE(headerText.gameObject, 0f, 8f, 1f, 0f);

            TextMeshProUGUI nameText = NewText("NameText", info, "EMPTY", 8.5f, DimText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            SetLE(nameText.gameObject, 0f, 11f, 1f, 0f);

            TextMeshProUGUI statsText = NewText("StatsText", info, "Empty slot", 6.5f, DimText, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            SetLE(statsText.gameObject, 0f, 9f, 1f, 0f);

            // Unequip button
            Button unequipBtn = NewButton("UnequipButton", inner, "X", Crimson, 7.5f);
            SetLE(unequipBtn.gameObject, 16f, 16f, 0f, 0f);
            unequipBtn.gameObject.SetActive(false);

            // Wire serialized fields on LoadoutSlotUI
            SerializedObject so = new SerializedObject(slotComp);
            so.FindProperty("borderImage").objectReferenceValue = borderImg;
            so.FindProperty("innerImage").objectReferenceValue = innerImg;
            so.FindProperty("thumbBoxImage").objectReferenceValue = thumbImg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("headerText").objectReferenceValue = headerText;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("unequipButton").objectReferenceValue = unequipBtn;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("defaultBorderColor").colorValue = BorderColor;
            so.FindProperty("activeAccentColor").colorValue = Amber;
            so.FindProperty("highlightValidColor").colorValue = TacticalGreen;
            so.FindProperty("defaultInnerColor").colorValue = CardColor;
            so.FindProperty("highlightInnerColor").colorValue = new Color(0.14f, 0.24f, 0.17f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, LoadoutSlotPrefabPath);
            return saved.GetComponent<LoadoutSlotUI>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Scene Hierarchy: HUD Canvas/LoadoutMenu
    // =====================================================================

    private static void BuildSceneHierarchy(InventoryItemSlotUI itemSlotPrefab, LoadoutSlotUI loadoutSlotPrefab)
    {
        GameObject canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[LoadoutUISetupBuilder] Could not find 'HUD Canvas' in the active scene.");
            return;
        }

        Transform loadoutMenuTrans = canvasObj.transform.Find("LoadoutMenu");
        if (loadoutMenuTrans == null)
        {
            Debug.LogError("[LoadoutUISetupBuilder] Could not find 'HUD Canvas/LoadoutMenu' in the active scene.");
            return;
        }

        GameObject loadoutMenuObj = loadoutMenuTrans.gameObject;

        // Clean existing children under LoadoutMenu (legacy ModalRoot/DialogBox)
        for (int i = loadoutMenuObj.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(loadoutMenuObj.transform.GetChild(i).gameObject);
        }

        LoadoutUI loadoutUI = loadoutMenuObj.GetComponent<LoadoutUI>();
        if (loadoutUI == null)
            loadoutUI = loadoutMenuObj.AddComponent<LoadoutUI>();

        InventoryDragController dragCtrl = loadoutMenuObj.GetComponent<InventoryDragController>();
        if (dragCtrl == null)
            dragCtrl = loadoutMenuObj.AddComponent<InventoryDragController>();

        // 1. ModalRoot (blocks raycasts behind, inactive by default)
        RectTransform modalRoot = NewRect("ModalRoot", loadoutMenuObj.transform);
        Stretch(modalRoot);
        Image overlayImg = modalRoot.gameObject.AddComponent<Image>();
        overlayImg.color = OverlayColor;

        // PanelBorder (94% x 92% centered)
        RectTransform panelBorder = NewRect("PanelBorder", modalRoot);
        panelBorder.anchorMin = new Vector2(0.03f, 0.04f);
        panelBorder.anchorMax = new Vector2(0.97f, 0.96f);
        panelBorder.pivot = new Vector2(0.5f, 0.5f);
        panelBorder.offsetMin = Vector2.zero;
        panelBorder.offsetMax = Vector2.zero;
        panelBorder.gameObject.AddComponent<Image>().color = BorderColor;

        // Panel
        RectTransform panel = NewRect("Panel", panelBorder);
        Stretch(panel, 1f, 1f, 1f, 1f);
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        AddLayout(panel.gameObject, true, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        // Header
        RectTransform header = NewRect("Header", panel);
        header.gameObject.AddComponent<Image>().color = HeaderColor;
        SetLE(header.gameObject, -1f, 20f, 1f, 0f);
        AddLayout(header.gameObject, false, 6f, new RectOffset(8, 4, 2, 2), TextAnchor.MiddleLeft);

        TextMeshProUGUI title = NewText("Title", header, "LOADOUT & INVENTORY", 9.5f, Amber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(title.gameObject, 0f, -1f, 1f, 1f);

        Button closeBtn = NewButton("CloseButton", header, "X", Crimson, 8f);
        SetLE(closeBtn.gameObject, 15f, 15f, 0f, 0f);

        // Body
        RectTransform body = NewRect("Body", panel);
        SetLE(body.gameObject, -1f, 0f, 1f, 1f);
        AddLayout(body.gameObject, false, 8f, new RectOffset(6, 6, 6, 6), TextAnchor.UpperLeft);

        // ---------------- Left Column: Loadout Display ----------------
        RectTransform leftCol = NewRect("LeftColumn_Loadout", body);
        SetLE(leftCol.gameObject, 230f, 0f, 0f, 1f);
        AddLayout(leftCol.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionLabel(leftCol, "EQUIPPED WEAPONS");

        LoadoutSlotUI primarySlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, leftCol, "Slot_Primary",
            LoadoutSlotUI.SlotKind.WeaponPrimary, 0,
            "SLOT 1 - PRIMARY", Amber, dragCtrl, 42f, 34f, true, 16f);

        Button swapBtn = NewButton("SwapSlotsButton", leftCol, "SWAP SLOTS", OliveGrey, 7.5f);
        SetLE(swapBtn.gameObject, 0f, 14f, 1f, 0f);

        LoadoutSlotUI secondarySlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, leftCol, "Slot_Secondary",
            LoadoutSlotUI.SlotKind.WeaponSecondary, 1,
            "SLOT 2 - SECONDARY", SteelBlue, dragCtrl, 42f, 34f, true, 16f);

        SectionLabel(leftCol, "EQUIPMENT");

        RectTransform armorRow = NewRect("ArmorRow", leftCol);
        SetLE(armorRow.gameObject, 0f, 36f, 1f, 0f);
        AddLayout(armorRow.gameObject, false, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        LoadoutSlotUI vestSlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, armorRow, "Slot_Vest",
            LoadoutSlotUI.SlotKind.EquipmentVest, 0,
            "VEST", TacticalGreen, dragCtrl, 36f, 26f, false, 14f);

        LoadoutSlotUI maskSlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, armorRow, "Slot_Mask",
            LoadoutSlotUI.SlotKind.EquipmentMask, 1,
            "MASK", TacticalGreen, dragCtrl, 36f, 26f, false, 14f);

        SectionLabel(leftCol, "CONSUMABLES");

        RectTransform consumablesGrid = NewRect("ConsumablesRow", leftCol);
        SetLE(consumablesGrid.gameObject, 0f, 51f, 1f, 0f);
        GridLayoutGroup grid = consumablesGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(113f, 24f);
        grid.spacing = new Vector2(4f, 3f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperLeft;

        string[] consumableKeys = new[] { "3", "4", "5", "6" };
        LoadoutSlotUI[] consumableSlots = new LoadoutSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            consumableSlots[i] = InstantiateLoadoutSlot(
                loadoutSlotPrefab, consumablesGrid, $"ConsumableSlot_{i + 1}",
                LoadoutSlotUI.SlotKind.Consumable, i,
                $"SLOT {i + 1} [{consumableKeys[i]}]", Amber, dragCtrl, 24f, 18f, false, 12f, isCompactConsumable: true);
        }

        // ---------------- Right Column: Inventory Tabs & Scroll List ----------------
        RectTransform rightCol = NewRect("RightColumn_Inventory", body);
        SetLE(rightCol.gameObject, 0f, 0f, 1f, 1f);
        AddLayout(rightCol.gameObject, true, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        RectTransform tabBar = NewRect("TabBar", rightCol);
        SetLE(tabBar.gameObject, 0f, 20f, 1f, 0f);
        AddLayout(tabBar.gameObject, false, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        string[] tabNames = new[] { "WEAPONS", "EQUIPMENT", "CONSUMABLES" };
        Button[] tabButtons = new Button[3];
        Image[] tabImages = new Image[3];
        TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            Button tabBtn = NewButton($"Tab_{tabNames[i]}", tabBar, tabNames[i], InactiveTab, 7.5f);
            SetLE(tabBtn.gameObject, 0f, 20f, 1f, 0f);
            tabButtons[i] = tabBtn;
            tabImages[i] = tabBtn.GetComponent<Image>();
            tabTexts[i] = tabBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        RectTransform scrollView = NewRect("InventoryScrollView", rightCol);
        scrollView.gameObject.AddComponent<Image>().color = InsetColor;
        SetLE(scrollView.gameObject, 0f, 0f, 1f, 1f);

        RectTransform viewport = NewRect("Viewport", scrollView);
        Stretch(viewport, 3f, 3f, 3f, 3f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        AddLayout(content.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;

        RectTransform emptyNotice = NewRect("EmptyNotice", scrollView);
        Stretch(emptyNotice, 10f, 10f, 10f, 10f);
        TextMeshProUGUI emptyText = NewText("Text", emptyNotice, "Empty", 8.5f, DimText, TextAlignmentOptions.Center, FontStyles.Normal);
        emptyText.enableWordWrapping = true;
        Stretch(emptyText.rectTransform);
        emptyNotice.gameObject.SetActive(false);

        // Footer
        RectTransform footer = NewRect("Footer", panel);
        footer.gameObject.AddComponent<Image>().color = HeaderColor;
        SetLE(footer.gameObject, -1f, 13f, 1f, 0f);
        AddLayout(footer.gameObject, false, 0f, new RectOffset(8, 8, 1, 1), TextAnchor.MiddleCenter);

        TextMeshProUGUI hint = NewText("HintText", footer, "[CLICK] OR [DRAG] TO EQUIP   |   [E] / [ESC] CLOSE", 6.8f, DimText, TextAlignmentOptions.Midline, FontStyles.Normal);
        SetLE(hint.gameObject, 0f, -1f, 1f, 1f);

        // 2. Top-level DragLayer (last sibling inside LoadoutMenu, blocksRaycasts = false)
        RectTransform dragLayer = NewRect("DragLayer", loadoutMenuObj.transform);
        Stretch(dragLayer);
        CanvasGroup dragLayerCg = dragLayer.gameObject.AddComponent<CanvasGroup>();
        dragLayerCg.blocksRaycasts = false;
        dragLayerCg.interactable = false;

        RectTransform dragGhost = NewRect("DragGhost", dragLayer);
        dragGhost.sizeDelta = new Vector2(115f, 30f);
        dragGhost.pivot = new Vector2(0.5f, 0.5f);
        Image ghostBorder = dragGhost.gameObject.AddComponent<Image>();
        ghostBorder.color = Amber;
        ghostBorder.raycastTarget = false;

        RectTransform ghostInner = NewRect("InnerFill", dragGhost);
        Stretch(ghostInner, 1f, 1f, 1f, 1f);
        Image ghostInnerImg = ghostInner.gameObject.AddComponent<Image>();
        ghostInnerImg.color = new Color(0.11f, 0.14f, 0.12f, 0.94f);
        ghostInnerImg.raycastTarget = false;
        AddLayout(ghostInner.gameObject, false, 4f, new RectOffset(4, 4, 3, 3), TextAnchor.MiddleLeft);

        RectTransform ghostIconRt = NewRect("Icon", ghostInner);
        Image ghostIcon = ghostIconRt.gameObject.AddComponent<Image>();
        ghostIcon.preserveAspect = true;
        ghostIcon.raycastTarget = false;
        SetLE(ghostIconRt.gameObject, 22f, 22f, 0f, 0f);

        TextMeshProUGUI ghostLabel = NewText("Label", ghostInner, "Item", 7.5f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        ghostLabel.raycastTarget = false;
        SetLE(ghostLabel.gameObject, 0f, -1f, 1f, 1f);

        dragGhost.gameObject.SetActive(false);
        modalRoot.gameObject.SetActive(false);

        // Wire InventoryDragController
        SerializedObject dragSo = new SerializedObject(dragCtrl);
        dragSo.FindProperty("dragLayer").objectReferenceValue = dragLayer;
        dragSo.FindProperty("dragGhost").objectReferenceValue = dragGhost;
        dragSo.FindProperty("dragGhostBorder").objectReferenceValue = ghostBorder;
        dragSo.FindProperty("dragGhostIcon").objectReferenceValue = ghostIcon;
        dragSo.FindProperty("dragGhostLabel").objectReferenceValue = ghostLabel;
        dragSo.FindProperty("loadoutUI").objectReferenceValue = loadoutUI;
        dragSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire LoadoutUI
        SerializedObject uiSo = new SerializedObject(loadoutUI);
        uiSo.FindProperty("modalRoot").objectReferenceValue = modalRoot.gameObject;
        uiSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
        uiSo.FindProperty("swapButton").objectReferenceValue = swapBtn;
        uiSo.FindProperty("primarySlot").objectReferenceValue = primarySlot;
        uiSo.FindProperty("secondarySlot").objectReferenceValue = secondarySlot;
        uiSo.FindProperty("vestSlot").objectReferenceValue = vestSlot;
        uiSo.FindProperty("maskSlot").objectReferenceValue = maskSlot;

        SerializedProperty cSlotsProp = uiSo.FindProperty("consumableSlots");
        cSlotsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
            cSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = consumableSlots[i];

        uiSo.FindProperty("inventoryDropArea").objectReferenceValue = rightCol;

        SerializedProperty tabBtnsProp = uiSo.FindProperty("tabButtons");
        SerializedProperty tabImgsProp = uiSo.FindProperty("tabImages");
        SerializedProperty tabTxtsProp = uiSo.FindProperty("tabTexts");
        tabBtnsProp.arraySize = 3;
        tabImgsProp.arraySize = 3;
        tabTxtsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            tabBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
            tabImgsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabImages[i];
            tabTxtsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabTexts[i];
        }

        uiSo.FindProperty("inventoryScrollRect").objectReferenceValue = scrollRect;
        uiSo.FindProperty("inventoryContentRoot").objectReferenceValue = content;
        uiSo.FindProperty("emptyNoticeObj").objectReferenceValue = emptyNotice.gameObject;
        uiSo.FindProperty("emptyNoticeText").objectReferenceValue = emptyText;
        uiSo.FindProperty("inventoryItemSlotPrefab").objectReferenceValue = itemSlotPrefab;
        uiSo.FindProperty("dragController").objectReferenceValue = dragCtrl;
        uiSo.FindProperty("activeTabColor").colorValue = Amber;
        uiSo.FindProperty("inactiveTabColor").colorValue = InactiveTab;
        uiSo.FindProperty("activeTabTextColor").colorValue = DarkText;
        uiSo.FindProperty("inactiveTabTextColor").colorValue = DimText;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(loadoutMenuObj);
        EditorSceneManager.MarkSceneDirty(loadoutMenuObj.scene);
        EditorSceneManager.SaveScene(loadoutMenuObj.scene);
    }

    private static LoadoutSlotUI InstantiateLoadoutSlot(
        LoadoutSlotUI prefab,
        Transform parent,
        string name,
        LoadoutSlotUI.SlotKind kind,
        int index,
        string headerLabel,
        Color accentColor,
        InventoryDragController dragCtrl,
        float slotHeight,
        float thumbSize,
        bool showStats,
        float unequipSize,
        bool isCompactConsumable = false)
    {
        LoadoutSlotUI slot = (LoadoutSlotUI)PrefabUtility.InstantiatePrefab(prefab, parent);
        slot.name = name;

        SetLE(slot.gameObject, 0f, slotHeight, 1f, 0f);

        Transform inner = slot.transform.Find("InnerFill");
        if (inner != null && isCompactConsumable)
        {
            HorizontalLayoutGroup hlg = inner.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 3f;
                hlg.padding = new RectOffset(3, 3, 2, 2);
            }
        }

        Transform thumb = inner != null ? inner.Find("ThumbBox") : null;
        if (thumb != null)
            SetLE(thumb.gameObject, thumbSize, thumbSize, 0f, 0f);

        Transform info = inner != null ? inner.Find("Info") : null;
        if (info != null)
        {
            Transform hdrTrans = info.Find("HeaderText");
            if (hdrTrans != null)
            {
                TextMeshProUGUI hdr = hdrTrans.GetComponent<TextMeshProUGUI>();
                hdr.text = headerLabel;
                hdr.color = accentColor;
                if (isCompactConsumable)
                {
                    hdr.fontSize = 5.5f;
                    SetLE(hdr.gameObject, 0f, 7f, 1f, 0f);
                }
            }

            Transform nameTrans = info.Find("NameText");
            if (nameTrans != null && isCompactConsumable)
            {
                TextMeshProUGUI nm = nameTrans.GetComponent<TextMeshProUGUI>();
                nm.fontSize = 7f;
                SetLE(nm.gameObject, 0f, 9f, 1f, 0f);
            }

            Transform statsTrans = info.Find("StatsText");
            if (statsTrans != null)
                statsTrans.gameObject.SetActive(showStats);
        }

        Transform unequipTrans = inner != null ? inner.Find("UnequipButton") : null;
        if (unequipTrans != null)
        {
            SetLE(unequipTrans.gameObject, unequipSize, unequipSize, 0f, 0f);
            if (isCompactConsumable)
            {
                TextMeshProUGUI xTxt = unequipTrans.GetComponentInChildren<TextMeshProUGUI>();
                if (xTxt != null) xTxt.fontSize = 6.5f;
            }
        }

        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("slotKind").enumValueIndex = (int)kind;
        so.FindProperty("slotIndex").intValue = index;
        so.FindProperty("activeAccentColor").colorValue = accentColor;
        so.FindProperty("dragController").objectReferenceValue = dragCtrl;
        so.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    // =====================================================================
    // UI Primitive Builders
    // =====================================================================

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

    private static void SectionLabel(Transform parent, string text)
    {
        TextMeshProUGUI label = NewText("Label_" + text, parent, text, 7f, DimText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetLE(label.gameObject, 0f, 9f, 1f, 0f);
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private static Button NewButton(string name, Transform parent, string label, Color color, float fontSize)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;

        AddLayout(rt.gameObject, false, 0f, new RectOffset(2, 2, 0, 0), TextAnchor.MiddleCenter);
        TextMeshProUGUI txt = NewText("Text", rt, label, fontSize, Color.white, TextAlignmentOptions.Midline, FontStyles.Bold);
        SetLE(txt.gameObject, 0f, 0f, 1f, 1f);

        return btn;
    }
}

