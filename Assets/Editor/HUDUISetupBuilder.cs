using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility that generates HUDUI sprites, assigns default weapon calibers,
/// creates Assets/Prefabs/UI/HUDConsumableSlot.prefab, and builds the HUDUI.pen
/// minimal tactical HUD under HUD Canvas/HUDRoot in the active scene.
/// Leaves LoadoutMenu (invUI) and blackscreen intact.
/// </summary>
public static class HUDUISetupBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string ConsumableSlotPrefabPath = PrefabFolder + "/HUDConsumableSlot.prefab";
    private const string SpriteFolder = "Assets/Sprites/UI";
    private const string FontAssetFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";

    // Exact HUDUI.pen v2.22 Color Tokens (parsed via ColorUtility to avoid float rounding drift)
    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static readonly Color BgSurface = Hex("#0F1114");
    private static readonly Color BgInset = Hex("#090A0C");
    private static readonly Color BorderSubtle = Hex("#23272D");
    private static readonly Color BorderDim = Hex("#191C21");
    private static readonly Color TextPrimary = Hex("#F1F3F5");
    private static readonly Color TextSecondary = Hex("#8A919A");
    private static readonly Color TextMuted = Hex("#4F555E");
    private static readonly Color AccentGreen = Hex("#22C55E");
    private static readonly Color AccentGreenBg = Hex("#0A2214");
    private static readonly Color AccentGreenBorder = Hex("#166534");
    private static readonly Color AccentCoin = Hex("#C8A44D");

    private class HUDAssets
    {
        public TMP_FontAsset uiSemiBold;
        public TMP_FontAsset monoRegular;
        public TMP_FontAsset monoSemiBold;

        public Sprite rounded4Px;
        public Sprite rounded3Px;
        public Sprite rounded1Px;
        public Sprite circleDot;
        public Sprite iconCrosshair;
        public Sprite iconCoins;
    }

    [MenuItem("Tools/Darkzone/Build In-Game HUD")]
    public static void BuildAll()
    {
        EnsureDirectories();
        AssignWeaponCalibers();

        HUDAssets assets = EnsureAssets();
        GameObject consumableSlotPrefab = CreateConsumableSlotPrefab(assets);

        BuildHUDHierarchy(consumableSlotPrefab, assets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HUDUISetupBuilder] Successfully built In-Game HUD.");
    }

    private static void EnsureDirectories()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "UI");
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets/Sprites", "UI");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string combined = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(combined))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void AssignWeaponCalibers()
    {
        SetCaliber("Assets/Weapons/Pistol.asset", "9MM");
        SetCaliber("Assets/Weapons/SMG.asset", "9MM");
        SetCaliber("Assets/Weapons/AssaultRifle.asset", "5.56MM");
        SetCaliber("Assets/Weapons/Shotgun.asset", "12GA");
        SetCaliber("Assets/Weapons/SniperRifle.asset", ".308 WIN");
    }

    private static void SetCaliber(string assetPath, string caliberValue)
    {
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(assetPath);
        if (weapon == null) return;
        weapon.caliber = caliberValue;
        EditorUtility.SetDirty(weapon);
    }

    private static HUDAssets EnsureAssets()
    {
        HUDAssets assets = new HUDAssets();
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/LiberationSans SDF.asset");
        assets.uiSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/Inter-SemiBold SDF.asset") ?? fallback;
        assets.monoRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/JetBrainsMono-Regular SDF.asset") ?? fallback;
        assets.monoSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/JetBrainsMono-SemiBold SDF.asset") ?? assets.monoRegular;

        assets.rounded4Px = GenerateSprite("ui_rounded_4px", 24, 24, new Vector4(6f, 6f, 6f, 6f), DrawRoundedRect4Px);
        assets.rounded3Px = GenerateSprite("ui_rounded_3px", 24, 24, new Vector4(5f, 5f, 5f, 5f), DrawRoundedRect3Px);
        assets.rounded1Px = GenerateSprite("ui_rounded_1px", 16, 16, new Vector4(3f, 3f, 3f, 3f), DrawRoundedRect1Px);
        assets.iconCoins = GenerateSprite("icon_coins", 64, 64, Vector4.zero, DrawCoinsIcon);

        assets.circleDot = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/ui_circle_dot.png");
        assets.iconCrosshair = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/icon_crosshair.png");

        return assets;
    }

    private static Sprite GenerateSprite(string fileName, int width, int height, Vector4 border, Func<int, int, Color[]> pixelGenerator)
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

            if (border != Vector4.zero)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = border;
            }

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    // =====================================================================
    // Procedural Anti-Aliased Sprite Rasterizers
    // =====================================================================

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

    private static float SdBox(Vector2 p, Vector2 halfSize)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize;
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    private static float SdRoundedBox(int w, int h, float x, float y, float radius)
    {
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        Vector2 half = new Vector2(w * 0.5f - radius, h * 0.5f - radius);
        return SdBox(new Vector2(x, y) - center, half) - radius;
    }

    private static Color[] DrawRoundedRect4Px(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        PaintCoverage(px, w, h, (x, y) => SdRoundedBox(w, h, x, y, 4.0f));
        return px;
    }

    private static Color[] DrawRoundedRect3Px(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        PaintCoverage(px, w, h, (x, y) => SdRoundedBox(w, h, x, y, 3.0f));
        return px;
    }

    private static Color[] DrawRoundedRect1Px(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        PaintCoverage(px, w, h, (x, y) => SdRoundedBox(w, h, x, y, 1.0f));
        return px;
    }

    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    private static Color[] DrawCoinsIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.2f;
        Vector2 cFront = new Vector2(26f, 26f);
        Vector2 cBack = new Vector2(39f, 38f);
        float r = 13.5f;

        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            // Front coin ring + inner vertical emblem line
            float frontRing = Mathf.Abs((p - cFront).magnitude - r);
            float frontSlash = SdSegment(p, cFront + new Vector2(0f, -5.5f), cFront + new Vector2(0f, 5.5f));
            float d = Mathf.Min(frontRing, frontSlash);

            // Back coin ring (masked outside front coin interior)
            float distFront = (p - cFront).magnitude;
            if (distFront > r + 1.0f)
            {
                float backRing = Mathf.Abs((p - cBack).magnitude - r);
                d = Mathf.Min(d, backRing);
            }

            return d - stroke;
        });
        return px;
    }

    // =====================================================================
    // Prefab: Assets/Prefabs/UI/HUDConsumableSlot.prefab
    // =====================================================================

    private static GameObject CreateConsumableSlotPrefab(HUDAssets assets)
    {
        GameObject root = new GameObject("HUDConsumableSlot", typeof(RectTransform));
        try
        {
            AddRoundedImage(root, assets.rounded4Px, BorderSubtle);
            SetLE(root, 200f, 62f, 0f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            AddRoundedImage(inner.gameObject, assets.rounded4Px, BgSurface);
            AddLayout(inner.gameObject, false, 12f, new RectOffset(10, 14, 10, 10), TextAnchor.MiddleLeft);

            // Keybind Box (36x36)
            RectTransform keyBorder = NewRect("KeybindBoxBorder", inner);
            AddRoundedImage(keyBorder.gameObject, assets.rounded3Px, BorderSubtle);
            SetLE(keyBorder.gameObject, 36f, 36f, 0f, 0f);

            RectTransform keyFill = NewRect("KeybindBoxFill", keyBorder);
            Stretch(keyFill, 1f, 1f, 1f, 1f);
            AddRoundedImage(keyFill.gameObject, assets.rounded3Px, BgInset);

            TextMeshProUGUI keyText = NewText("Keybind", keyFill, "3", 12f, TextMuted, TextAlignmentOptions.Center, assets.monoSemiBold);
            keyText.overflowMode = TextOverflowModes.Overflow;
            Stretch(keyText.rectTransform);

            // Slot Info column
            RectTransform info = NewRect("SlotInfo", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 1f);
            AddLayout(info.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            RectTransform header = NewRect("SlotHeader", info);
            SetLE(header.gameObject, 0f, 15f, 1f, 0f);
            AddLayout(header.gameObject, false, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI slotLabel = NewText("SlotLabel", header, "SLOT 1 [3]", 10f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            slotLabel.characterSpacing = 4f;
            SetLE(slotLabel.gameObject, 0f, 15f, 1f, 0f);

            TextMeshProUGUI countText = NewText("ItemCount", header, "x0", 10f, TextMuted, TextAlignmentOptions.MidlineRight, assets.monoSemiBold);
            countText.overflowMode = TextOverflowModes.Overflow;
            SetLE(countText.gameObject, -1f, 15f, 0f, 0f);

            TextMeshProUGUI itemName = NewText("ItemName", info, "EMPTY", 13f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
            itemName.characterSpacing = 2f;
            SetLE(itemName.gameObject, 0f, 20f, 1f, 0f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ConsumableSlotPrefabPath);
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Scene Hierarchy: HUD Canvas/HUDRoot
    // =====================================================================

    private static void BuildHUDHierarchy(GameObject consumableSlotPrefab, HUDAssets assets)
    {
        GameObject canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[HUDUISetupBuilder] Could not find 'HUD Canvas' in the active scene.");
            return;
        }

        // Ensure vertex colors render in sRGB/Gamma even under Linear color space (matches Pen.dev exact hexes)
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.vertexColorAlwaysGammaSpace = true;
            EditorUtility.SetDirty(canvas);
        }

        // Ensure 1920x1080 16:9 CanvasScaler
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            EditorUtility.SetDirty(scaler);
        }

        // Clean legacy HUD objects (leave blackscreen & LoadoutMenu intact)
        DestroyChildIfExists(canvasObj.transform, "AmmoPanel");
        DestroyChildIfExists(canvasObj.transform, "HealthPanel");
        DestroyChildIfExists(canvasObj.transform, "HUDRoot");
        DestroyChildIfExists(canvasObj.transform, "ConsumablesBar");

        // Create HUDRoot as first sibling so blackscreen & LoadoutMenu render on top
        RectTransform hudRoot = NewRect("HUDRoot", canvasObj.transform);
        Stretch(hudRoot);
        hudRoot.SetSiblingIndex(0);

        // Preserve blackscreen above HUDRoot, LoadoutMenu last
        Transform blackscreen = canvasObj.transform.Find("blackscreen");
        if (blackscreen != null)
            blackscreen.SetSiblingIndex(1);

        Transform loadoutMenu = canvasObj.transform.Find("LoadoutMenu");
        if (loadoutMenu != null)
            loadoutMenu.SetAsLastSibling();

        // 1. Top-Left: StatusTag (OP-01 // STANDBY <-> ACTIVE)
        BuildStatusTag(hudRoot, canvasObj, assets);

        // 2. Top-Right: CoinCounter (CREDITS)
        TextMeshProUGUI coinValueText = BuildCoinCounter(hudRoot, assets);

        // 3. Bottom-Left: HealthBarModule (01 // VITALS)
        BuildHealthModule(hudRoot, assets);

        // 4. Bottom-Center: ConsumablesModule (02 // CONSUMABLES)
        ConsumableHUD.ConsumableSlotView[] slotViews = BuildConsumablesModule(hudRoot, consumableSlotPrefab, assets);

        // 5. Bottom-Right: WeaponAmmoModule
        BuildWeaponAmmoModule(hudRoot, assets);

        // Wire ConsumableHUD on HUD Canvas
        ConsumableHUD consumableHud = canvasObj.GetComponent<ConsumableHUD>() ?? canvasObj.AddComponent<ConsumableHUD>();
        SerializedObject chSo = new SerializedObject(consumableHud);
        SerializedProperty viewsProp = chSo.FindProperty("slotViews");
        viewsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            SerializedProperty elem = viewsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("keybindText").objectReferenceValue = slotViews[i].keybindText;
            elem.FindPropertyRelative("slotLabelText").objectReferenceValue = slotViews[i].slotLabelText;
            elem.FindPropertyRelative("countText").objectReferenceValue = slotViews[i].countText;
            elem.FindPropertyRelative("itemNameText").objectReferenceValue = slotViews[i].itemNameText;
        }
        chSo.FindProperty("coinValueText").objectReferenceValue = coinValueText;
        chSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(canvasObj);
        EditorSceneManager.MarkSceneDirty(canvasObj.scene);
        EditorSceneManager.SaveScene(canvasObj.scene);
    }

    private static void BuildStatusTag(RectTransform hudRoot, GameObject canvasObj, HUDAssets assets)
    {
        RectTransform tagBorder = NewRect("StatusTag", hudRoot);
        tagBorder.anchorMin = new Vector2(0f, 1f);
        tagBorder.anchorMax = new Vector2(0f, 1f);
        tagBorder.pivot = new Vector2(0f, 1f);
        tagBorder.anchoredPosition = new Vector2(36f, -36f);
        AddRoundedImage(tagBorder.gameObject, assets.rounded4Px, BorderSubtle);

        HorizontalLayoutGroup hlg = tagBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = tagBorder.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform fill = NewRect("Fill", tagBorder);
        Stretch(fill, 1f, 1f, 1f, 1f);
        AddRoundedImage(fill.gameObject, assets.rounded4Px, BgSurface);
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        RectTransform dotRt = NewRect("LiveDot", tagBorder);
        Image dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.sprite = assets.circleDot;
        dotImg.color = AccentCoin;
        dotImg.raycastTarget = false;
        SetLE(dotRt.gameObject, 6f, 6f, 0f, 0f);

        TextMeshProUGUI unitLabel = NewText("UnitLabel", tagBorder, "OP-01 // STANDBY", 11f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        unitLabel.characterSpacing = 5f;
        unitLabel.overflowMode = TextOverflowModes.Overflow;
        SetLE(unitLabel.gameObject, -1f, 18f, 0f, 0f);

        // Attach HUDStatusController on always-active HUD Canvas so LateUpdate always tracks LoadoutUI.IsOpen
        HUDStatusController statusCtrl = canvasObj.GetComponent<HUDStatusController>() ?? canvasObj.AddComponent<HUDStatusController>();
        SerializedObject so = new SerializedObject(statusCtrl);
        so.FindProperty("hudRoot").objectReferenceValue = hudRoot.gameObject;
        so.FindProperty("statusDot").objectReferenceValue = dotImg;
        so.FindProperty("statusText").objectReferenceValue = unitLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI BuildCoinCounter(RectTransform hudRoot, HUDAssets assets)
    {
        RectTransform cardBorder = NewRect("CoinCounter", hudRoot);
        cardBorder.anchorMin = new Vector2(1f, 1f);
        cardBorder.anchorMax = new Vector2(1f, 1f);
        cardBorder.pivot = new Vector2(1f, 1f);
        cardBorder.anchoredPosition = new Vector2(-36f, -36f);
        AddRoundedImage(cardBorder.gameObject, assets.rounded4Px, BorderSubtle);

        HorizontalLayoutGroup hlg = cardBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 16, 10, 10);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = cardBorder.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform fill = NewRect("Fill", cardBorder);
        Stretch(fill, 1f, 1f, 1f, 1f);
        AddRoundedImage(fill.gameObject, assets.rounded4Px, BgSurface);
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // Coin Icon Box (32x32)
        RectTransform iconBoxBorder = NewRect("CoinIconBoxBorder", cardBorder);
        AddRoundedImage(iconBoxBorder.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(iconBoxBorder.gameObject, 32f, 32f, 0f, 0f);

        RectTransform iconBoxFill = NewRect("CoinIconBoxFill", iconBoxBorder);
        Stretch(iconBoxFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(iconBoxFill.gameObject, assets.rounded3Px, BgInset);

        RectTransform iconRt = NewRect("CoinIcon", iconBoxFill);
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(16f, 16f);
        Image coinIcon = iconRt.gameObject.AddComponent<Image>();
        coinIcon.sprite = assets.iconCoins;
        coinIcon.color = AccentCoin;
        coinIcon.preserveAspect = true;
        coinIcon.raycastTarget = false;

        // Coin Text Column
        RectTransform textCol = NewRect("CoinText", cardBorder);
        AddLayout(textCol.gameObject, true, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
        SetLE(textCol.gameObject, -1f, 42f, 0f, 0f);

        TextMeshProUGUI label = NewText("CoinLabel", textCol, "CREDITS", 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        label.characterSpacing = 5f;
        label.overflowMode = TextOverflowModes.Overflow;
        SetLE(label.gameObject, -1f, 14f, 0f, 0f);

        TextMeshProUGUI value = NewText("CoinValue", textCol, "00,000", 18f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.monoSemiBold);
        value.characterSpacing = 2f;
        value.overflowMode = TextOverflowModes.Overflow;
        SetLE(value.gameObject, -1f, 26f, 0f, 0f);

        return value;
    }

    private static void BuildHealthModule(RectTransform hudRoot, HUDAssets assets)
    {
        RectTransform modBorder = NewRect("HealthBarModule", hudRoot);
        modBorder.anchorMin = new Vector2(0f, 0f);
        modBorder.anchorMax = new Vector2(0f, 0f);
        modBorder.pivot = new Vector2(0f, 0f);
        modBorder.anchoredPosition = new Vector2(36f, 36f);
        modBorder.sizeDelta = new Vector2(340f, 100f);
        AddRoundedImage(modBorder.gameObject, assets.rounded4Px, BorderSubtle);

        RectTransform inner = NewRect("InnerFill", modBorder);
        Stretch(inner, 1f, 1f, 1f, 1f);
        AddRoundedImage(inner.gameObject, assets.rounded4Px, BgSurface);
        AddLayout(inner.gameObject, true, 10f, new RectOffset(16, 16, 14, 14), TextAnchor.UpperLeft);

        // Header
        RectTransform header = NewRect("HealthHeader", inner);
        SetLE(header.gameObject, 0f, 22f, 1f, 0f);
        AddLayout(header.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        RectTransform titleGroup = NewRect("HealthTitleGroup", header);
        SetLE(titleGroup.gameObject, 0f, 22f, 1f, 0f);
        AddLayout(titleGroup.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        RectTransform dotRt = NewRect("HealthDot", titleGroup);
        Image dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.sprite = assets.circleDot;
        dotImg.color = AccentGreen;
        dotImg.raycastTarget = false;
        SetLE(dotRt.gameObject, 6f, 6f, 0f, 0f);

        TextMeshProUGUI lbl = NewText("HealthLabel", titleGroup, "01 // VITALS", 10f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        lbl.characterSpacing = 5f;
        SetLE(lbl.gameObject, -1f, 18f, 0f, 0f);

        TextMeshProUGUI numeric = NewText("HealthNumeric", header, "100 / 100", 15f, TextPrimary, TextAlignmentOptions.MidlineRight, assets.monoSemiBold);
        numeric.overflowMode = TextOverflowModes.Overflow;
        SetLE(numeric.gameObject, -1f, 22f, 0f, 0f);

        // Segments Row (10 segments, 10px high, 4px gap)
        RectTransform segRow = NewRect("HealthSegments", inner);
        SetLE(segRow.gameObject, 0f, 10f, 1f, 0f);
        HorizontalLayoutGroup segLayout = segRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        segLayout.spacing = 4f;
        segLayout.childAlignment = TextAnchor.MiddleLeft;
        segLayout.childControlWidth = true;
        segLayout.childControlHeight = true;
        segLayout.childForceExpandWidth = true;
        segLayout.childForceExpandHeight = true;

        Image[] segBorders = new Image[10];
        Image[] segFills = new Image[10];
        for (int i = 0; i < 10; i++)
        {
            RectTransform segBorderRt = NewRect($"Segment_{i + 1}", segRow);
            segBorders[i] = AddRoundedImage(segBorderRt.gameObject, assets.rounded1Px, AccentGreenBorder);

            RectTransform segFillRt = NewRect("Fill", segBorderRt);
            Stretch(segFillRt, 1f, 1f, 1f, 1f);
            segFills[i] = AddRoundedImage(segFillRt.gameObject, assets.rounded1Px, AccentGreen);
        }

        // Footer
        RectTransform footer = NewRect("HealthFooter", inner);
        SetLE(footer.gameObject, 0f, 14f, 1f, 0f);
        AddLayout(footer.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI statusTxt = NewText("HealthStatus", footer, "STATUS // NOMINAL", 9f, AccentGreen, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        statusTxt.characterSpacing = 4f;
        SetLE(statusTxt.gameObject, 0f, 14f, 1f, 0f);

        TextMeshProUGUI pctTxt = NewText("HealthPercent", footer, "100%", 9f, TextMuted, TextAlignmentOptions.MidlineRight, assets.monoRegular);
        pctTxt.characterSpacing = 4f;
        SetLE(pctTxt.gameObject, -1f, 14f, 0f, 0f);

        // Attach PlayerHealthUI
        PlayerHealthUI healthUI = modBorder.gameObject.AddComponent<PlayerHealthUI>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        PlayerHealth playerHealth = playerObj != null ? playerObj.GetComponent<PlayerHealth>() : null;

        SerializedObject so = new SerializedObject(healthUI);
        so.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        so.FindProperty("healthDot").objectReferenceValue = dotImg;
        so.FindProperty("healthNumericText").objectReferenceValue = numeric;
        so.FindProperty("healthStatusText").objectReferenceValue = statusTxt;
        so.FindProperty("healthPercentText").objectReferenceValue = pctTxt;

        SerializedProperty bordersProp = so.FindProperty("segmentBorders");
        SerializedProperty fillsProp = so.FindProperty("segmentFills");
        bordersProp.arraySize = 10;
        fillsProp.arraySize = 10;
        for (int i = 0; i < 10; i++)
        {
            bordersProp.GetArrayElementAtIndex(i).objectReferenceValue = segBorders[i];
            fillsProp.GetArrayElementAtIndex(i).objectReferenceValue = segFills[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ConsumableHUD.ConsumableSlotView[] BuildConsumablesModule(RectTransform hudRoot, GameObject slotPrefab, HUDAssets assets)
    {
        RectTransform mod = NewRect("ConsumablesModule", hudRoot);
        mod.anchorMin = new Vector2(0.5f, 0f);
        mod.anchorMax = new Vector2(0.5f, 0f);
        mod.pivot = new Vector2(0.5f, 0f);
        mod.anchoredPosition = new Vector2(0f, 36f);
        mod.sizeDelta = new Vector2(824f, 86f);
        AddLayout(mod.gameObject, true, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        TextMeshProUGUI title = NewText("ConsumablesTitle", mod, "02 // CONSUMABLES", 10f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        title.characterSpacing = 5f;
        SetLE(title.gameObject, 0f, 16f, 1f, 0f);

        RectTransform row = NewRect("ConsumablesRow", mod);
        SetLE(row.gameObject, 824f, 62f, 0f, 0f);
        AddLayout(row.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        string[] keys = new[] { "3", "4", "5", "6" };
        ConsumableHUD.ConsumableSlotView[] views = new ConsumableHUD.ConsumableSlotView[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject slotGo = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, row);
            slotGo.name = $"ConsumableSlot_{i + 1}";

            TextMeshProUGUI kTxt = slotGo.transform.Find("InnerFill/KeybindBoxBorder/KeybindBoxFill/Keybind").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI lblTxt = slotGo.transform.Find("InnerFill/SlotInfo/SlotHeader/SlotLabel").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI cntTxt = slotGo.transform.Find("InnerFill/SlotInfo/SlotHeader/ItemCount").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI nmTxt = slotGo.transform.Find("InnerFill/SlotInfo/ItemName").GetComponent<TextMeshProUGUI>();

            kTxt.text = keys[i];
            lblTxt.text = $"SLOT {i + 1} [{keys[i]}]";
            cntTxt.text = "x0";
            nmTxt.text = "EMPTY";

            views[i] = new ConsumableHUD.ConsumableSlotView
            {
                keybindText = kTxt,
                slotLabelText = lblTxt,
                countText = cntTxt,
                itemNameText = nmTxt
            };
        }

        return views;
    }

    private static void BuildWeaponAmmoModule(RectTransform hudRoot, HUDAssets assets)
    {
        RectTransform modBorder = NewRect("WeaponAmmoModule", hudRoot);
        modBorder.anchorMin = new Vector2(1f, 0f);
        modBorder.anchorMax = new Vector2(1f, 0f);
        modBorder.pivot = new Vector2(1f, 0f);
        modBorder.anchoredPosition = new Vector2(-36f, 36f);
        modBorder.sizeDelta = new Vector2(350f, 88f);
        AddRoundedImage(modBorder.gameObject, assets.rounded4Px, BorderSubtle);

        RectTransform inner = NewRect("InnerFill", modBorder);
        Stretch(inner, 1f, 1f, 1f, 1f);
        AddRoundedImage(inner.gameObject, assets.rounded4Px, BgSurface);
        AddLayout(inner.gameObject, false, 14f, new RectOffset(12, 18, 14, 14), TextAnchor.MiddleLeft);

        // Active Accent Bar (3x52)
        RectTransform accentBar = NewRect("ActiveAccentBar", inner);
        Image accentImg = accentBar.gameObject.AddComponent<Image>();
        accentImg.color = AccentGreen;
        accentImg.raycastTarget = false;
        SetLE(accentBar.gameObject, 3f, 54f, 0f, 0f);

        // Weapon Icon Box (48x48)
        RectTransform iconBoxBorder = NewRect("WeaponIconBoxBorder", inner);
        AddRoundedImage(iconBoxBorder.gameObject, assets.rounded3Px, BorderSubtle);
        SetLE(iconBoxBorder.gameObject, 48f, 48f, 0f, 0f);

        RectTransform iconBoxFill = NewRect("WeaponIconBoxFill", iconBoxBorder);
        Stretch(iconBoxFill, 1f, 1f, 1f, 1f);
        AddRoundedImage(iconBoxFill.gameObject, assets.rounded3Px, BgInset);

        RectTransform iconRt = NewRect("WeaponIcon", iconBoxFill);
        Stretch(iconRt, 8f, 8f, 8f, 8f);
        Image weaponIcon = iconRt.gameObject.AddComponent<Image>();
        weaponIcon.sprite = assets.iconCrosshair;
        weaponIcon.color = TextPrimary;
        weaponIcon.preserveAspect = true;
        weaponIcon.raycastTarget = false;

        // Weapon Info (middle column)
        RectTransform info = NewRect("WeaponInfo", inner);
        SetLE(info.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(info.gameObject, true, 3f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI slotLbl = NewText("WeaponSlotLabel", info, "SLOT 1 // PRIMARY", 9f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        slotLbl.characterSpacing = 4f;
        SetLE(slotLbl.gameObject, 0f, 14f, 1f, 0f);

        TextMeshProUGUI wName = NewText("WeaponName", info, "PISTOL", 15f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
        SetLE(wName.gameObject, 0f, 22f, 1f, 0f);

        TextMeshProUGUI wMeta = NewText("WeaponMeta", info, "SEMI  •  9MM", 10f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        wMeta.characterSpacing = 2f;
        SetLE(wMeta.gameObject, 0f, 16f, 1f, 0f);

        // Ammo Counter (right column)
        RectTransform ammoCol = NewRect("AmmoCounter", inner);
        SetLE(ammoCol.gameObject, -1f, -1f, 0f, 1f);
        AddLayout(ammoCol.gameObject, true, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.LowerRight);

        RectTransform numsRow = NewRect("AmmoNumbers", ammoCol);
        SetLE(numsRow.gameObject, -1f, 38f, 0f, 0f);
        AddLayout(numsRow.gameObject, false, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.LowerRight);

        TextMeshProUGUI ammoCur = NewText("AmmoCurrent", numsRow, "12", 28f, TextPrimary, TextAlignmentOptions.BottomRight, assets.monoSemiBold);
        ammoCur.overflowMode = TextOverflowModes.Overflow;
        SetLE(ammoCur.gameObject, -1f, 38f, 0f, 0f);

        TextMeshProUGUI ammoMax = NewText("AmmoMax", numsRow, "/ 12", 13f, TextSecondary, TextAlignmentOptions.BottomLeft, assets.monoSemiBold);
        ammoMax.overflowMode = TextOverflowModes.Overflow;
        SetLE(ammoMax.gameObject, -1f, 24f, 0f, 0f);

        TextMeshProUGUI ammoLbl = NewText("AmmoLabel", ammoCol, "MAG // CAP", 9f, TextMuted, TextAlignmentOptions.MidlineRight, assets.monoRegular);
        ammoLbl.characterSpacing = 4f;
        ammoLbl.overflowMode = TextOverflowModes.Overflow;
        SetLE(ammoLbl.gameObject, -1f, 14f, 0f, 0f);

        // Attach AmmoUI
        AmmoUI ammoUI = modBorder.gameObject.AddComponent<AmmoUI>();
        GameObject playerObj = GameObject.FindWithTag("Player");
        WeaponController wc = playerObj != null
            ? (playerObj.GetComponent<WeaponController>() ?? playerObj.GetComponentInChildren<WeaponController>())
            : null;

        SerializedObject so = new SerializedObject(ammoUI);
        so.FindProperty("weaponController").objectReferenceValue = wc;
        so.FindProperty("activeAccentBar").objectReferenceValue = accentBar.gameObject;
        so.FindProperty("weaponIconImage").objectReferenceValue = weaponIcon;
        so.FindProperty("fallbackWeaponIcon").objectReferenceValue = assets.iconCrosshair;
        so.FindProperty("slotLabelText").objectReferenceValue = slotLbl;
        so.FindProperty("weaponNameText").objectReferenceValue = wName;
        so.FindProperty("weaponMetaText").objectReferenceValue = wMeta;
        so.FindProperty("ammoCurrentText").objectReferenceValue = ammoCur;
        so.FindProperty("ammoMaxText").objectReferenceValue = ammoMax;
        so.FindProperty("ammoLabelText").objectReferenceValue = ammoLbl;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // Primitive Helpers
    // =====================================================================

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        if (t != null)
            UnityEngine.Object.DestroyImmediate(t.gameObject);
    }

    private static Image AddRoundedImage(GameObject go, Sprite roundedSprite, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
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
}
