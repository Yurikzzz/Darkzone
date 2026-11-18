using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural pixel-art muzzle flash VFX adapted from Sprite Fusion's muzzleFlash effect
/// for Darkzone's top-down 2D 32-PPU pixel-art renderer.
/// </summary>
public class MuzzleFlashVFX : MonoBehaviour
{
    public struct SpawnOptions
    {
        public Vector2 position;
        public float angleRad;
        public float size;
        public int sparks; // < 0 uses auto: Round(4 + 3 * size)
        public bool smoke;
        public bool casing;
        public int seed;
        public Transform anchor;
        public int baseSortingOrder;
        public string sortingLayerName;
        public Action<Vector2> onKick;

        public static SpawnOptions Default => new SpawnOptions
        {
            size = 1f,
            sparks = -1,
            smoke = true,
            casing = true,
            seed = 0,
            baseSortingOrder = 10,
            sortingLayerName = "Default"
        };
    }

    // Pixel scale matching Darkzone's PixelArtImporter (32 PPU) and TS PX = 2
    private const float PixelsPerUnit = 32f;
    private const float CanvasPxToWorld = 1f / (2f * PixelsPerUnit); // 1 TS canvas px = 0.5 VFX px = 1/64 world unit
    private const float GravityCanvas = 1900f;
    private const float AnchorTrackDuration = 0.08f;
    private const int FlashTextureSize = 96; // Supports up to ~1.5x flash size at 1:1 32-PPU pixel density
    private const int MaxRasterRadius = (FlashTextureSize / 2) - 2;

    // Exact Sprite Fusion palette
    private static readonly Color32 ColWhite = Hex32(0xffffff);
    private static readonly Color32 ColHot = Hex32(0xfff3a6);
    private static readonly Color32 ColYellow = Hex32(0xffe26b);
    private static readonly Color32 ColOrange = Hex32(0xff8a2a);
    private static readonly Color32 ColDeepOrange = Hex32(0xf2561d);
    private static readonly Color32 ColRed = Hex32(0xc9281f);
    private static readonly Color ColWarmGlow = new Color(1f, 176f / 255f, 96f / 255f, 1f);
    private static readonly Color ColSmoke = HexColor(0x5b5566);
    private static readonly Color ColWisp = HexColor(0x7a7186);
    private static readonly Color ColBrass = HexColor(0xe9b54a);
    private static readonly Color ColBrassDark = HexColor(0x9b6a22);
    private static readonly Color[] SmokeRamp =
    {
        HexColor(0x7b6f72),
        HexColor(0x5d5660),
        HexColor(0x433d48),
        HexColor(0x2b2630),
        HexColor(0x1a171e)
    };

    private struct Lobe
    {
        public float off;
        public float a;
        public float len;
        public float wid;
        public float core;
    }

    private struct SparkParticle
    {
        public Vector2 pos;
        public Vector2 vel;
        public float height;
        public float vHeight;
        public float life;
        public float maxLife;
        public SpriteRenderer tipSr;
        public SpriteRenderer tailSr;
        public SpriteRenderer glowSr;
    }

    private struct SmokeParticle
    {
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public float delay;
        public Color baseColor;
        public float size;
        public float seed;
        public SpriteRenderer baseSr;
        public SpriteRenderer highlightSr;
    }

    private struct CasingState
    {
        public bool active;
        public Vector2 pos;
        public Vector2 vel;
        public float height;
        public float vHeight;
        public float angle;
        public float angularVel;
        public float life;
        public float maxLife;
        public bool rest;
        public float glint;
    }

