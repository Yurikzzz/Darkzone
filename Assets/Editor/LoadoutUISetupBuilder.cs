using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility that generates the minimal-black tactical UI assets (TMP SDF fonts + crisp sprites),
/// builds the reusable Loadout/Inventory UI prefabs under Assets/Prefabs/UI/, updates HUD Canvas
/// to a 1920x1080 (16:9) reference resolution, and constructs HUD Canvas/LoadoutMenu.
/// </summary>
public static class LoadoutUISetupBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string InventoryItemPrefabPath = PrefabFolder + "/InventoryItemSlot.prefab";
    private const string LoadoutSlotPrefabPath = PrefabFolder + "/LoadoutEquipmentSlot.prefab";

    private const string SpriteFolder = "Assets/Sprites/UI";
    private const string FontSourceFolder = "Assets/TextMesh Pro/Fonts";
    private const string FontAssetFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";

    // Minimal Black Palette (matching invUI.pen v2.22)
    private static readonly Color BgBackdrop = new Color(0.020f, 0.020f, 0.024f, 0.80f);       // #050506 @ 80%
    private static readonly Color BgVoid = new Color(0.020f, 0.020f, 0.024f, 1f);             // #050506
    private static readonly Color BgSurface = new Color(0.043f, 0.043f, 0.051f, 1f);          // #0B0B0D
    private static readonly Color BgCard = new Color(0.067f, 0.067f, 0.078f, 1f);             // #111114
    private static readonly Color BgEmpty = new Color(0.031f, 0.031f, 0.039f, 1f);            // #08080A
    private static readonly Color BgWell = new Color(0.024f, 0.024f, 0.027f, 1f);             // #060607
    private static readonly Color BorderSubtle = new Color(0.094f, 0.094f, 0.114f, 1f);       // #18181D
    private static readonly Color BorderMedium = new Color(0.153f, 0.153f, 0.180f, 1f);       // #27272E
    private static readonly Color BorderOccupiedWeapon = new Color(0.247f, 0.247f, 0.275f, 1f);// #3F3F46
    private static readonly Color TextPrimary = new Color(0.957f, 0.957f, 0.961f, 1f);        // #F4F4F5
    private static readonly Color TextSecondary = new Color(0.631f, 0.631f, 0.667f, 1f);      // #A1A1AA
    private static readonly Color TextMuted = new Color(0.443f, 0.443f, 0.478f, 1f);          // #71717A
    private static readonly Color TextDim = new Color(0.322f, 0.322f, 0.357f, 1f);            // #52525B
    private static readonly Color StatusActive = new Color(0.290f, 0.871f, 0.502f, 1f);       // #4ADE80
    private static readonly Color StatusActiveBg = new Color(0.031f, 0.090f, 0.055f, 1f);     // #08170E
    private static readonly Color StatusActiveBorder = new Color(0.086f, 0.396f, 0.204f, 1f); // #166534
    private static readonly Color StatusInactive = new Color(0.322f, 0.322f, 0.357f, 1f);     // #52525B
    private static readonly Color DangerText = new Color(0.973f, 0.443f, 0.443f, 1f);         // #F87171
    private static readonly Color DangerBg = new Color(0.102f, 0.043f, 0.051f, 1f);           // #1A0B0D
    private static readonly Color DangerBorder = new Color(0.498f, 0.114f, 0.114f, 1f);       // #7F1D1D

    private class UIAssets
    {
        public TMP_FontAsset uiRegular;
        public TMP_FontAsset uiSemiBold;
        public TMP_FontAsset monoRegular;
        public TMP_FontAsset monoSemiBold;

        public Sprite rounded2Px;
        public Sprite circleDot;
        public Sprite iconClose;
        public Sprite iconSwap;
        public Sprite iconCrosshair;
        public Sprite iconShield;
        public Sprite iconBiohazard;
        public Sprite iconTrash;
    }

    [MenuItem("Tools/Darkzone/Build Loadout UI & Prefabs")]
    public static void BuildAll()
    {
        EnsureDirectories();

        UIAssets assets = EnsureAssets();

        InventoryItemSlotUI itemSlotPrefab = CreateInventoryItemSlotPrefab(assets);
        LoadoutSlotUI loadoutSlotPrefab = CreateLoadoutSlotPrefab(assets);

        BuildSceneHierarchy(itemSlotPrefab, loadoutSlotPrefab, assets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LoadoutUISetupBuilder] Successfully built UI prefabs and HUD Canvas/LoadoutMenu hierarchy.");
    }

    private static void EnsureDirectories()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "UI");
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets/Sprites", "UI");
        EnsureFolder("Assets/TextMesh Pro", "Resources");
        EnsureFolder("Assets/TextMesh Pro/Resources", "Fonts & Materials");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string combined = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(combined))
            AssetDatabase.CreateFolder(parent, child);
    }

    // =====================================================================
    // Asset Generation: Fonts & Sprites
    // =====================================================================

    private static UIAssets EnsureAssets()
    {
        UIAssets assets = new UIAssets();

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetFolder + "/LiberationSans SDF.asset");
        assets.uiRegular = EnsureTMPFontAsset("Inter-Regular", fallback);
        assets.uiSemiBold = EnsureTMPFontAsset("Inter-SemiBold", assets.uiRegular);
        assets.monoRegular = EnsureTMPFontAsset("JetBrainsMono-Regular", assets.uiRegular);
        assets.monoSemiBold = EnsureTMPFontAsset("JetBrainsMono-SemiBold", assets.monoRegular);

        assets.rounded2Px = EnsureSprite("ui_rounded_2px", 24, 24, true, DrawRoundedRect2Px);
        assets.circleDot = EnsureSprite("ui_circle_dot", 64, 64, false, DrawCircleDot);
        assets.iconClose = EnsureSprite("icon_close", 64, 64, false, DrawCloseIcon);
        assets.iconSwap = EnsureSprite("icon_swap", 64, 64, false, DrawSwapIcon);
        assets.iconCrosshair = EnsureSprite("icon_crosshair", 64, 64, false, DrawCrosshairIcon);
        assets.iconShield = EnsureSprite("icon_shield", 64, 64, false, DrawShieldIcon);
        assets.iconBiohazard = EnsureSprite("icon_biohazard", 64, 64, false, DrawBiohazardIcon);
        assets.iconTrash = EnsureSprite("icon_trash", 64, 64, false, DrawTrashIcon);

        return assets;
    }

    private static TMP_FontAsset EnsureTMPFontAsset(string fontBaseName, TMP_FontAsset fallback)
    {
        string assetPath = $"{FontAssetFolder}/{fontBaseName} SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null && existing.material != null && existing.atlasTexture != null)
            return existing;

        string ttfPath = $"{FontSourceFolder}/{fontBaseName}.ttf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (sourceFont == null)
        {
            Debug.LogWarning($"[LoadoutUISetupBuilder] Source font not found at '{ttfPath}', using fallback.");
            return fallback;
        }

        if (existing != null)
            AssetDatabase.DeleteAsset(assetPath);

        FontEngine.InitializeFontEngine();
        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            64,
            6,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (created == null)
        {
            Debug.LogWarning($"[LoadoutUISetupBuilder] Failed to create TMP_FontAsset for '{ttfPath}', using fallback.");
            return fallback;
        }

        created.name = $"{fontBaseName} SDF";
        AssetDatabase.CreateAsset(created, assetPath);

        if (created.atlasTextures != null)
        {
            for (int i = 0; i < created.atlasTextures.Length; i++)
            {
                Texture2D atlas = created.atlasTextures[i];
                if (atlas == null) continue;
                atlas.name = $"{created.name} Atlas {(i == 0 ? "" : i.ToString())}".Trim();
                if (!AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, created);
            }
        }

        if (created.material != null)
        {
            created.material.name = $"{created.name} Material";
            if (!AssetDatabase.Contains(created.material))
                AssetDatabase.AddObjectToAsset(created.material, created);
        }

        const string preloadChars =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·•—–…";
        created.TryAddCharacters(preloadChars, out _);

        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) ?? created;
    }

    private static Sprite EnsureSprite(string fileName, int width, int height, bool nineSlice, Func<int, int, Color[]> pixelGenerator)
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

            if (nineSlice)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = new Vector4(4f, 4f, 4f, 4f);
                importer.SetTextureSettings(settings);
                importer.spriteBorder = new Vector4(4f, 4f, 4f, 4f);
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

    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    private static float SdBox(Vector2 p, Vector2 halfSize)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize;
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    private static float SdCircle(Vector2 p, Vector2 center, float r)
    {
        return (p - center).magnitude - r;
    }

    private static Color[] DrawRoundedRect2Px(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float radius = 2.0f;
        Vector2 half = new Vector2(w * 0.5f - radius, h * 0.5f - radius);
        PaintCoverage(px, w, h, (x, y) => SdBox(new Vector2(x, y) - center, half) - radius);
        return px;
    }

    private static Color[] DrawCircleDot(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
        PaintCoverage(px, w, h, (x, y) => SdCircle(new Vector2(x, y), c, w * 0.44f));
        return px;
    }

    private static Color[] DrawCloseIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.6f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float d1 = SdSegment(p, new Vector2(16f, 16f), new Vector2(48f, 48f));
            float d2 = SdSegment(p, new Vector2(48f, 16f), new Vector2(16f, 48f));
            return Mathf.Min(d1, d2) - stroke;
        });
        return px;
    }

    private static Color[] DrawSwapIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.3f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            // Up arrow (left)
            float d = SdSegment(p, new Vector2(21f, 13f), new Vector2(21f, 51f));
            d = Mathf.Min(d, SdSegment(p, new Vector2(13f, 42f), new Vector2(21f, 51f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(29f, 42f), new Vector2(21f, 51f)));
            // Down arrow (right)
            d = Mathf.Min(d, SdSegment(p, new Vector2(43f, 13f), new Vector2(43f, 51f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(35f, 22f), new Vector2(43f, 13f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(51f, 22f), new Vector2(43f, 13f)));
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawCrosshairIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.2f;
        Vector2 c = new Vector2(32f, 32f);
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float ring = Mathf.Abs(SdCircle(p, c, 20f));
            float d = ring;
            d = Mathf.Min(d, SdSegment(p, new Vector2(32f, 6f), new Vector2(32f, 17f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(32f, 47f), new Vector2(32f, 58f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(6f, 32f), new Vector2(17f, 32f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(47f, 32f), new Vector2(58f, 32f)));
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawShieldIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.3f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float d = SdSegment(p, new Vector2(32f, 55f), new Vector2(13f, 47f));
            d = Mathf.Min(d, SdSegment(p, new Vector2(32f, 55f), new Vector2(51f, 47f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(13f, 47f), new Vector2(13f, 29f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(51f, 47f), new Vector2(51f, 29f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(13f, 29f), new Vector2(32f, 9f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(51f, 29f), new Vector2(32f, 9f)));
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawBiohazardIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.1f;
        Vector2 c = new Vector2(32f, 31f);
        Vector2[] lobes = new[]
        {
            c + new Vector2(0f, 12.5f),
            c + new Vector2(-10.8f, -6.25f),
            c + new Vector2(10.8f, -6.25f)
        };
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float d = Mathf.Abs(SdCircle(p, c, 5.0f));
            float outerArc = Mathf.Abs(SdCircle(p, c, 15.5f));
            d = Mathf.Min(d, outerArc);
            for (int i = 0; i < 3; i++)
            {
                float lobeRing = Mathf.Abs(SdCircle(p, lobes[i], 10.5f));
                d = Mathf.Min(d, lobeRing);
            }
            return d - stroke;
        });
        return px;
    }

    private static Color[] DrawTrashIcon(int w, int h)
    {
        Color[] px = BlankPixels(w, h);
        float stroke = 2.2f;
        PaintCoverage(px, w, h, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            // Lid
            float d = SdSegment(p, new Vector2(12f, 47f), new Vector2(52f, 47f));
            // Handle
            d = Mathf.Min(d, SdSegment(p, new Vector2(25f, 47f), new Vector2(25f, 54f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(25f, 54f), new Vector2(39f, 54f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(39f, 54f), new Vector2(39f, 47f)));
            // Bucket
            d = Mathf.Min(d, SdSegment(p, new Vector2(18f, 47f), new Vector2(20f, 12f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(46f, 47f), new Vector2(44f, 12f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(20f, 12f), new Vector2(44f, 12f)));
            // Ribs
            d = Mathf.Min(d, SdSegment(p, new Vector2(27f, 20f), new Vector2(27f, 39f)));
            d = Mathf.Min(d, SdSegment(p, new Vector2(37f, 20f), new Vector2(37f, 39f)));
            return d - stroke;
        });
        return px;
    }

    // =====================================================================
    // Prefab 1: Assets/Prefabs/UI/InventoryItemSlot.prefab
    // =====================================================================

    private static InventoryItemSlotUI CreateInventoryItemSlotPrefab(UIAssets assets)
    {
        GameObject root = new GameObject("InventoryItemSlot", typeof(RectTransform));
        try
        {
            Image borderImg = AddRoundedImage(root, assets.rounded2Px, BorderMedium);
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            InventoryItemSlotUI slotComp = root.AddComponent<InventoryItemSlotUI>();
            SetLE(root, 0f, 84f, 1f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            Image innerImg = AddRoundedImage(inner.gameObject, assets.rounded2Px, BgCard);
            AddLayout(inner.gameObject, false, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.MiddleLeft);

            // Thumbnail box (72x54 with 1px subtle border)
            RectTransform thumbBorder = NewRect("ThumbBoxBorder", inner);
            Image thumbBorderImg = AddRoundedImage(thumbBorder.gameObject, assets.rounded2Px, BorderSubtle);
            SetLE(thumbBorder.gameObject, 72f, 54f, 0f, 0f);

            RectTransform thumbFill = NewRect("ThumbBoxFill", thumbBorder);
            Stretch(thumbFill, 1f, 1f, 1f, 1f);
            Image thumbImg = AddRoundedImage(thumbFill.gameObject, assets.rounded2Px, BgWell);

            RectTransform iconRt = NewRect("Icon", thumbFill);
            Stretch(iconRt, 8f, 8f, 8f, 8f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Title + Stats column
            RectTransform info = NewRect("Info", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 1f);
            AddLayout(info.gameObject, true, 5f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI titleText = NewText("TitleText", info, "Item Name", 15f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
            SetLE(titleText.gameObject, 0f, 22f, 1f, 0f);

            TextMeshProUGUI statsText = NewText("StatsText", info, "DMG 18  •  MAG 12  •  SEMI", 11f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            SetLE(statsText.gameObject, 0f, 16f, 1f, 0f);

            // Right Item Actions (Equipped Slot Badge + Unequip Button + Trash Button)
            RectTransform actions = NewRect("ItemActions", inner);
            SetLE(actions.gameObject, -1f, 28f, 0f, 0f);
            AddLayout(actions.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleRight);

            // Equipped Slot Badge
            RectTransform badgeBorder = NewRect("EquippedBadge", actions);
            Image badgeBorderImg = AddRoundedImage(badgeBorder.gameObject, assets.rounded2Px, StatusActiveBorder);
            SetLE(badgeBorder.gameObject, -1f, 26f, 0f, 0f);

            RectTransform badgeBg = NewRect("Fill", badgeBorder);
            Stretch(badgeBg, 1f, 1f, 1f, 1f);
            Image badgeBgImg = AddRoundedImage(badgeBg.gameObject, assets.rounded2Px, StatusActiveBg);
            // Also size the outer badge to fit contents via ContentSizeFitter
            HorizontalLayoutGroup badgeLayout = badgeBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeLayout.padding = new RectOffset(10, 10, 5, 5);
            badgeLayout.spacing = 6f;
            badgeLayout.childAlignment = TextAnchor.MiddleCenter;
            badgeLayout.childControlWidth = true;
            badgeLayout.childControlHeight = true;
            badgeLayout.childForceExpandWidth = false;
            badgeLayout.childForceExpandHeight = false;
            ContentSizeFitter badgeFitter = badgeBorder.gameObject.AddComponent<ContentSizeFitter>();
            badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            badgeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Keep Fill stretched via LayoutElement.ignoreLayout
            LayoutElement bgIgnore = badgeBg.gameObject.AddComponent<LayoutElement>();
            bgIgnore.ignoreLayout = true;

            RectTransform dotRt = NewRect("Dot", badgeBorder);
            Image dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = assets.circleDot;
            dotImg.color = StatusActive;
            dotImg.raycastTarget = false;
            SetLE(dotRt.gameObject, 5f, 5f, 0f, 0f);

            TextMeshProUGUI badgeText = NewText("Text", badgeBorder, "SLOT 1 · ACTIVE", 11f, StatusActive, TextAlignmentOptions.Center, assets.monoSemiBold);
            badgeText.characterSpacing = 5f;
            SetLE(badgeText.gameObject, -1f, 16f, 0f, 0f);
            badgeBorder.gameObject.SetActive(false);

            // Inventory Unequip Button
            Button unequipBtn = CreateMinimalButton(
                "UnequipButton", actions, "UNEQUIP",
                DangerBg, DangerBorder, DangerText,
                10f, assets.monoSemiBold, assets.rounded2Px,
                new RectOffset(10, 10, 4, 4), 26f);
            unequipBtn.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 5f;
            unequipBtn.gameObject.SetActive(false);

            // Trash Button (26x26)
            Button trashBtn = CreateIconButton(
                "TrashButton", actions,
                BgWell, BorderMedium, TextSecondary,
                assets.iconTrash, assets.rounded2Px,
                26f, 26f, 13f);

            // Wire serialized fields on InventoryItemSlotUI
            SerializedObject so = new SerializedObject(slotComp);
            so.FindProperty("borderImage").objectReferenceValue = borderImg;
            so.FindProperty("innerImage").objectReferenceValue = innerImg;
            so.FindProperty("thumbBoxBorderImage").objectReferenceValue = thumbBorderImg;
            so.FindProperty("thumbBoxImage").objectReferenceValue = thumbImg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("equippedBadge").objectReferenceValue = badgeBorder.gameObject;
            so.FindProperty("equippedBadgeBorder").objectReferenceValue = badgeBorderImg;
            so.FindProperty("equippedBadgeBg").objectReferenceValue = badgeBgImg;
            so.FindProperty("equippedBadgeDot").objectReferenceValue = dotImg;
            so.FindProperty("equippedBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("unequipButton").objectReferenceValue = unequipBtn;
            so.FindProperty("trashButton").objectReferenceValue = trashBtn;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, InventoryItemPrefabPath);
            return saved.GetComponent<InventoryItemSlotUI>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Prefab 2: Assets/Prefabs/UI/LoadoutEquipmentSlot.prefab
    // =====================================================================

    private static LoadoutSlotUI CreateLoadoutSlotPrefab(UIAssets assets)
    {
        GameObject root = new GameObject("LoadoutEquipmentSlot", typeof(RectTransform));
        try
        {
            Image borderImg = AddRoundedImage(root, assets.rounded2Px, BorderSubtle);
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            LoadoutSlotUI slotComp = root.AddComponent<LoadoutSlotUI>();
            SetLE(root, 0f, 102f, 1f, 0f);

            RectTransform inner = NewRect("InnerFill", root.transform);
            Stretch(inner, 1f, 1f, 1f, 1f);
            Image innerImg = AddRoundedImage(inner.gameObject, assets.rounded2Px, BgEmpty);
            AddLayout(inner.gameObject, false, 14f, new RectOffset(14, 14, 14, 14), TextAnchor.MiddleLeft);

            // Active Left Accent Bar (2x58, visible when occupied)
            RectTransform accentBar = NewRect("ActiveAccentBar", inner);
            Image accentBarImg = accentBar.gameObject.AddComponent<Image>();
            accentBarImg.color = StatusActive;
            accentBarImg.raycastTarget = false;
            SetLE(accentBar.gameObject, 2f, 58f, 0f, 0f);
            accentBar.gameObject.SetActive(false);

            // Thumbnail box (76x58 default, resized per slot kind)
            RectTransform thumbBorder = NewRect("ThumbBoxBorder", inner);
            Image thumbBorderImg = AddRoundedImage(thumbBorder.gameObject, assets.rounded2Px, BorderSubtle);
            SetLE(thumbBorder.gameObject, 76f, 58f, 0f, 0f);

            RectTransform thumbFill = NewRect("ThumbBoxFill", thumbBorder);
            Stretch(thumbFill, 1f, 1f, 1f, 1f);
            Image thumbImg = AddRoundedImage(thumbFill.gameObject, assets.rounded2Px, BgWell);

            // Equipped Sprite Icon
            RectTransform iconRt = NewRect("Icon", thumbFill);
            Stretch(iconRt, 8f, 8f, 8f, 8f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = false;

            // Empty Placeholder Icon (centered 18x18 Lucide sprite)
            RectTransform placeholderRt = NewRect("EmptyPlaceholderIcon", thumbFill);
            placeholderRt.anchorMin = new Vector2(0.5f, 0.5f);
            placeholderRt.anchorMax = new Vector2(0.5f, 0.5f);
            placeholderRt.pivot = new Vector2(0.5f, 0.5f);
            placeholderRt.sizeDelta = new Vector2(18f, 18f);
            Image placeholderImg = placeholderRt.gameObject.AddComponent<Image>();
            placeholderImg.preserveAspect = true;
            placeholderImg.color = TextDim;
            placeholderImg.raycastTarget = false;

            // Empty Keybind Text (for consumable slots 3/4/5/6)
            TextMeshProUGUI keybindText = NewText("EmptyKeybindText", thumbFill, "3", 12f, TextDim, TextAlignmentOptions.Center, assets.monoRegular);
            Stretch(keybindText.rectTransform);
            keybindText.gameObject.SetActive(false);

            // Info Column
            RectTransform info = NewRect("Info", inner);
            SetLE(info.gameObject, 0f, -1f, 1f, 1f);
            AddLayout(info.gameObject, true, 5f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            // Top Row: Header Label + Status Badge
            RectTransform topRow = NewRect("TopRow", info);
            SetLE(topRow.gameObject, 0f, 20f, 1f, 0f);
            AddLayout(topRow.gameObject, false, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI headerText = NewText("HeaderText", topRow, "SLOT 1 // PRIMARY", 10f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            headerText.characterSpacing = 5f;
            SetLE(headerText.gameObject, 0f, 16f, 1f, 0f);

            // Status Badge Pill
            RectTransform badgeBorder = NewRect("StatusBadge", topRow);
            Image badgeBorderImg = AddRoundedImage(badgeBorder.gameObject, assets.rounded2Px, BorderSubtle);
            SetLE(badgeBorder.gameObject, -1f, 20f, 0f, 0f);

            RectTransform badgeBg = NewRect("Fill", badgeBorder);
            Stretch(badgeBg, 1f, 1f, 1f, 1f);
            Image badgeBgImg = AddRoundedImage(badgeBg.gameObject, assets.rounded2Px, BgWell);
            LayoutElement badgeBgIgnore = badgeBg.gameObject.AddComponent<LayoutElement>();
            badgeBgIgnore.ignoreLayout = true;

            HorizontalLayoutGroup badgeHlg = badgeBorder.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeHlg.padding = new RectOffset(8, 8, 3, 3);
            badgeHlg.spacing = 6f;
            badgeHlg.childAlignment = TextAnchor.MiddleCenter;
            badgeHlg.childControlWidth = true;
            badgeHlg.childControlHeight = true;
            badgeHlg.childForceExpandWidth = false;
            badgeHlg.childForceExpandHeight = false;
            ContentSizeFitter badgeFitter = badgeBorder.gameObject.AddComponent<ContentSizeFitter>();
            badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            badgeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            RectTransform dotRt = NewRect("Dot", badgeBorder);
            Image dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = assets.circleDot;
            dotImg.color = StatusInactive;
            dotImg.raycastTarget = false;
            SetLE(dotRt.gameObject, 5f, 5f, 0f, 0f);

            TextMeshProUGUI badgeText = NewText("Text", badgeBorder, "INACTIVE", 10f, TextMuted, TextAlignmentOptions.Center, assets.monoSemiBold);
            badgeText.characterSpacing = 5f;
            SetLE(badgeText.gameObject, -1f, 14f, 0f, 0f);

            // Item Name
            TextMeshProUGUI nameText = NewText("NameText", info, "EMPTY", 15f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
            SetLE(nameText.gameObject, 0f, 20f, 1f, 0f);

            // Bottom Row: StatsText + Unequip Button
            RectTransform bottomRow = NewRect("BottomRow", info);
            SetLE(bottomRow.gameObject, 0f, 22f, 1f, 0f);
            AddLayout(bottomRow.gameObject, false, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            TextMeshProUGUI statsText = NewText("StatsText", bottomRow, "No weapon equipped", 11f, TextDim, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
            SetLE(statsText.gameObject, 0f, 18f, 1f, 0f);

            Button unequipBtn = CreateMinimalButton(
                "UnequipButton", bottomRow, "UNEQUIP",
                DangerBg, DangerBorder, DangerText,
                10f, assets.monoSemiBold, assets.rounded2Px,
                new RectOffset(10, 10, 3, 3), 22f);
            unequipBtn.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 4f;
            unequipBtn.gameObject.SetActive(false);

            // Wire serialized fields on LoadoutSlotUI
            SerializedObject so = new SerializedObject(slotComp);
            so.FindProperty("borderImage").objectReferenceValue = borderImg;
            so.FindProperty("innerImage").objectReferenceValue = innerImg;
            so.FindProperty("activeAccentBar").objectReferenceValue = accentBar.gameObject;
            so.FindProperty("activeAccentBarImage").objectReferenceValue = accentBarImg;
            so.FindProperty("thumbBoxBorderImage").objectReferenceValue = thumbBorderImg;
            so.FindProperty("thumbBoxImage").objectReferenceValue = thumbImg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("emptyPlaceholderIcon").objectReferenceValue = placeholderImg;
            so.FindProperty("emptyKeybindText").objectReferenceValue = keybindText;
            so.FindProperty("headerText").objectReferenceValue = headerText;
            so.FindProperty("statusBadgeBorder").objectReferenceValue = badgeBorderImg;
            so.FindProperty("statusBadgeBg").objectReferenceValue = badgeBgImg;
            so.FindProperty("statusBadgeDot").objectReferenceValue = dotImg;
            so.FindProperty("statusBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("bottomRow").objectReferenceValue = bottomRow.gameObject;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("showStatsRow").boolValue = true;
            so.FindProperty("unequipButton").objectReferenceValue = unequipBtn;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, LoadoutSlotPrefabPath);
            return saved.GetComponent<LoadoutSlotUI>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // =====================================================================
    // Scene Hierarchy: HUD Canvas (1920x1080) & LoadoutMenu
    // =====================================================================

    private static void BuildSceneHierarchy(InventoryItemSlotUI itemSlotPrefab, LoadoutSlotUI loadoutSlotPrefab, UIAssets assets)
    {
        GameObject canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[LoadoutUISetupBuilder] Could not find 'HUD Canvas' in the active scene.");
            return;
        }

        UpgradeCanvasAndExistingHUD(canvasObj);

        Transform loadoutMenuTrans = canvasObj.transform.Find("LoadoutMenu");
        if (loadoutMenuTrans == null)
        {
            Debug.LogError("[LoadoutUISetupBuilder] Could not find 'HUD Canvas/LoadoutMenu' in the active scene.");
            return;
        }

        GameObject loadoutMenuObj = loadoutMenuTrans.gameObject;

        // Clean existing children under LoadoutMenu
        for (int i = loadoutMenuObj.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(loadoutMenuObj.transform.GetChild(i).gameObject);
        }

        LoadoutUI loadoutUI = loadoutMenuObj.GetComponent<LoadoutUI>() ?? loadoutMenuObj.AddComponent<LoadoutUI>();
        InventoryDragController dragCtrl = loadoutMenuObj.GetComponent<InventoryDragController>() ?? loadoutMenuObj.AddComponent<InventoryDragController>();

        // 1. ModalRoot (full-screen dim overlay, inactive by default)
        RectTransform modalRoot = NewRect("ModalRoot", loadoutMenuObj.transform);
        Stretch(modalRoot);
        Image overlayImg = modalRoot.gameObject.AddComponent<Image>();
        overlayImg.color = BgBackdrop;

        // WindowBorder (1440 x 860 centered on 1920x1080, fits cleanly in 16:9)
        RectTransform windowBorder = NewRect("WindowBorder", modalRoot);
        windowBorder.anchorMin = new Vector2(0.5f, 0.5f);
        windowBorder.anchorMax = new Vector2(0.5f, 0.5f);
        windowBorder.pivot = new Vector2(0.5f, 0.5f);
        windowBorder.sizeDelta = new Vector2(1442f, 862f);
        windowBorder.anchoredPosition = Vector2.zero;
        AddRoundedImage(windowBorder.gameObject, assets.rounded2Px, BorderSubtle);

        RectTransform window = NewRect("Window", windowBorder);
        Stretch(window, 1f, 1f, 1f, 1f);
        AddRoundedImage(window.gameObject, assets.rounded2Px, BgVoid);
        AddLayout(window.gameObject, true, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        // ---------------- Header (64px) ----------------
        RectTransform header = NewRect("Header", window);
        header.gameObject.AddComponent<Image>().color = BgVoid;
        SetLE(header.gameObject, -1f, 64f, 1f, 0f);

        RectTransform headerContent = NewRect("HeaderContent", header);
        Stretch(headerContent, 0f, 1f, 0f, 0f);
        AddLayout(headerContent.gameObject, false, 16f, new RectOffset(32, 32, 0, 0), TextAnchor.MiddleLeft);

        // Header Bottom Rule (1px)
        RectTransform headerRule = NewRect("BottomRule", header);
        headerRule.anchorMin = new Vector2(0f, 0f);
        headerRule.anchorMax = new Vector2(1f, 0f);
        headerRule.pivot = new Vector2(0.5f, 0f);
        headerRule.sizeDelta = new Vector2(0f, 1f);
        headerRule.anchoredPosition = Vector2.zero;
        Image headerRuleImg = headerRule.gameObject.AddComponent<Image>();
        headerRuleImg.color = BorderSubtle;
        headerRuleImg.raycastTarget = false;

        // Left Title Group
        RectTransform titleGroup = NewRect("HeaderTitleGroup", headerContent);
        SetLE(titleGroup.gameObject, 0f, -1f, 1f, 1f);
        AddLayout(titleGroup.gameObject, false, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

        RectTransform pipRt = NewRect("StatusPip", titleGroup);
        Image pipImg = pipRt.gameObject.AddComponent<Image>();
        pipImg.sprite = assets.circleDot;
        pipImg.color = StatusActive;
        pipImg.raycastTarget = false;
        SetLE(pipRt.gameObject, 6f, 6f, 0f, 0f);

        TextMeshProUGUI titleText = NewText("MenuTitle", titleGroup, "LOADOUT & INVENTORY", 15f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
        titleText.characterSpacing = 8f;
        SetLE(titleText.gameObject, -1f, 24f, 0f, 0f);

        RectTransform titleDivider = NewRect("TitleDivider", titleGroup);
        Image titleDividerImg = titleDivider.gameObject.AddComponent<Image>();
        titleDividerImg.color = BorderMedium;
        titleDividerImg.raycastTarget = false;
        SetLE(titleDivider.gameObject, 1f, 14f, 0f, 0f);

        TextMeshProUGUI kitTag = NewText("KitTag", titleGroup, "OP-01 // ACTIVE KIT", 11f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        kitTag.characterSpacing = 6f;
        SetLE(kitTag.gameObject, -1f, 18f, 0f, 0f);

        // Close Button (34x34)
        Button closeBtn = CreateIconButton(
            "CloseButton", headerContent,
            BgCard, BorderMedium, TextSecondary,
            assets.iconClose, assets.rounded2Px,
            34f, 34f, 15f);

        // ---------------- Main Content ----------------
        RectTransform mainContent = NewRect("MainContent", window);
        SetLE(mainContent.gameObject, -1f, 0f, 1f, 1f);
        AddLayout(mainContent.gameObject, false, 32f, new RectOffset(32, 32, 28, 28), TextAnchor.UpperLeft);

        // Left Column: Loadout (490px wide)
        RectTransform leftCol = NewRect("LoadoutColumn", mainContent);
        SetLE(leftCol.gameObject, 490f, 0f, 0f, 1f);
        AddLayout(leftCol.gameObject, true, 24f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        // Section 01: Equipped Weapons
        RectTransform weaponsSection = NewRect("EquippedWeaponsSection", leftCol);
        SetLE(weaponsSection.gameObject, 0f, -1f, 1f, 0f);
        AddLayout(weaponsSection.gameObject, true, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionHeaderRow(weaponsSection, "01 // EQUIPPED WEAPONS", assets);

        LoadoutSlotUI primarySlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, weaponsSection, "Slot_Primary",
            LoadoutSlotUI.SlotKind.WeaponPrimary, 0,
            "SLOT 1 // PRIMARY", assets.iconCrosshair, "",
            dragCtrl, 104f, 76f, 58f, true, true, false, assets);

        Button swapBtn = CreateSwapSlotsButton(weaponsSection, assets);

        LoadoutSlotUI secondarySlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, weaponsSection, "Slot_Secondary",
            LoadoutSlotUI.SlotKind.WeaponSecondary, 1,
            "SLOT 2 // SECONDARY", assets.iconCrosshair, "",
            dragCtrl, 104f, 76f, 58f, true, true, false, assets);

        // Section 02: Equipment (2 columns: VEST, MASK)
        RectTransform equipSection = NewRect("EquipmentSection", leftCol);
        SetLE(equipSection.gameObject, 0f, -1f, 1f, 0f);
        AddLayout(equipSection.gameObject, true, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionHeaderRow(equipSection, "02 // EQUIPMENT", assets);

        RectTransform equipRow = NewRect("EquipmentRow", equipSection);
        SetLE(equipRow.gameObject, 0f, 84f, 1f, 0f);
        AddLayout(equipRow.gameObject, false, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        LoadoutSlotUI vestSlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, equipRow, "Slot_Vest",
            LoadoutSlotUI.SlotKind.EquipmentVest, 0,
            "VEST", assets.iconShield, "",
            dragCtrl, 84f, 48f, 48f, false, false, true, assets);

        LoadoutSlotUI maskSlot = InstantiateLoadoutSlot(
            loadoutSlotPrefab, equipRow, "Slot_Mask",
            LoadoutSlotUI.SlotKind.EquipmentMask, 1,
            "MASK", assets.iconBiohazard, "",
            dragCtrl, 84f, 48f, 48f, false, false, true, assets);

        // Section 03: Consumables (2x2 Grid)
        RectTransform consumablesSection = NewRect("ConsumablesSection", leftCol);
        SetLE(consumablesSection.gameObject, 0f, -1f, 1f, 0f);
        AddLayout(consumablesSection.gameObject, true, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionHeaderRow(consumablesSection, "03 // CONSUMABLES", assets);

        RectTransform consumablesGrid = NewRect("ConsumablesGrid", consumablesSection);
        SetLE(consumablesGrid.gameObject, 0f, 176f, 1f, 0f);
        GridLayoutGroup grid = consumablesGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(239f, 82f);
        grid.spacing = new Vector2(12f, 12f);
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
                $"SLOT {i + 1} [{consumableKeys[i]}]", null, consumableKeys[i],
                dragCtrl, 82f, 44f, 44f, false, false, true, assets);
        }

        // Column Divider (1px)
        RectTransform colDivider = NewRect("ColumnDivider", mainContent);
        Image colDividerImg = colDivider.gameObject.AddComponent<Image>();
        colDividerImg.color = BorderSubtle;
        colDividerImg.raycastTarget = false;
        SetLE(colDivider.gameObject, 1f, 0f, 0f, 1f);

        // Right Column: Inventory (fills remaining width)
        RectTransform rightCol = NewRect("InventoryColumn", mainContent);
        SetLE(rightCol.gameObject, 0f, 0f, 1f, 1f);
        Image rightDropBg = rightCol.gameObject.AddComponent<Image>();
        rightDropBg.color = Color.clear;
        rightDropBg.raycastTarget = true; // Ensures dragging equipped items to empty list area still triggers OnDrop
        AddLayout(rightCol.gameObject, true, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

        SectionHeaderRow(rightCol, "04 // INVENTORY", assets);

        // Category Tabs Outer Container (#0B0B0D with #18181D 1px border, 4px padding, 6px gap)
        RectTransform tabBarBorder = NewRect("CategoryTabsBorder", rightCol);
        AddRoundedImage(tabBarBorder.gameObject, assets.rounded2Px, BorderSubtle);
        SetLE(tabBarBorder.gameObject, 0f, 48f, 1f, 0f);

        RectTransform tabBar = NewRect("CategoryTabs", tabBarBorder);
        Stretch(tabBar, 1f, 1f, 1f, 1f);
        AddRoundedImage(tabBar.gameObject, assets.rounded2Px, BgSurface);
        AddLayout(tabBar.gameObject, false, 6f, new RectOffset(4, 4, 4, 4), TextAnchor.MiddleCenter);

        string[] tabNames = new[] { "WEAPONS", "EQUIPMENT", "CONSUMABLES" };
        Button[] tabButtons = new Button[3];
        Image[] tabBorders = new Image[3];
        Image[] tabImages = new Image[3];
        TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[3];
        TextMeshProUGUI[] tabCountTexts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            bool isDefaultActive = i == 0;
            RectTransform tabOuter = NewRect($"Tab_{tabNames[i]}", tabBar);
            SetLE(tabOuter.gameObject, 0f, 38f, 1f, 0f);
            Image tabOuterBorder = AddRoundedImage(tabOuter.gameObject, assets.rounded2Px, isDefaultActive ? BorderMedium : BgSurface);

            RectTransform tabInner = NewRect("Fill", tabOuter);
            Stretch(tabInner, 1f, 1f, 1f, 1f);
            Image tabInnerImg = AddRoundedImage(tabInner.gameObject, assets.rounded2Px, isDefaultActive ? BgCard : BgSurface);
            AddLayout(tabInner.gameObject, false, 8f, new RectOffset(8, 8, 0, 0), TextAnchor.MiddleCenter);

            Button tabBtn = tabOuter.gameObject.AddComponent<Button>();
            tabBtn.targetGraphic = tabInnerImg;
            tabBtn.transition = Selectable.Transition.ColorTint;
            tabBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock cb = tabBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            tabBtn.colors = cb;

            TextMeshProUGUI tabLbl = NewText(
                "Label", tabInner, tabNames[i], 11f,
                isDefaultActive ? TextPrimary : TextMuted,
                TextAlignmentOptions.Center,
                isDefaultActive ? assets.monoSemiBold : assets.monoRegular);
            tabLbl.characterSpacing = 5f;
            SetLE(tabLbl.gameObject, -1f, 18f, 0f, 0f);

            TextMeshProUGUI tabCount = NewText(
                "Count", tabInner, "(0)", 11f,
                isDefaultActive ? StatusActive : TextDim,
                TextAlignmentOptions.Center,
                assets.monoRegular);
            SetLE(tabCount.gameObject, -1f, 18f, 0f, 0f);

            tabButtons[i] = tabBtn;
            tabBorders[i] = tabOuterBorder;
            tabImages[i] = tabInnerImg;
            tabTexts[i] = tabLbl;
            tabCountTexts[i] = tabCount;
        }

        // Scrollable Inventory List
        RectTransform scrollView = NewRect("InventoryScrollView", rightCol);
        Image scrollBg = scrollView.gameObject.AddComponent<Image>();
        scrollBg.color = Color.clear;
        scrollBg.raycastTarget = true;
        SetLE(scrollView.gameObject, 0f, 0f, 1f, 1f);

        RectTransform viewport = NewRect("Viewport", scrollView);
        Stretch(viewport, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        AddLayout(content.gameObject, true, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
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
        Stretch(emptyNotice, 24f, 24f, 24f, 24f);
        TextMeshProUGUI emptyText = NewText("Text", emptyNotice, "Empty", 13f, TextMuted, TextAlignmentOptions.Center, assets.monoRegular);
        emptyText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(emptyText.rectTransform);
        emptyNotice.gameObject.SetActive(false);

        // ---------------- Footer (44px) ----------------
        RectTransform footer = NewRect("Footer", window);
        footer.gameObject.AddComponent<Image>().color = BgVoid;
        SetLE(footer.gameObject, -1f, 44f, 1f, 0f);

        RectTransform footerTopRule = NewRect("TopRule", footer);
        footerTopRule.anchorMin = new Vector2(0f, 1f);
        footerTopRule.anchorMax = new Vector2(1f, 1f);
        footerTopRule.pivot = new Vector2(0.5f, 1f);
        footerTopRule.sizeDelta = new Vector2(0f, 1f);
        footerTopRule.anchoredPosition = Vector2.zero;
        Image footerRuleImg = footerTopRule.gameObject.AddComponent<Image>();
        footerRuleImg.color = BorderSubtle;
        footerRuleImg.raycastTarget = false;

        RectTransform footerContent = NewRect("FooterContent", footer);
        Stretch(footerContent, 0f, 0f, 0f, 1f);
        AddLayout(footerContent.gameObject, false, 16f, new RectOffset(32, 32, 0, 0), TextAnchor.MiddleLeft);

        TextMeshProUGUI hintLeft = NewText("EquipHint", footerContent, "[CLICK] OR [DRAG] TO EQUIP", 11f, TextMuted, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        hintLeft.characterSpacing = 5f;
        SetLE(hintLeft.gameObject, 0f, -1f, 1f, 1f);

        TextMeshProUGUI hintRight = NewText("CloseHint", footerContent, "[E] / [ESC] CLOSE", 11f, TextMuted, TextAlignmentOptions.MidlineRight, assets.monoRegular);
        hintRight.characterSpacing = 5f;
        SetLE(hintRight.gameObject, -1f, -1f, 0f, 1f);

        // 2. Top-level DragLayer & Ghost
        RectTransform dragLayer = NewRect("DragLayer", loadoutMenuObj.transform);
        Stretch(dragLayer);
        CanvasGroup dragLayerCg = dragLayer.gameObject.AddComponent<CanvasGroup>();
        dragLayerCg.blocksRaycasts = false;
        dragLayerCg.interactable = false;

        RectTransform dragGhost = NewRect("DragGhost", dragLayer);
        dragGhost.sizeDelta = new Vector2(228f, 58f);
        dragGhost.pivot = new Vector2(0.5f, 0.5f);
        Image ghostBorder = AddRoundedImage(dragGhost.gameObject, assets.rounded2Px, StatusActive);
        ghostBorder.raycastTarget = false;

        RectTransform ghostInner = NewRect("InnerFill", dragGhost);
        Stretch(ghostInner, 1f, 1f, 1f, 1f);
        Image ghostInnerImg = AddRoundedImage(ghostInner.gameObject, assets.rounded2Px, new Color(BgCard.r, BgCard.g, BgCard.b, 0.96f));
        ghostInnerImg.raycastTarget = false;
        AddLayout(ghostInner.gameObject, false, 12f, new RectOffset(10, 12, 8, 8), TextAnchor.MiddleLeft);

        RectTransform ghostThumbBorder = NewRect("ThumbBorder", ghostInner);
        Image ghostThumbBorderImg = AddRoundedImage(ghostThumbBorder.gameObject, assets.rounded2Px, BorderMedium);
        ghostThumbBorderImg.raycastTarget = false;
        SetLE(ghostThumbBorder.gameObject, 42f, 40f, 0f, 0f);

        RectTransform ghostThumbFill = NewRect("ThumbFill", ghostThumbBorder);
        Stretch(ghostThumbFill, 1f, 1f, 1f, 1f);
        Image ghostThumbImg = AddRoundedImage(ghostThumbFill.gameObject, assets.rounded2Px, BgWell);
        ghostThumbImg.raycastTarget = false;

        RectTransform ghostIconRt = NewRect("Icon", ghostThumbFill);
        Stretch(ghostIconRt, 5f, 5f, 5f, 5f);
        Image ghostIcon = ghostIconRt.gameObject.AddComponent<Image>();
        ghostIcon.preserveAspect = true;
        ghostIcon.raycastTarget = false;

        TextMeshProUGUI ghostLabel = NewText("Label", ghostInner, "Item", 14f, TextPrimary, TextAlignmentOptions.MidlineLeft, assets.uiSemiBold);
        ghostLabel.raycastTarget = false;
        SetLE(ghostLabel.gameObject, 0f, -1f, 1f, 1f);

        dragGhost.gameObject.SetActive(false);
        modalRoot.gameObject.SetActive(false);

        // Wire InventoryDragController
        SerializedObject dragSo = new SerializedObject(dragCtrl);
        dragSo.FindProperty("dragLayer").objectReferenceValue = dragLayer;
        dragSo.FindProperty("dragGhost").objectReferenceValue = dragGhost;
        dragSo.FindProperty("dragGhostBorder").objectReferenceValue = ghostBorder;
        dragSo.FindProperty("dragGhostInner").objectReferenceValue = ghostInnerImg;
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
        SerializedProperty tabBordersProp = uiSo.FindProperty("tabBorders");
        SerializedProperty tabImgsProp = uiSo.FindProperty("tabImages");
        SerializedProperty tabTxtsProp = uiSo.FindProperty("tabTexts");
        SerializedProperty tabCountTxtsProp = uiSo.FindProperty("tabCountTexts");
        tabBtnsProp.arraySize = 3;
        tabBordersProp.arraySize = 3;
        tabImgsProp.arraySize = 3;
        tabTxtsProp.arraySize = 3;
        tabCountTxtsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            tabBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
            tabBordersProp.GetArrayElementAtIndex(i).objectReferenceValue = tabBorders[i];
            tabImgsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabImages[i];
            tabTxtsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabTexts[i];
            tabCountTxtsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabCountTexts[i];
        }

        uiSo.FindProperty("inventoryScrollRect").objectReferenceValue = scrollRect;
        uiSo.FindProperty("inventoryContentRoot").objectReferenceValue = content;
        uiSo.FindProperty("emptyNoticeObj").objectReferenceValue = emptyNotice.gameObject;
        uiSo.FindProperty("emptyNoticeText").objectReferenceValue = emptyText;
        uiSo.FindProperty("inventoryItemSlotPrefab").objectReferenceValue = itemSlotPrefab;
        uiSo.FindProperty("dragController").objectReferenceValue = dragCtrl;
        uiSo.FindProperty("activeTabBorderColor").colorValue = BorderMedium;
        uiSo.FindProperty("inactiveTabBorderColor").colorValue = BgSurface;
        uiSo.FindProperty("activeTabColor").colorValue = BgCard;
        uiSo.FindProperty("inactiveTabColor").colorValue = BgSurface;
        uiSo.FindProperty("activeTabTextColor").colorValue = TextPrimary;
        uiSo.FindProperty("inactiveTabTextColor").colorValue = TextMuted;
        uiSo.FindProperty("activeTabCountColor").colorValue = StatusActive;
        uiSo.FindProperty("inactiveTabCountColor").colorValue = TextDim;
        uiSo.FindProperty("activeTabFont").objectReferenceValue = assets.monoSemiBold;
        uiSo.FindProperty("inactiveTabFont").objectReferenceValue = assets.monoRegular;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(loadoutMenuObj);
        EditorSceneManager.MarkSceneDirty(loadoutMenuObj.scene);
        EditorSceneManager.SaveScene(loadoutMenuObj.scene);
    }

    /// <summary>
    /// Upgrades HUD Canvas to 1920x1080 (16:9) once and scales existing HUD siblings
    /// proportionally so AmmoPanel, HealthPanel, and ConsumableHUD keep their on-screen size.
    /// </summary>
    private static void UpgradeCanvasAndExistingHUD(GameObject canvasObj)
    {
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            bool needsHudRescale = scaler.referenceResolution.x < 1000f;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            EditorUtility.SetDirty(scaler);

            if (needsHudRescale)
            {
                const float hudScaleFactor = 3f;

                Transform ammoTrans = canvasObj.transform.Find("AmmoPanel");
                if (ammoTrans is RectTransform ammoRt)
                {
                    ammoRt.anchoredPosition *= hudScaleFactor;
                    ammoRt.localScale *= hudScaleFactor;
                    EditorUtility.SetDirty(ammoRt);
                }

                Transform healthTrans = canvasObj.transform.Find("HealthPanel");
                if (healthTrans is RectTransform healthRt)
                {
                    healthRt.anchoredPosition *= hudScaleFactor;
                    healthRt.localScale *= hudScaleFactor;
                    EditorUtility.SetDirty(healthRt);
                }

                ConsumableHUD consumableHud = canvasObj.GetComponent<ConsumableHUD>();
                if (consumableHud != null)
                {
                    SerializedObject chSo = new SerializedObject(consumableHud);
                    SerializedProperty posProp = chSo.FindProperty("panelPosition");
                    SerializedProperty sizeProp = chSo.FindProperty("slotSize");
                    SerializedProperty spaceProp = chSo.FindProperty("slotSpacing");
                    if (posProp != null) posProp.vector2Value = new Vector2(0f, 54f);
                    if (sizeProp != null) sizeProp.vector2Value = new Vector2(96f, 96f);
                    if (spaceProp != null) spaceProp.floatValue = 18f;
                    chSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(consumableHud);
                }
            }
        }
    }

    private static LoadoutSlotUI InstantiateLoadoutSlot(
        LoadoutSlotUI prefab,
        Transform parent,
        string name,
        LoadoutSlotUI.SlotKind kind,
        int index,
        string headerLabel,
        Sprite emptyIcon,
        string emptyKeybind,
        InventoryDragController dragCtrl,
        float slotHeight,
        float thumbW,
        float thumbH,
        bool showAccentBar,
        bool showStats,
        bool compactBadgeTextOnly,
        UIAssets assets)
    {
        LoadoutSlotUI slot = (LoadoutSlotUI)PrefabUtility.InstantiatePrefab(prefab, parent);
        slot.name = name;
        SetLE(slot.gameObject, 0f, slotHeight, 1f, 0f);

        Transform inner = slot.transform.Find("InnerFill");
        if (inner != null)
        {
            HorizontalLayoutGroup hlg = inner.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                int pad = compactBadgeTextOnly ? 12 : 14;
                hlg.padding = new RectOffset(pad, pad, pad, pad);
                hlg.spacing = compactBadgeTextOnly ? 12f : 14f;
            }

            Transform accent = inner.Find("ActiveAccentBar");
            if (accent != null && !showAccentBar)
            {
                accent.gameObject.SetActive(false);
            }

            Transform thumb = inner.Find("ThumbBoxBorder");
            if (thumb != null)
            {
                SetLE(thumb.gameObject, thumbW, thumbH, 0f, 0f);
                Transform thumbFill = thumb.Find("ThumbBoxFill");
                if (thumbFill != null)
                {
                    Transform placeholderTrans = thumbFill.Find("EmptyPlaceholderIcon");
                    if (placeholderTrans != null)
                    {
                        Image ph = placeholderTrans.GetComponent<Image>();
                        ph.sprite = emptyIcon;
                        ph.enabled = emptyIcon != null;
                    }

                    Transform keyTrans = thumbFill.Find("EmptyKeybindText");
                    if (keyTrans != null)
                    {
                        TextMeshProUGUI kt = keyTrans.GetComponent<TextMeshProUGUI>();
                        kt.text = emptyKeybind;
                        kt.gameObject.SetActive(!string.IsNullOrEmpty(emptyKeybind));
                    }
                }
            }

            Transform info = inner.Find("Info");
            if (info != null)
            {
                VerticalLayoutGroup infoLayout = info.GetComponent<VerticalLayoutGroup>();
                if (infoLayout != null)
                    infoLayout.spacing = compactBadgeTextOnly ? 3f : 5f;

                Transform topRow = info.Find("TopRow");
                if (topRow != null)
                {
                    Transform hdrTrans = topRow.Find("HeaderText");
                    if (hdrTrans != null)
                    {
                        TextMeshProUGUI hdr = hdrTrans.GetComponent<TextMeshProUGUI>();
                        hdr.text = headerLabel;
                        hdr.font = assets.monoRegular;
                        hdr.fontSize = 10f;
                    }

                    Transform badge = topRow.Find("StatusBadge");
                    if (badge != null && compactBadgeTextOnly)
                    {
                        // Minimal text-only status tag ("INACTIVE" / "ACTIVE") matching 02 // EQUIPMENT & 03 // CONSUMABLES
                        Image bBorder = badge.GetComponent<Image>();
                        if (bBorder != null) bBorder.enabled = false;
                        Transform bFill = badge.Find("Fill");
                        if (bFill != null)
                        {
                            Image bfImg = bFill.GetComponent<Image>();
                            if (bfImg != null) bfImg.enabled = false;
                        }
                        Transform bDot = badge.Find("Dot");
                        if (bDot != null) bDot.gameObject.SetActive(false);

                        HorizontalLayoutGroup bh = badge.GetComponent<HorizontalLayoutGroup>();
                        if (bh != null)
                        {
                            bh.padding = new RectOffset(0, 0, 0, 0);
                            bh.spacing = 0f;
                        }
                        Transform bText = badge.Find("Text");
                        if (bText != null)
                        {
                            TextMeshProUGUI bt = bText.GetComponent<TextMeshProUGUI>();
                            bt.font = assets.monoRegular;
                            bt.fontSize = 9f;
                        }
                    }
                }

                Transform nameTrans = info.Find("NameText");
                Transform bottomRow = info.Find("BottomRow");

                if (nameTrans != null)
                {
                    TextMeshProUGUI nm = nameTrans.GetComponent<TextMeshProUGUI>();
                    nm.fontSize = compactBadgeTextOnly ? 13f : 16f;
                }

                if (bottomRow != null)
                {
                    Transform statsTrans = bottomRow.Find("StatsText");
                    if (statsTrans != null)
                        statsTrans.gameObject.SetActive(showStats);

                    Transform unequipTrans = bottomRow.Find("UnequipButton");
                    if (compactBadgeTextOnly)
                    {
                        SetLE(bottomRow.gameObject, 0f, 18f, 1f, 0f);
                        bottomRow.gameObject.SetActive(false);
                        HorizontalLayoutGroup brHlg = bottomRow.GetComponent<HorizontalLayoutGroup>();
                        if (brHlg != null) brHlg.childAlignment = TextAnchor.MiddleRight;
                        if (unequipTrans != null)
                        {
                            SetLE(unequipTrans.gameObject, -1f, 18f, 0f, 0f);
                            TextMeshProUGUI uTxt = unequipTrans.GetComponentInChildren<TextMeshProUGUI>();
                            if (uTxt != null) uTxt.fontSize = 9f;
                            HorizontalLayoutGroup uHlg = unequipTrans.GetComponent<HorizontalLayoutGroup>();
                            if (uHlg != null) uHlg.padding = new RectOffset(7, 7, 2, 2);
                        }
                    }
                }
            }
        }

        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("slotKind").enumValueIndex = (int)kind;
        so.FindProperty("slotIndex").intValue = index;
        so.FindProperty("dragController").objectReferenceValue = dragCtrl;
        so.FindProperty("showStatsRow").boolValue = showStats;
        if (string.IsNullOrEmpty(emptyKeybind))
            so.FindProperty("emptyKeybindText").objectReferenceValue = null;
        if (!showAccentBar)
        {
            so.FindProperty("activeAccentBar").objectReferenceValue = null;
            so.FindProperty("activeAccentBarImage").objectReferenceValue = null;
        }
        if (compactBadgeTextOnly)
        {
            so.FindProperty("statusBadgeBorder").objectReferenceValue = null;
            so.FindProperty("statusBadgeBg").objectReferenceValue = null;
            so.FindProperty("statusBadgeDot").objectReferenceValue = null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    // =====================================================================
    // UI Primitive Builders
    // =====================================================================

    private static void SectionHeaderRow(Transform parent, string labelText, UIAssets assets)
    {
        RectTransform row = NewRect("SectionHeader_" + labelText, parent);
        SetLE(row.gameObject, 0f, 20f, 1f, 0f);
        AddLayout(row.gameObject, false, 12f, new RectOffset(0, 0, 2, 2), TextAnchor.MiddleLeft);

        TextMeshProUGUI label = NewText("Label", row, labelText, 11f, TextSecondary, TextAlignmentOptions.MidlineLeft, assets.monoRegular);
        label.characterSpacing = 6f;
        SetLE(label.gameObject, -1f, 16f, 0f, 0f);

        RectTransform rule = NewRect("Rule", row);
        Image ruleImg = rule.gameObject.AddComponent<Image>();
        ruleImg.color = BorderSubtle;
        ruleImg.raycastTarget = false;
        SetLE(rule.gameObject, 0f, 1f, 1f, 0f);
    }

    private static Button CreateSwapSlotsButton(Transform parent, UIAssets assets)
    {
        RectTransform outer = NewRect("SwapSlotsButton", parent);
        AddRoundedImage(outer.gameObject, assets.rounded2Px, BorderSubtle);
        SetLE(outer.gameObject, 0f, 34f, 1f, 0f);

        RectTransform fill = NewRect("Fill", outer);
        Stretch(fill, 1f, 1f, 1f, 1f);
        Image fillImg = AddRoundedImage(fill.gameObject, assets.rounded2Px, BgSurface);
        AddLayout(fill.gameObject, false, 8f, new RectOffset(12, 12, 0, 0), TextAnchor.MiddleCenter);

        Button btn = outer.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        btn.colors = cb;

        RectTransform iconRt = NewRect("SwapIcon", fill);
        Image iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = assets.iconSwap;
        iconImg.color = TextMuted;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        SetLE(iconRt.gameObject, 13f, 13f, 0f, 0f);

        TextMeshProUGUI txt = NewText("SwapLabel", fill, "SWAP SLOTS", 11f, TextSecondary, TextAlignmentOptions.Center, assets.monoRegular);
        txt.characterSpacing = 6f;
        SetLE(txt.gameObject, -1f, 18f, 0f, 0f);

        return btn;
    }

    private static Button CreateMinimalButton(
        string name,
        Transform parent,
        string label,
        Color bgColor,
        Color borderColor,
        Color textColor,
        float fontSize,
        TMP_FontAsset font,
        Sprite roundedSprite,
        RectOffset padding,
        float height)
    {
        RectTransform outer = NewRect(name, parent);
        AddRoundedImage(outer.gameObject, roundedSprite, borderColor);
        SetLE(outer.gameObject, -1f, height, 0f, 0f);

        HorizontalLayoutGroup outerHlg = outer.gameObject.AddComponent<HorizontalLayoutGroup>();
        outerHlg.padding = padding;
        outerHlg.childAlignment = TextAnchor.MiddleCenter;
        outerHlg.childControlWidth = true;
        outerHlg.childControlHeight = true;
        outerHlg.childForceExpandWidth = false;
        outerHlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = outer.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform fill = NewRect("Fill", outer);
        Stretch(fill, 1f, 1f, 1f, 1f);
        Image fillImg = AddRoundedImage(fill.gameObject, roundedSprite, bgColor);
        LayoutElement fillIgnore = fill.gameObject.AddComponent<LayoutElement>();
        fillIgnore.ignoreLayout = true;

        Button btn = outer.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = cb;

        TextMeshProUGUI txt = NewText("Label", outer, label, fontSize, textColor, TextAlignmentOptions.Center, font);
        SetLE(txt.gameObject, -1f, height - 4f, 0f, 0f);

        return btn;
    }

    private static Button CreateIconButton(
        string name,
        Transform parent,
        Color bgColor,
        Color borderColor,
        Color iconColor,
        Sprite iconSprite,
        Sprite roundedSprite,
        float width,
        float height,
        float iconSize)
    {
        RectTransform outer = NewRect(name, parent);
        AddRoundedImage(outer.gameObject, roundedSprite, borderColor);
        SetLE(outer.gameObject, width, height, 0f, 0f);

        RectTransform fill = NewRect("Fill", outer);
        Stretch(fill, 1f, 1f, 1f, 1f);
        Image fillImg = AddRoundedImage(fill.gameObject, roundedSprite, bgColor);

        Button btn = outer.gameObject.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;

        RectTransform iconRt = NewRect("Icon", fill);
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        Image iconImg = iconRt.gameObject.AddComponent<Image>();
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
}