    // Shared static pool & cached procedural sprites/material
    private static readonly Stack<MuzzleFlashVFX> Pool = new Stack<MuzzleFlashVFX>(16);
    private static Transform poolRoot;
    private static Material sharedAdditiveMaterial;
    private static Sprite sharedPixelSprite;
    private static Sprite sharedGlowSprite;
    private static Sprite sharedSmokeBaseSprite;
    private static Sprite sharedSmokeHighlightSprite;
    private static Sprite sharedCasingSprite;
    private static int seedCounter = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Pool.Clear();
        poolRoot = null;
        sharedAdditiveMaterial = null;
        sharedPixelSprite = null;
        sharedGlowSprite = null;
        sharedSmokeBaseSprite = null;
        sharedSmokeHighlightSprite = null;
        sharedCasingSprite = null;
        seedCounter = 1;
    }

    // Instance state
    private Texture2D flashTexture;
    private Sprite flashSprite;
    private Color32[] flashPixels;
    private SpriteRenderer flashRenderer;
    private SpriteRenderer warmGlowRenderer;
    private SpriteRenderer coreGlowRenderer;

    private SpriteRenderer casingRenderer;
    private SpriteRenderer casingGlintRenderer;
    private SpriteRenderer casingGlowRenderer;
    private CasingState casing;

    private readonly List<SparkParticle> sparks = new List<SparkParticle>(16);
    private readonly Stack<SpriteRenderer> sparkRendererPool = new Stack<SpriteRenderer>(48);
    private readonly List<SmokeParticle> smokes = new List<SmokeParticle>(16);
    private readonly Stack<SpriteRenderer> smokeRendererPool = new Stack<SpriteRenderer>(32);

    private SpawnOptions opts;
    private Lobe[] frame1Lobes;
    private Lobe[] frame2Lobes;
    private float elapsed;
    private int lastRenderedFlashFrame;
    private float rasterScaleFactor = 1f;
    private bool isPlaying;

    /// <summary>
    /// Spawns (or reuses from pool) a procedural muzzle flash at the given world position and angle.
    /// </summary>
    public static MuzzleFlashVFX Spawn(SpawnOptions options)
    {
        EnsureSharedAssets();

        MuzzleFlashVFX vfx = null;
        while (Pool.Count > 0 && vfx == null)
        {
            vfx = Pool.Pop();
        }

        if (vfx == null)
        {
            GameObject go = new GameObject("MuzzleFlashVFX");
            vfx = go.AddComponent<MuzzleFlashVFX>();
            vfx.InitializeInstance();
        }

        vfx.gameObject.SetActive(true);
        vfx.Play(options);
        return vfx;
    }

    private void InitializeInstance()
    {
        flashTexture = new Texture2D(FlashTextureSize, FlashTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        flashPixels = new Color32[FlashTextureSize * FlashTextureSize];
        flashSprite = Sprite.Create(
            flashTexture,
            new Rect(0f, 0f, FlashTextureSize, FlashTextureSize),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        flashSprite.hideFlags = HideFlags.DontSave;

        flashRenderer = CreateChildRenderer("Flash", flashSprite, null, 12);
        warmGlowRenderer = CreateChildRenderer("WarmGlow", sharedGlowSprite, sharedAdditiveMaterial, 13);
        coreGlowRenderer = CreateChildRenderer("CoreGlow", sharedGlowSprite, sharedAdditiveMaterial, 13);

        casingRenderer = CreateChildRenderer("Casing", sharedCasingSprite, null, 9);
        casingGlintRenderer = CreateChildRenderer("CasingGlint", sharedPixelSprite, null, 10);
        casingGlowRenderer = CreateChildRenderer("CasingGlow", sharedGlowSprite, sharedAdditiveMaterial, 13);
    }

    private SpriteRenderer CreateChildRenderer(string childName, Sprite sprite, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null)
            sr.sharedMaterial = mat;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
        return sr;
    }

    private void Play(SpawnOptions options)
    {
        opts = options;
        if (opts.size <= 0.01f) opts.size = 1f;
        if (string.IsNullOrEmpty(opts.sortingLayerName)) opts.sortingLayerName = "Default";
        if (opts.seed == 0) opts.seed = unchecked((int)((uint)(++seedCounter) * 747796405u + 2891336453u));

        elapsed = 0f;
        lastRenderedFlashFrame = 0;
        isPlaying = true;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        Rng rng = new Rng((uint)opts.seed);
        float S = opts.size;
        float A = opts.angleRad;
        float c = Mathf.Cos(A);
        float s = Mathf.Sin(A);

        // Compute flash lobes (frames 1 & 2) matching TS lobes()
        float spin = rng.Range(-0.35f, 0.35f);
        float[] jit = new float[9];
        for (int i = 0; i < 9; i++)
            jit[i] = rng.Range(0.8f, 1.2f);

        // Determine if lobes exceed FlashTextureSize; if so, rasterize slightly smaller and scale up
        float maxExtent = 2f;
        Lobe[] raw1 = BuildLobes(S, 1, spin, jit);
        for (int i = 0; i < raw1.Length; i++)
            maxExtent = Mathf.Max(maxExtent, raw1[i].off + raw1[i].len + 2f);

        if (maxExtent > MaxRasterRadius)
        {
            rasterScaleFactor = maxExtent / MaxRasterRadius;
            float rasterS = S / rasterScaleFactor;
            frame1Lobes = BuildLobes(rasterS, 1, spin, jit);
            frame2Lobes = BuildLobes(rasterS, 2, spin, jit);
        }
        else
        {
            rasterScaleFactor = 1f;
            frame1Lobes = raw1;
            frame2Lobes = BuildLobes(S, 2, spin, jit);
        }

        // Spawn sparks
        int sparkCount = opts.sparks >= 0 ? opts.sparks : Mathf.RoundToInt(4f + 3f * S);
        Vector2 sparkOrigin = opts.position + new Vector2(c, s) * (4f * CanvasPxToWorld);
        for (int i = 0; i < sparkCount; i++)
        {
            float sa = A + rng.Range(-1f, 1f) * 0.32f;
            float sp = rng.Range(300f, 760f) * (0.7f + 0.3f * S) * CanvasPxToWorld;
            float life = rng.Range(0.05f, 0.15f);

            sparks.Add(new SparkParticle
            {
                pos = sparkOrigin,
                vel = new Vector2(Mathf.Cos(sa) * sp, Mathf.Sin(sa) * sp),
                height = 0.22f,
                vHeight = rng.Range(-0.5f, 1.0f),
                life = life,
                maxLife = life,
                tipSr = RentSparkRenderer(opts.sortingLayerName, opts.baseSortingOrder + 2, null, sharedPixelSprite),
                tailSr = RentSparkRenderer(opts.sortingLayerName, opts.baseSortingOrder + 2, null, sharedPixelSprite),
                glowSr = RentSparkRenderer(opts.sortingLayerName, opts.baseSortingOrder + 3, sharedAdditiveMaterial, sharedGlowSprite)
            });
        }

        // Spawn smoke puffs & delayed wisps
        if (opts.smoke)
        {
            int puffCount = Mathf.RoundToInt(1f + S * 2.5f);
            Vector2 smokeOrigin = opts.position + new Vector2(c, s) * (3f * CanvasPxToWorld);
            float sqrtS = Mathf.Sqrt(S);

            for (int i = 0; i < puffCount; i++)
            {
                float sa = A + rng.Range(-0.5f, 0.5f);
                float sp = rng.Range(20f, 90f) * S * CanvasPxToWorld;
                float life = rng.Range(0.45f, 0.9f);
                SpawnSmokeParticle(
                    smokeOrigin,
                    new Vector2(Mathf.Cos(sa) * sp, Mathf.Sin(sa) * sp + 10f * CanvasPxToWorld),
                    life,
                    ColSmoke,
                    rng.Range(1.2f, 2.2f) * sqrtS,
                    0f,
                    rng.Next() * 100f);
            }

            Vector2 wispOrigin = opts.position + new Vector2(c, s) * (2f * CanvasPxToWorld);
            for (int i = 0; i < 4; i++)
            {
                float k = i / 3f;
                float speedScale = (40f - 30f * k) * S;
                float vx = (c * speedScale + rng.Range(-6f, 6f)) * CanvasPxToWorld;
                float vy = (s * speedScale + (14f + 26f * k)) * CanvasPxToWorld;
                float life = rng.Range(0.9f, 1.4f);
                SpawnSmokeParticle(
                    wispOrigin,
                    new Vector2(vx, vy),
                    life,
                    ColWisp,
                    rng.Range(0.9f, 1.4f) * sqrtS,
                    0.04f + i * 0.05f,
                    rng.Next() * 100f);
            }
        }

        // Spawn brass casing ejection (adapted for top-down 2D with simulated Z bounce)
        if (opts.casing)
        {
            float up = c >= 0f ? 1f : -1f;
            float nx = -s * up;
            float ny = c * up;

            float sideSpeed = rng.Range(170f, 260f) * CanvasPxToWorld;
            float backSpeed = rng.Range(50f, 130f) * CanvasPxToWorld;
            float life = rng.Range(3.6f, 4.6f);

            casing = new CasingState
            {
                active = true,
                pos = opts.position - new Vector2(c, s) * (14f * S * CanvasPxToWorld),
                vel = new Vector2(nx * sideSpeed - c * backSpeed, ny * sideSpeed - s * backSpeed),
                height = 0.24f,
                vHeight = rng.Range(1.3f, 2.2f),
                angle = rng.Range(0f, Mathf.PI * 2f),
                angularVel = rng.Range(22f, 34f) * rng.Sign(),
                life = life,
                maxLife = life,
                rest = false,
                glint = 0f
            };
        }
        else
        {
            casing.active = false;
            casingRenderer.enabled = false;
            casingGlintRenderer.enabled = false;
            casingGlowRenderer.enabled = false;
        }

        // Apply recoil kick callback matching TS: if (S >= 1) o.kick?.(-c * 1.6 * S, -s * 1.6 * S)
        if (S >= 1f)
        {
            opts.onKick?.Invoke(new Vector2(-c, -s) * (1.6f * S * CanvasPxToWorld));
        }
        else
        {
            // Subtle proportional kick for lighter weapons too
            opts.onKick?.Invoke(new Vector2(-c, -s) * (1.0f * S * CanvasPxToWorld));
        }

        // Render frame 1 immediately on the spawn frame so semi-auto shots never miss frame 1
        ApplySortingLayers();
        RenderVisuals();
    }

    private void SpawnSmokeParticle(Vector2 pos, Vector2 vel, float life, Color col, float size, float delay, float seed)
    {
        smokes.Add(new SmokeParticle
        {
            pos = pos,
            vel = vel,
            life = life,
            maxLife = life,
            delay = delay,
            baseColor = col,
            size = size,
            seed = seed,
            baseSr = RentSmokeRenderer(opts.sortingLayerName, opts.baseSortingOrder, sharedSmokeBaseSprite),
            highlightSr = RentSmokeRenderer(opts.sortingLayerName, opts.baseSortingOrder, sharedSmokeHighlightSprite)
        });
    }

    private void ApplySortingLayers()
    {
        flashRenderer.sortingLayerName = opts.sortingLayerName;
        flashRenderer.sortingOrder = opts.baseSortingOrder + 2;

        warmGlowRenderer.sortingLayerName = opts.sortingLayerName;
        warmGlowRenderer.sortingOrder = opts.baseSortingOrder + 3;

        coreGlowRenderer.sortingLayerName = opts.sortingLayerName;
        coreGlowRenderer.sortingOrder = opts.baseSortingOrder + 3;

        casingRenderer.sortingLayerName = opts.sortingLayerName;
        casingRenderer.sortingOrder = opts.baseSortingOrder - 1;

        casingGlintRenderer.sortingLayerName = opts.sortingLayerName;
        casingGlintRenderer.sortingOrder = opts.baseSortingOrder;

        casingGlowRenderer.sortingLayerName = opts.sortingLayerName;
        casingGlowRenderer.sortingOrder = opts.baseSortingOrder + 3;
    }

    private void Update()
    {
        if (!isPlaying) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        elapsed += dt;
        UpdateCasing(dt);
        UpdateSparks(dt);
        UpdateSmokes(dt);
        RenderVisuals();

        if (elapsed > 0.2f && sparks.Count == 0 && smokes.Count == 0 && !casing.active)
        {
            ReturnToPool();
        }
    }

    private void UpdateCasing(float dt)
    {
        if (!casing.active) return;

        casing.life -= dt;
        if (casing.life <= 0f)
        {
            casing.active = false;
            casingRenderer.enabled = false;
            casingGlintRenderer.enabled = false;
            casingGlowRenderer.enabled = false;
            return;
        }

        if (!casing.rest)
        {
            casing.vHeight -= GravityCanvas * 0.9f * CanvasPxToWorld * dt;
            casing.vel.x -= casing.vel.x * 0.4f * dt;
            casing.vel.y -= casing.vel.y * 0.4f * dt;

            casing.pos += casing.vel * dt;
            casing.angle += casing.angularVel * dt;

            float nextHeight = casing.height + casing.vHeight * dt;
            if (casing.vHeight < 0f && nextHeight <= 0f)
            {
                casing.height = 0f;
                casing.glint = 0.06f;
                float impactSpeed = -casing.vHeight;
                if (impactSpeed > 160f * CanvasPxToWorld)
                {
                    casing.vHeight = impactSpeed * 0.42f;
                    casing.vel *= 0.55f;
                    casing.angularVel *= 0.6f;
                }
                else
                {
                    casing.rest = true;
                    casing.vHeight = 0f;
                    casingRenderer.sortingOrder = opts.baseSortingOrder - 5;
                }
            }
            else
            {
                casing.height = nextHeight;
            }
        }
        else
        {
            casing.pos += casing.vel * dt;
            float damp = Mathf.Min(1f, 6f * dt);
            casing.vel -= casing.vel * damp;
        }

        casing.glint = Mathf.Max(0f, casing.glint - dt);
    }

    private void UpdateSparks(float dt)
    {
        float drag = 2.2f * dt;
        float grav = GravityCanvas * 0.45f * CanvasPxToWorld * dt;

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            SparkParticle p = sparks[i];
            p.life -= dt;
            if (p.life <= 0f)
            {
                ReturnSparkRenderer(p.tipSr);
                ReturnSparkRenderer(p.tailSr);
                ReturnSparkRenderer(p.glowSr);
                sparks.RemoveAt(i);
                continue;
            }

            p.vHeight -= grav;
            p.vel.x -= p.vel.x * drag;
            p.vel.y -= p.vel.y * drag;
            p.pos += p.vel * dt;

            float nextHeight = p.height + p.vHeight * dt;
            if (p.vHeight < 0f && nextHeight <= 0f)
            {
                p.height = 0f;
                p.vHeight = -p.vHeight * 0.45f;
                p.vel *= 0.7f;
            }
            else
            {
                p.height = nextHeight;
            }

            sparks[i] = p;
        }
    }

    private void UpdateSmokes(float dt)
    {
        float rise = 32f * CanvasPxToWorld * dt;
        float swayAmp = 16f * CanvasPxToWorld * dt;

        for (int i = smokes.Count - 1; i >= 0; i--)
        {
            SmokeParticle p = smokes[i];
            if (p.delay > 0f)
            {
                p.delay -= dt;
                smokes[i] = p;
                continue;
            }

            p.life -= dt;
            if (p.life <= 0f)
            {
                ReturnSmokeRenderer(p.baseSr);
                ReturnSmokeRenderer(p.highlightSr);
                smokes.RemoveAt(i);
                continue;
            }

            p.vel.y += rise;
            p.vel.x += Mathf.Sin(elapsed * 1.3f + p.seed) * swayAmp;
            p.vel.x -= p.vel.x * 1.6f * dt;
            p.vel.y -= p.vel.y * 1.2f * dt;
            p.pos += p.vel * dt;

            smokes[i] = p;
        }
    }

    private void RenderVisuals()
    {
        Vector2 muzzlePos = GetAnchoredPosition();
        float angle = opts.angleRad;
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        float S = opts.size;

        // 1. Two-frame procedural flash (60 Hz)
        int flashFrame = Mathf.Max(1, Mathf.CeilToInt(elapsed * 60f - 1e-6f));
        if (flashFrame <= 2)
        {
            if (flashFrame != lastRenderedFlashFrame)
            {
                Lobe[] lobes = flashFrame == 1 ? frame1Lobes : frame2Lobes;
                RasterizeFlash(angle, flashFrame, lobes, elapsed * 1000f + opts.position.x * PixelsPerUnit);
                lastRenderedFlashFrame = flashFrame;
            }

            flashRenderer.enabled = true;
            flashRenderer.transform.position = new Vector3(muzzlePos.x, muzzlePos.y, 0f);
            flashRenderer.transform.localScale = new Vector3(rasterScaleFactor, rasterScaleFactor, 1f);
        }
        else
        {
            flashRenderer.enabled = false;
        }

        // 2. Additive light pop
        float pop = Mathf.Exp(-elapsed / 0.03f);
        if (pop > 0.02f)
        {
            float warmRadiusPx = 18f * S + 8f;
            float warmScale = (warmRadiusPx * 2f) / PixelsPerUnit;
            warmGlowRenderer.enabled = true;
            warmGlowRenderer.transform.position = new Vector3(
                muzzlePos.x + c * (4f * S / PixelsPerUnit),
                muzzlePos.y + s * (4f * S / PixelsPerUnit),
                0f);
            warmGlowRenderer.transform.localScale = new Vector3(warmScale, warmScale, 1f);
            Color wc = ColWarmGlow;
            wc.a = 0.85f * pop;
            warmGlowRenderer.color = wc;

            float coreRadiusPx = 6f * S + 3f;
            float coreScale = (coreRadiusPx * 2f) / PixelsPerUnit;
            coreGlowRenderer.enabled = true;
            coreGlowRenderer.transform.position = new Vector3(
                muzzlePos.x + c * (2f / PixelsPerUnit),
                muzzlePos.y + s * (2f / PixelsPerUnit),
                0f);
            coreGlowRenderer.transform.localScale = new Vector3(coreScale, coreScale, 1f);
            coreGlowRenderer.color = new Color(1f, 1f, 1f, 0.6f * pop);
        }
        else
        {
            warmGlowRenderer.enabled = false;
            coreGlowRenderer.enabled = false;
        }

        // 3. Sparks
        for (int i = 0; i < sparks.Count; i++)
        {
            SparkParticle p = sparks[i];
            float k = p.life / p.maxLife;
            Color hotColor = k > 0.6f ? (Color)ColHot : (k > 0.3f ? (Color)ColYellow : (Color)ColOrange);
            Color tailColor = k > 0.5f ? hotColor : (Color)ColDeepOrange;

            float speedWorld = p.vel.magnitude;
            float speedCanvas = speedWorld / CanvasPxToWorld;
            float lenVfxPx = Mathf.Min(9f, 1f + speedCanvas * 0.011f);
            float lenWorld = lenVfxPx / PixelsPerUnit;

            Vector2 dir = speedWorld > 0.0001f ? p.vel / speedWorld : new Vector2(c, s);
            float rotDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 tipPos = new Vector2(p.pos.x, p.pos.y + p.height * 0.25f);
            float thickness = 1f; // 1 VFX pixel (1/32 world unit)

            float tipLen = lenWorld * 0.45f;
            float tailLen = lenWorld * 0.6f;

            p.tipSr.enabled = true;
            p.tipSr.transform.position = tipPos - dir * (tipLen * 0.5f);
            p.tipSr.transform.rotation = Quaternion.Euler(0f, 0f, rotDeg);
            p.tipSr.transform.localScale = new Vector3(tipLen * PixelsPerUnit, thickness, 1f);
            p.tipSr.color = hotColor;

            p.tailSr.enabled = true;
            p.tailSr.transform.position = tipPos - dir * (lenWorld * 0.7f);
            p.tailSr.transform.rotation = Quaternion.Euler(0f, 0f, rotDeg);
            p.tailSr.transform.localScale = new Vector3(tailLen * PixelsPerUnit, thickness, 1f);
            p.tailSr.color = tailColor;

            float glowWorldDiameter = (3f * 2f) / PixelsPerUnit;
            p.glowSr.enabled = true;
            p.glowSr.transform.position = tipPos;
            p.glowSr.transform.localScale = new Vector3(glowWorldDiameter, glowWorldDiameter, 1f);
            hotColor.a = 0.45f * k;
            p.glowSr.color = hotColor;
        }

        // 4. Smoke puffs
        for (int i = 0; i < smokes.Count; i++)
        {
            SmokeParticle p = smokes[i];
            if (p.delay > 0f)
            {
                p.baseSr.enabled = false;
                p.highlightSr.enabled = false;
                continue;
            }

            float k = p.life / p.maxLife;
            float age = 1f - k;
            float radiusVfxPx = p.size * (0.65f + Mathf.Min(1f, age * 2.5f) * 0.45f + age * 0.4f);
            float alpha = Mathf.Min(1f, age * 12f) * Mathf.Min(1f, k * 2.4f) * 0.58f;
            Color smokeColor = Color.Lerp(p.baseColor, SampleSmokeRamp(age), age);

            float diameterWorld = (radiusVfxPx * 2f) / PixelsPerUnit;
            // Base sprite texture is 24x24 at 32 PPU (0.75 units); scale to match diameterWorld
            float scale = diameterWorld / (24f / PixelsPerUnit);

            p.baseSr.enabled = true;
            p.baseSr.transform.position = p.pos;
            p.baseSr.transform.localScale = new Vector3(scale, scale, 1f);
            Color bc = smokeColor;
            bc.a = alpha;
            p.baseSr.color = bc;

            if (age < 0.5f)
            {
                float hlAlpha = alpha * 0.4f * (1f - age * 2f);
                float hlOffset = radiusVfxPx / PixelsPerUnit;
                p.highlightSr.enabled = true;
                p.highlightSr.transform.position = new Vector2(p.pos.x - hlOffset * 0.22f, p.pos.y + hlOffset * 0.3f);
                p.highlightSr.transform.localScale = new Vector3(scale * 0.5f, scale * 0.5f, 1f);
                Color hc = Color.Lerp(smokeColor, Color.white, 0.12f);
                hc.a = hlAlpha;
                p.highlightSr.color = hc;
            }
            else
            {
                p.highlightSr.enabled = false;
            }
        }

        // 5. Brass casing
        if (casing.active)
        {
            float fade = Mathf.Min(1f, casing.life / 0.6f);
            float drawAngle = casing.rest
                ? Mathf.Round(casing.angle / Mathf.PI) * Mathf.PI
                : casing.angle;

            Vector2 drawPos = new Vector2(casing.pos.x, casing.pos.y + casing.height * 0.35f);
            casingRenderer.enabled = true;
            casingRenderer.transform.position = drawPos;
            casingRenderer.transform.rotation = Quaternion.Euler(0f, 0f, drawAngle * Mathf.Rad2Deg);

            // Stepped Bayer-like fade during the final 0.6s
            float steppedFade = fade >= 1f ? 1f : Mathf.Floor(fade * 8f) / 8f;
            casingRenderer.color = new Color(1f, 1f, 1f, steppedFade);

            if (casing.glint > 0f)
            {
                casingGlintRenderer.enabled = true;
                casingGlintRenderer.transform.position = drawPos + new Vector2(0f, 1f / PixelsPerUnit);
                casingGlintRenderer.color = Color.white;

                float gDiam = (4f * 2f) / PixelsPerUnit;
                casingGlowRenderer.enabled = true;
                casingGlowRenderer.transform.position = drawPos;
                casingGlowRenderer.transform.localScale = new Vector3(gDiam, gDiam, 1f);
                Color gc = ColBrass;
                gc.a = 0.5f * (casing.glint / 0.06f);
                casingGlowRenderer.color = gc;
            }
            else
            {
                casingGlintRenderer.enabled = false;
                casingGlowRenderer.enabled = false;
            }
        }
    }

    private Vector2 GetAnchoredPosition()
    {
        if (opts.anchor != null && elapsed < AnchorTrackDuration)
            return opts.anchor.position;
        return opts.position;
    }

    private static Lobe[] BuildLobes(float S, int f, float spin, float[] jit)
    {
        float k = f == 1 ? 1f : 0.6f;
        float w = f == 1 ? 1f : 0.7f;

        if (f == 1)
        {
            return new[]
            {
                new Lobe { off = 0.5f, a = 0f, len = 9f * S * k, wid = 2.4f * S * w, core = 0.55f },
                new Lobe { off = 2f, a = 0f, len = 21f * S * k * jit[0], wid = 1.15f * S * w, core = 0.4f },
                new Lobe { off = 2f, a = -(0.72f - spin * 0.4f), len = 8.5f * S * k * jit[1], wid = 1.35f * S * w, core = 0.3f },
                new Lobe { off = 1f, a = -(2.2f + spin * 0.4f), len = 4f * S * jit[3], wid = 1.1f * S, core = 0f },
                new Lobe { off = 2f, a = 0.72f + spin * 0.4f, len = 8.5f * S * k * jit[3], wid = 1.35f * S * w, core = 0.3f },
                new Lobe { off = 1f, a = 2.2f - spin * 0.4f, len = 4f * S * jit[5], wid = 1.1f * S, core = 0f }
            };
        }

        return new[]
        {
            new Lobe { off = 0.5f, a = 0f, len = 9f * S * k, wid = 2.4f * S * w, core = 0.55f },
            new Lobe { off = 2f, a = 0f, len = 21f * S * k * jit[0], wid = 1.15f * S * w, core = 0.4f },
            new Lobe { off = 2f, a = -(0.72f - spin * 0.4f), len = 8.5f * S * k * jit[1], wid = 1.35f * S * w, core = 0.3f },
            new Lobe { off = 2f, a = 0.72f + spin * 0.4f, len = 8.5f * S * k * jit[3], wid = 1.35f * S * w, core = 0.3f }
        };
    }

    private static float Profile(in Lobe l, float u)
    {
        float k = u / l.len;
        return l.wid * Mathf.Pow(Mathf.Max(0f, 1f - k), 0.8f) * Mathf.Min(1f, 0.45f + k * 3f);
    }

    private void RasterizeFlash(float angle, int f, Lobe[] lobes, float seed)
    {
        Array.Clear(flashPixels, 0, flashPixels.Length);

        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        float ext = 2f;
        for (int i = 0; i < lobes.Length; i++)
            ext = Mathf.Max(ext, lobes[i].off + lobes[i].len + 2f);

        int half = FlashTextureSize / 2;
        int rInt = Mathf.Min(half - 1, Mathf.CeilToInt(ext));
        float noiseScale = 0.5f * Mathf.Min(1f, ext / 14f);

        Color32 core = f == 1 ? ColWhite : ColHot;
        Color32 body = f == 1 ? ColYellow : ColOrange;
        Color32 inner = f == 1 ? ColHot : ColYellow;
        Color32 rim = f == 1 ? ColDeepOrange : ColRed;

        for (int py = -rInt; py <= rInt; py++)
        {
            int texY = half + py;
            int rowOffset = texY * FlashTextureSize;
            float dy = py + 0.5f;

            for (int px = -rInt; px <= rInt; px++)
            {
                float dx = px + 0.5f;
                float along = dx * c + dy * s;
                float across = -dx * s + dy * c;

                float m = -9f;
                bool hot = false;
                bool warm = false;

                for (int i = 0; i < lobes.Length; i++)
                {
                    ref readonly Lobe l = ref lobes[i];
                    float ca = Mathf.Cos(l.a);
                    float sa = Mathf.Sin(l.a);
                    float lu = (along - l.off) * ca + across * sa;
                    float lv = -(along - l.off) * sa + across * ca;

                    if (lu < -1.5f || lu > l.len + 1f) continue;

                    float r = Profile(in l, Mathf.Max(0f, lu));
                    float mm = r - Mathf.Abs(lv);
                    if (mm > m) m = mm;
                    if (lu < l.len * l.core && Mathf.Abs(lv) <= r * 0.5f) hot = true;
                    if (mm > r * 0.4f) warm = true;
                }

                float dAlong = along - 0.5f;
                float dm = 1.6f - Mathf.Sqrt(dAlong * dAlong + across * across);
                if (dm > m) m = dm;
                if (dm > 0.5f) hot = true;

                m += (Hash(px * 73 + py * 151, seed) - 0.5f) * noiseScale;
                if (m < -0.85f) continue;

                flashPixels[rowOffset + half + px] = m < 0f ? rim : (hot ? core : (warm ? inner : body));
            }
        }

        flashTexture.SetPixels32(flashPixels);
        flashTexture.Apply(false);
    }

    private SpriteRenderer RentSparkRenderer(string layerName, int order, Material mat, Sprite sprite)
    {
        SpriteRenderer sr = null;
        while (sparkRendererPool.Count > 0 && sr == null)
            sr = sparkRendererPool.Pop();

        if (sr == null)
            sr = CreateChildRenderer("SparkPart", sprite, mat, order);

        sr.sprite = sprite;
        sr.sharedMaterial = mat != null ? mat : flashRenderer.sharedMaterial;
        sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
        sr.transform.rotation = Quaternion.identity;
        sr.transform.localScale = Vector3.one;
        sr.enabled = false;
        return sr;
    }

    private void ReturnSparkRenderer(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        sparkRendererPool.Push(sr);
    }

    private SpriteRenderer RentSmokeRenderer(string layerName, int order, Sprite sprite)
    {
        SpriteRenderer sr = null;
        while (smokeRendererPool.Count > 0 && sr == null)
            sr = smokeRendererPool.Pop();

        if (sr == null)
            sr = CreateChildRenderer("SmokePart", sprite, null, order);

        sr.sprite = sprite;
        sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
        sr.transform.rotation = Quaternion.identity;
        sr.transform.localScale = Vector3.one;
        sr.enabled = false;
        return sr;
    }

    private void ReturnSmokeRenderer(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        smokeRendererPool.Push(sr);
    }

    private void ReturnToPool()
    {
        isPlaying = false;
        flashRenderer.enabled = false;
        warmGlowRenderer.enabled = false;
        coreGlowRenderer.enabled = false;
        casingRenderer.enabled = false;
        casingGlintRenderer.enabled = false;
        casingGlowRenderer.enabled = false;

        if (poolRoot == null)
        {
            GameObject root = new GameObject("MuzzleFlashVFX_Pool");
            poolRoot = root.transform;
        }

        transform.SetParent(poolRoot, false);
        gameObject.SetActive(false);
        Pool.Push(this);
    }

    private void OnDestroy()
    {
        if (flashSprite != null) Destroy(flashSprite);
        if (flashTexture != null) Destroy(flashTexture);
    }

    private static void EnsureSharedAssets()
    {
        if (sharedAdditiveMaterial == null)
        {
            sharedAdditiveMaterial = Resources.Load<Material>("MuzzleGlowAdditive");
            if (sharedAdditiveMaterial == null)
            {
                Shader shader = Shader.Find("Darkzone/AdditiveSprite");
                if (shader != null)
                {
                    sharedAdditiveMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                else
                {
                    Shader fallback = Shader.Find("Sprites/Default");
                    sharedAdditiveMaterial = new Material(fallback) { hideFlags = HideFlags.DontSave };
                }
            }
        }

        if (sharedPixelSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false);
            sharedPixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sharedPixelSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedGlowSprite == null)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] cols = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float dy = (y + 0.5f - r) / r;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - r) / r;
                    float d = dx * dx + dy * dy;
                    if (d >= 1f)
                    {
                        cols[y * size + x] = Color.clear;
                        continue;
                    }
                    float f = 1f - Mathf.Sqrt(d);
                    cols[y * size + x] = new Color(1f, 1f, 1f, f * f);
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            sharedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sharedGlowSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedSmokeBaseSprite == null)
        {
            const int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] cols = new Color[size * size];
            float center = size * 0.5f;
            float r1 = size * 0.45f;
            float r2 = r1 * 0.74f;
            float ox2 = r1 * 0.05f;
            float oy2 = -r1 * 0.06f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx1 = x + 0.5f - center;
                    float dy1 = y + 0.5f - center;
                    bool inOuter = dx1 * dx1 + dy1 * dy1 <= r1 * r1;

                    float dx2 = dx1 - ox2;
                    float dy2 = dy1 - oy2;
                    bool inInner = dx2 * dx2 + dy2 * dy2 <= r2 * r2;

                    float a = 0f;
                    if (inOuter) a = 0.45f;
                    if (inInner) a = a + 0.62f * (1f - a);
                    cols[y * size + x] = a > 0f ? new Color(1f, 1f, 1f, a) : Color.clear;
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            sharedSmokeBaseSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sharedSmokeBaseSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedSmokeHighlightSprite == null)
        {
            const int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] cols = new Color[size * size];
            float center = size * 0.5f;
            float r = size * 0.45f;
            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    cols[y * size + x] = (dx * dx + dy * dy <= r * r) ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            sharedSmokeHighlightSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sharedSmokeHighlightSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedCasingSprite == null)
        {
            Texture2D tex = new Texture2D(2, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixel(0, 0, ColBrass);
            tex.SetPixel(1, 0, ColBrassDark);
            tex.Apply(false);
            sharedCasingSprite = Sprite.Create(tex, new Rect(0, 0, 2, 1), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sharedCasingSprite.hideFlags = HideFlags.DontSave;
        }
    }

    private static Color SampleSmokeRamp(float t)
    {
        float u = Mathf.Clamp(t, 0f, 0.9999f) * (SmokeRamp.Length - 1);
        int i = Mathf.FloorToInt(u);
        return Color.Lerp(SmokeRamp[i], SmokeRamp[i + 1], u - i);
    }

    private static float Hash(int n, float k = 0f)
    {
        uint v = unchecked((uint)(n + 1 + Mathf.FloorToInt(k) * 7919) * 2654435761u);
        return (v % 100000u) / 100000f;
    }

    private static Color32 Hex32(int hex)
    {
        return new Color32(
            (byte)((hex >> 16) & 0xff),
            (byte)((hex >> 8) & 0xff),
            (byte)(hex & 0xff),
            255);
    }

    private static Color HexColor(int hex)
    {
        return new Color(
            ((hex >> 16) & 0xff) / 255f,
            ((hex >> 8) & 0xff) / 255f,
            (hex & 0xff) / 255f,
            1f);
    }

    private sealed class Rng
    {
        private uint s;

        public Rng(uint seed = 1)
        {
            s = seed == 0 ? 1u : seed;
        }

        public float Next()
        {
            unchecked
            {
                s += 0x6d2b79f5u;
                uint t = s;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }

        public float Range(float a, float b) => a + (b - a) * Next();
        public float Sign() => Next() < 0.5f ? -1f : 1f;
    }
}
