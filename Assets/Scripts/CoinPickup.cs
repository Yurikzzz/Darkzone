using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural pixel-art coin drop and magnetic pickup VFX adapted from Sprite Fusion's pickup ("coin") effect.
/// Drops from defeated enemies, idles on the ground with a spinning/bobbing animation,
/// magnetically arcs toward the player when within pickup radius, and bursts into homing sparks on collection.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class CoinPickup : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const float VfxPxToWorld = 1f / PixelsPerUnit;
    private const float CanvasPxToWorld = 1f / (2f * PixelsPerUnit);
    private const float LiftDuration = 0.1f;
    private const float FlyDuration = 0.34f;
    private const int CoinSpriteSize = 9;
    private const int RingFrameCount = 8;

    private static readonly Color ColWhite = HexColor(0xfffbe8);   // WH
    private static readonly Color ColHighlight = HexColor(0xffec92); // HI
    private static readonly Color ColLight = HexColor(0xffd243);     // LI
    private static readonly Color ColMid = HexColor(0xf4a923);       // MI
    private static readonly Color ColDark = HexColor(0xa8601a);      // DK
    private static readonly Color ColOutline = HexColor(0x3a1d0c);   // o
    private static readonly Color ColShadow = HexColor(0x0b0814);    // ground shadow

    private static readonly Color[] BurstColors = { ColWhite, ColHighlight, ColLight };
    private static readonly Color[] MoteColors = { ColHighlight, ColLight };

    private static readonly string[] CoinRows =
    {
        "..ooooo..",
        ".ohllllo.",
        "ohlmmmmdo",
        "olmhlmmdo",
        "olmlmmmdo",
        "olmmmmmdo",
        "olddmmddo",
        ".odddddo.",
        "..ooooo.."
    };

    private static readonly float[] BayerMatrix =
    {
        0f, 8f, 2f, 10f,
        12f, 4f, 14f, 6f,
        3f, 11f, 1f, 9f,
        15f, 7f, 13f, 5f
    };

    private struct SparkParticle
    {
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public int armSize;
        public Color color;
        public Vector2[] trail;
        public int trailCount;
        public SpriteRenderer starSr;
        public SpriteRenderer glowSr;
        public SpriteRenderer[] trailSrs;
    }

    private struct MoteParticle
    {
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public int armSize;
        public Color color;
        public SpriteRenderer starSr;
        public SpriteRenderer glowSr;
    }

    private struct RingEffect
    {
        public Vector2 pos;
        public float spawnTime;
        public float maxRadiusCanvasPx;
        public SpriteRenderer ringSr;
        public SpriteRenderer glowSr;
    }

    // Shared cached assets
    private static Material sharedAdditiveMaterial;
    private static Sprite sharedCoinSprite;
    private static Sprite sharedCoinMaskSprite;
    private static Sprite sharedPixelSprite;
    private static Sprite sharedGlowSprite;
    private static Sprite sharedDiscSprite;
    private static Sprite sharedShadowSprite;
    private static Sprite[] sharedStarSprites;
    private static Sprite[] sharedRingSprites;
    private static int seedCounter = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharedAdditiveMaterial = null;
        sharedCoinSprite = null;
        sharedCoinMaskSprite = null;
        sharedPixelSprite = null;
        sharedGlowSprite = null;
        sharedDiscSprite = null;
        sharedShadowSprite = null;
        sharedStarSprites = null;
        sharedRingSprites = null;
        seedCounter = 1;
    }

    [Header("Pickup Settings")]
    [Tooltip("Coin currency granted when collected.")]
    [Min(1)]
    [SerializeField] private int coinValue = 1;

    [Tooltip("Distance at which the coin magnetically flies toward the player.")]
    [Min(0.2f)]
    [SerializeField] private float pickupRadius = 1.75f;

    [Tooltip("Delay after spawning before proximity collection can activate.")]
    [Min(0f)]
    [SerializeField] private float pickupEnableDelay = 0.2f;

    [Tooltip("Base sorting order for coin visuals.")]
    [SerializeField] private int baseSortingOrder = 15;

    private CircleCollider2D triggerCollider;
    private Transform visualsRoot;
    private SpriteRenderer coinSr;
    private SpriteRenderer shadowSr;
    private SpriteRenderer glintSr;
    private SpriteRenderer glintGlowSr;
    private SpriteRenderer warmGlowSr;
    private SpriteRenderer coreGlowSr;
    private SpriteRenderer flashDiscSr;
    private SpriteRenderer popGlowSr;
    private readonly SpriteRenderer[] orbitDotSrs = new SpriteRenderer[2];
    private readonly SpriteRenderer[] orbitGlowSrs = new SpriteRenderer[2];
    private readonly SpriteRenderer[] flyTrailSrs = new SpriteRenderer[6];

    private readonly List<Vector2> flyTrail = new List<Vector2>(7);
    private readonly List<SparkParticle> sparks = new List<SparkParticle>(16);
    private readonly List<MoteParticle> motes = new List<MoteParticle>(16);
    private readonly List<RingEffect> rings = new List<RingEffect>(6);

    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private Rng rng;

    private Vector2 basePosition;
    private Vector2 popVelocity;
    private float popTimer;
    private Vector2 currentCoinPos;
    private Vector2 collectFromPos;

    private float elapsed;
    private float collectTime = float.PositiveInfinity;
    private float popTime = float.PositiveInfinity;
    private int absorbedCount;
    private bool initialized;

    /// <summary>
    /// Returns the shared 9x9 pixel-art coin sprite (useful for UI counters).
    /// </summary>
    public static Sprite GetCoinSprite()
    {
        EnsureSharedAssets();
        return sharedCoinSprite;
    }

    /// <summary>
    /// Spawns a coin pickup in the world with an optional initial scatter velocity.
    /// </summary>
    public static CoinPickup Spawn(Vector2 position, int value = 1, Vector2 initialVelocity = default)
    {
        EnsureSharedAssets();

        GameObject go = new GameObject("CoinPickup");
        go.transform.position = position;

        CoinPickup pickup = go.AddComponent<CoinPickup>();
        pickup.Initialize(value, initialVelocity);
        return pickup;
    }

    public void Initialize(int value, Vector2 initialVelocity = default)
    {
        EnsureSharedAssets();
        coinValue = Mathf.Max(1, value);
        basePosition = transform.position;
        currentCoinPos = basePosition;
        popVelocity = initialVelocity;
        popTimer = initialVelocity.sqrMagnitude > 0.0001f ? 0.32f : 0f;

        if (!initialized)
        {
            SetupVisuals();
            initialized = true;
        }
    }

    private void Awake()
    {
        EnsureSharedAssets();

        rng = new Rng(unchecked((uint)(++seedCounter * 747796405 + 2891336453u)));
        basePosition = transform.position;
        currentCoinPos = basePosition;

        triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.radius = pickupRadius;
        }

        if (!initialized)
        {
            SetupVisuals();
            initialized = true;
        }

        CachePlayerReference();
    }

    private void CachePlayerReference()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    private void SetupVisuals()
    {
        visualsRoot = new GameObject("Visuals").transform;
        visualsRoot.SetParent(transform, false);

        shadowSr = CreateRenderer("Shadow", sharedShadowSprite, null, baseSortingOrder - 2);
        coinSr = CreateRenderer("Coin", sharedCoinSprite, null, baseSortingOrder);
        glintSr = CreateRenderer("Glint", sharedStarSprites[0], null, baseSortingOrder + 2);
        glintGlowSr = CreateRenderer("GlintGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3);
        warmGlowSr = CreateRenderer("WarmGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 2);
        coreGlowSr = CreateRenderer("CoreGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3);
        flashDiscSr = CreateRenderer("PopDisc", sharedDiscSprite, null, baseSortingOrder + 2);
        popGlowSr = CreateRenderer("PopGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3);

        for (int i = 0; i < 2; i++)
        {
            orbitDotSrs[i] = CreateRenderer($"OrbitDot_{i}", sharedPixelSprite, null, baseSortingOrder + 1);
            orbitGlowSrs[i] = CreateRenderer($"OrbitGlow_{i}", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3);
        }

        for (int i = 0; i < flyTrailSrs.Length; i++)
        {
            flyTrailSrs[i] = CreateRenderer($"FlyTrail_{i}", sharedPixelSprite, null, baseSortingOrder - 1);
        }

        HideAllStaticRenderers();
    }

    private SpriteRenderer CreateRenderer(string childName, Sprite sprite, Material mat, int sortingOrder)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(visualsRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null)
            sr.sharedMaterial = mat;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
        return sr;
    }

    private void HideAllStaticRenderers()
    {
        shadowSr.enabled = false;
        coinSr.enabled = false;
        glintSr.enabled = false;
        glintGlowSr.enabled = false;
        warmGlowSr.enabled = false;
        coreGlowSr.enabled = false;
        flashDiscSr.enabled = false;
        popGlowSr.enabled = false;
        for (int i = 0; i < 2; i++)
        {
            orbitDotSrs[i].enabled = false;
            orbitGlowSrs[i].enabled = false;
        }
        for (int i = 0; i < flyTrailSrs.Length; i++)
            flyTrailSrs[i].enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (float.IsFinite(collectTime) || elapsed < pickupEnableDelay) return;
        if (other.CompareTag("Player"))
        {
            TryStartCollect(other.transform);
        }
    }

    private void TryStartCollect(Transform target)
    {
        if (float.IsFinite(collectTime) || target == null) return;
        if (playerHealth != null && !playerHealth.IsAlive) return;

        playerTransform = target;
        collectTime = elapsed;
        collectFromPos = currentCoinPos;
        popVelocity = Vector2.zero;
        popTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        elapsed += dt;

        // Scatter deceleration right after enemy death
        if (popTimer > 0f && !float.IsFinite(collectTime))
        {
            popTimer -= dt;
            basePosition += popVelocity * dt;
            popVelocity = Vector2.Lerp(popVelocity, Vector2.zero, Mathf.Min(1f, dt * 7f));
            transform.position = basePosition;
        }

        // Proximity detection so waiting coins magnetically collect when player walks near
        if (playerTransform == null)
            CachePlayerReference();

        if (!float.IsFinite(collectTime) && elapsed >= pickupEnableDelay && playerTransform != null)
        {
            if (playerHealth == null || playerHealth.IsAlive)
            {
                float dist = Vector2.Distance(basePosition, playerTransform.position);
                if (dist <= pickupRadius)
                {
                    TryStartCollect(playerTransform);
                }
            }
        }

        Vector2 targetPos = playerTransform != null ? (Vector2)playerTransform.position : currentCoinPos;

        // 1. Coin idle bobbing or magnetic Bezier flight
        if (!float.IsFinite(collectTime) || !float.IsFinite(popTime))
        {
            float bob = Mathf.Sin(elapsed * 3.4f) * (3f * CanvasPxToWorld);
            if (!float.IsFinite(collectTime))
            {
                currentCoinPos = new Vector2(basePosition.x, basePosition.y + bob);
            }
            else
            {
                float k = elapsed - collectTime;
                if (k < LiftDuration)
                {
                    float liftProgress = OutCubic(k / LiftDuration);
                    currentCoinPos = new Vector2(collectFromPos.x, collectFromPos.y + (8f * CanvasPxToWorld) * liftProgress);
                }
                else
                {
                    float u = Mathf.Pow(Mathf.Clamp01((k - LiftDuration) / FlyDuration), 2.2f);
                    Vector2 sx = new Vector2(collectFromPos.x, collectFromPos.y + 8f * CanvasPxToWorld);
                    Vector2 mx = new Vector2(
                        (sx.x + targetPos.x) * 0.5f,
                        Mathf.Max(sx.y, targetPos.y) + Mathf.Max(40f * CanvasPxToWorld, Vector2.Distance(sx, targetPos) * 0.35f));

                    float oneMinusU = 1f - u;
                    currentCoinPos = oneMinusU * oneMinusU * sx + 2f * oneMinusU * u * mx + u * u * targetPos;

                    flyTrail.Insert(0, currentCoinPos);
                    if (flyTrail.Count > 7)
                        flyTrail.RemoveAt(flyTrail.Count - 1);

                    if (u >= 1f)
                    {
                        TriggerPop(targetPos);
                    }
                }
            }
        }
        else if (flyTrail.Count > 0)
        {
            flyTrail.RemoveAt(flyTrail.Count - 1);
        }

        // 2. Update sparks (initial burst + homing onto player)
        UpdateSparks(dt, targetPos);

        // 3. Update rising motes
        UpdateMotes(dt);

        // 4. Update rings
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            if (elapsed - rings[i].spawnTime > 0.4f)
            {
                DestroyRenderer(rings[i].ringSr);
                DestroyRenderer(rings[i].glowSr);
                rings.RemoveAt(i);
            }
        }

        // 5. Render everything
        RenderVisuals();

        // 6. Destroy once pop + particles finish
        if (float.IsFinite(popTime) && elapsed - popTime > 0.5f && sparks.Count == 0 && motes.Count == 0 && rings.Count == 0)
        {
            Destroy(gameObject);
        }
    }

    private void TriggerPop(Vector2 targetPos)
    {
        if (float.IsFinite(popTime)) return;
        popTime = elapsed;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.AddCoins(coinValue);

        CameraFollow.Shake(0.035f, 0.08f);

        SpawnRing(currentCoinPos, 20f);

        for (int i = 0; i < 14; i++)
        {
            float angle = (i / 14f) * (Mathf.PI * 2f) + rng.Range(-0.2f, 0.2f);
            float speed = rng.Range(150f, 300f) * CanvasPxToWorld;
            float life = rng.Range(0.75f, 1.05f);
            int arm = rng.Chance(0.4f) ? 2 : 1;
            Color col = rng.Pick(BurstColors);

            SpriteRenderer[] trSrs = new SpriteRenderer[4];
            for (int tIdx = 0; tIdx < trSrs.Length; tIdx++)
                trSrs[tIdx] = CreateRenderer("SparkTrail", sharedPixelSprite, null, baseSortingOrder + 1);

            sparks.Add(new SparkParticle
            {
                pos = currentCoinPos,
                vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 40f * CanvasPxToWorld),
                life = life,
                maxLife = 1.05f,
                armSize = arm,
                color = col,
                trail = new Vector2[5],
                trailCount = 0,
                starSr = CreateRenderer("SparkStar", sharedStarSprites[arm], null, baseSortingOrder + 2),
                glowSr = CreateRenderer("SparkGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3),
                trailSrs = trSrs
            });
        }
    }

    private void SpawnRing(Vector2 pos, float maxRadiusCanvasPx)
    {
        rings.Add(new RingEffect
        {
            pos = pos,
            spawnTime = elapsed,
            maxRadiusCanvasPx = maxRadiusCanvasPx,
            ringSr = CreateRenderer("Ring", sharedRingSprites[0], null, baseSortingOrder + 2),
            glowSr = CreateRenderer("RingGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3)
        });
    }

    private void UpdateSparks(float dt, Vector2 targetPos)
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            SparkParticle s = sparks[i];
            float age = s.maxLife - s.life;

            // Shift trail buffer
            int copyCount = Mathf.Min(s.trailCount, s.trail.Length - 1);
            for (int tIdx = copyCount; tIdx > 0; tIdx--)
                s.trail[tIdx] = s.trail[tIdx - 1];
            s.trail[0] = s.pos;
            s.trailCount = Mathf.Min(s.trail.Length, s.trailCount + 1);

            s.life -= dt;
            if (s.life <= 0f)
            {
                DestroySpark(s);
                sparks.RemoveAt(i);
                continue;
            }

            if (playerTransform != null && age > 0.12f)
            {
                Vector2 toTarget = targetPos - s.pos;
                float dist = toTarget.magnitude;
                float d = dist > 0.0001f ? dist : 1f;
                float wantSpeed = (380f + 900f * age) * CanvasPxToWorld;
                float steer = Mathf.Min(1f, dt * (3f + 22f * age));

                s.vel.x += ((toTarget.x / d) * wantSpeed - s.vel.x) * steer;
                s.vel.y += ((toTarget.y / d) * wantSpeed - s.vel.y) * steer;

                if (dist < 9f * CanvasPxToWorld)
                {
                    DestroySpark(s);
                    sparks.RemoveAt(i);

                    if (absorbedCount++ % 3 == 0)
                        SpawnRing(targetPos, 12f);

                    for (int j = 0; j < 2; j++)
                    {
                        float mLife = rng.Range(0.4f, 0.8f);
                        int mArm = rng.Chance(0.3f) ? 1 : 0;
                        Color mCol = rng.Pick(MoteColors);
                        motes.Add(new MoteParticle
                        {
                            pos = targetPos + new Vector2(rng.Range(-10f, 10f), rng.Range(-10f, 14f)) * CanvasPxToWorld,
                            vel = new Vector2(rng.Range(-12f, 12f), rng.Range(30f, 80f)) * CanvasPxToWorld,
                            life = mLife,
                            maxLife = 0.8f,
                            armSize = mArm,
                            color = mCol,
                            starSr = CreateRenderer("MoteStar", sharedStarSprites[mArm], null, baseSortingOrder + 2),
                            glowSr = CreateRenderer("MoteGlow", sharedGlowSprite, sharedAdditiveMaterial, baseSortingOrder + 3)
                        });
                    }
                    continue;
                }
            }
            else
            {
                s.vel -= s.vel * (3f * dt);
                s.vel.y -= 60f * CanvasPxToWorld * dt;
            }

            s.pos += s.vel * dt;
            sparks[i] = s;
        }
    }

    private void UpdateMotes(float dt)
    {
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            MoteParticle m = motes[i];
            m.life -= dt;
            if (m.life <= 0f)
            {
                DestroyRenderer(m.starSr);
                DestroyRenderer(m.glowSr);
                motes.RemoveAt(i);
                continue;
            }

            m.vel.x -= m.vel.x * (1.5f * dt);
            m.pos += m.vel * dt;
            motes[i] = m;
        }
    }

    private void RenderVisuals()
    {
        bool alive = !float.IsFinite(popTime);

        // Shadow (only while waiting on ground)
        if (alive && !float.IsFinite(collectTime))
        {
            float groundY = basePosition.y - 9f * VfxPxToWorld;
            float h = Mathf.Clamp01((currentCoinPos.y - groundY) / (80f * CanvasPxToWorld));
            float shadowScaleX = (1f - 0.35f * h);
            float shadowAlpha = 0.8f - 0.4f * h;

            shadowSr.enabled = true;
            shadowSr.transform.position = new Vector3(basePosition.x, groundY, 0f);
            shadowSr.transform.localScale = new Vector3(shadowScaleX, 1f, 1f);
            shadowSr.color = new Color(ColShadow.r, ColShadow.g, ColShadow.b, shadowAlpha);
        }
        else
        {
            shadowSr.enabled = false;
        }

        // Orbiting dots (front and back sorted around coin)
        for (int i = 0; i < 2; i++)
        {
            if (!alive)
            {
                orbitDotSrs[i].enabled = false;
                orbitGlowSrs[i].enabled = false;
                continue;
            }

            float a = elapsed * 2.6f + i * Mathf.PI;
            float sinA = Mathf.Sin(a);
            bool front = sinA > 0f;
            Vector2 dotPos = currentCoinPos + new Vector2(Mathf.Cos(a) * 9f, sinA * 2.6f + 1f) * VfxPxToWorld;

            orbitDotSrs[i].enabled = true;
            orbitDotSrs[i].sortingOrder = front ? baseSortingOrder + 1 : baseSortingOrder - 1;
            orbitDotSrs[i].transform.position = dotPos;
            orbitDotSrs[i].color = front ? ColHighlight : ColMid;

            float glowDiam = (4f * 2f) * VfxPxToWorld;
            orbitGlowSrs[i].enabled = true;
            orbitGlowSrs[i].transform.position = dotPos;
            orbitGlowSrs[i].transform.localScale = new Vector3(glowDiam, glowDiam, 1f);
            orbitGlowSrs[i].color = new Color(ColLight.r, ColLight.g, ColLight.b, 0.35f);
        }

        // Flight trail lines
        for (int i = 0; i < flyTrailSrs.Length; i++)
        {
            int idx = i + 1;
            if (idx < flyTrail.Count)
            {
                Vector2 p0 = flyTrail[idx - 1];
                Vector2 p1 = flyTrail[idx];
                Vector2 delta = p1 - p0;
                float len = delta.magnitude;

                flyTrailSrs[i].enabled = len > 0.0001f;
                if (flyTrailSrs[i].enabled)
                {
                    flyTrailSrs[i].transform.position = (p0 + p1) * 0.5f;
                    flyTrailSrs[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    flyTrailSrs[i].transform.localScale = new Vector3(len * PixelsPerUnit, 1f, 1f);
                    flyTrailSrs[i].color = idx < 3 ? ColLight : ColMid;
                }
            }
            else
            {
                flyTrailSrs[i].enabled = false;
            }
        }

        // Coin body, glint, and ambient glows
        if (alive)
        {
            flashDiscSr.enabled = false;
            popGlowSr.enabled = false;

            float flying = float.IsFinite(collectTime)
                ? Mathf.Clamp01((elapsed - collectTime - LiftDuration) / FlyDuration)
                : 0f;

            float spin = Mathf.Cos(elapsed * 5.2f);
            float sx = Mathf.Max(0.15f, Mathf.Abs(spin)) * (1f - 0.25f * flying);
            float sy = 1f - 0.25f * flying;
            float lift = (float.IsFinite(collectTime) && elapsed - collectTime < LiftDuration)
                ? OutCubic((elapsed - collectTime) / LiftDuration)
                : 0f;

            bool edgeTint = Mathf.Abs(spin) < 0.3f;
            coinSr.enabled = true;
            coinSr.sprite = edgeTint ? sharedCoinMaskSprite : sharedCoinSprite;
            coinSr.color = edgeTint ? ColDark : Color.white;
            coinSr.transform.position = currentCoinPos;
            coinSr.transform.localScale = new Vector3(sx * (1f - 0.15f * lift), sy * (1f + 0.2f * lift), 1f);

            float gl = ((elapsed + 0.4f) % 1.3f) / 1.3f;
            if (gl < 0.22f)
            {
                int arm = Mathf.Clamp(Mathf.RoundToInt(2.5f * Mathf.Sin((gl / 0.22f) * Mathf.PI)), 0, 3);
                Vector2 glintPos = currentCoinPos + new Vector2(-CoinSpriteSize * 0.22f, CoinSpriteSize * 0.2f) * VfxPxToWorld;
                glintSr.enabled = true;
                glintSr.sprite = sharedStarSprites[arm];
                glintSr.color = ColWhite;
                glintSr.transform.position = glintPos;

                float gDiam = ((3f + arm * 2.5f) * 2f) * VfxPxToWorld;
                glintGlowSr.enabled = true;
                glintGlowSr.transform.position = glintPos;
                glintGlowSr.transform.localScale = new Vector3(gDiam, gDiam, 1f);
                glintGlowSr.color = new Color(ColWhite.r, ColWhite.g, ColWhite.b, 0.4f);
            }
            else
            {
                glintSr.enabled = false;
                glintGlowSr.enabled = false;
            }

            float warmDiam = ((14f + 2f * Mathf.Sin(elapsed * 4f)) * 2f) * VfxPxToWorld;
            warmGlowSr.enabled = true;
            warmGlowSr.transform.position = currentCoinPos;
            warmGlowSr.transform.localScale = new Vector3(warmDiam, warmDiam, 1f);
            warmGlowSr.color = new Color(ColMid.r, ColMid.g, ColMid.b, 0.45f + 0.4f * flying);

            float coreDiam = (5f * 2f) * VfxPxToWorld;
            coreGlowSr.enabled = true;
            coreGlowSr.transform.position = currentCoinPos;
            coreGlowSr.transform.localScale = new Vector3(coreDiam, coreDiam, 1f);
            coreGlowSr.color = new Color(ColWhite.r, ColWhite.g, ColWhite.b, 0.25f);
        }
        else
        {
            glintSr.enabled = false;
            glintGlowSr.enabled = false;
            warmGlowSr.enabled = false;
            coreGlowSr.enabled = false;

            float k = elapsed - popTime;

            if (k < 0.1f)
            {
                bool firstHalf = k < 0.05f;
                coinSr.enabled = true;
                coinSr.sprite = firstHalf ? sharedCoinSprite : sharedCoinMaskSprite;
                coinSr.color = firstHalf ? Color.white : ColWhite;
                coinSr.transform.position = currentCoinPos;
                coinSr.transform.localScale = firstHalf ? new Vector3(1.5f, 0.6f, 1f) : new Vector3(0.7f, 1.45f, 1f);
            }
            else
            {
                coinSr.enabled = false;
            }

            if (k < 0.08f)
            {
                float rVfx = 7f * (1f - k / 0.08f) + 1f;
                float diam = (rVfx * 2f) * VfxPxToWorld;
                flashDiscSr.enabled = true;
                flashDiscSr.transform.position = currentCoinPos;
                flashDiscSr.transform.localScale = new Vector3(diam, diam, 1f);
                flashDiscSr.color = ColWhite;
            }
            else
            {
                flashDiscSr.enabled = false;
            }

            if (k < 0.3f)
            {
                float popDiam = (30f * 2f) * VfxPxToWorld;
                popGlowSr.enabled = true;
                popGlowSr.transform.position = currentCoinPos;
                popGlowSr.transform.localScale = new Vector3(popDiam, popDiam, 1f);
                popGlowSr.color = new Color(ColLight.r, ColLight.g, ColLight.b, 0.9f * (1f - k / 0.3f));
            }
            else
            {
                popGlowSr.enabled = false;
            }
        }

        // Expanding rings
        for (int i = 0; i < rings.Count; i++)
        {
            RingEffect r = rings[i];
            float k = Mathf.Clamp01((elapsed - r.spawnTime) / 0.4f);
            float e = OutCubic(k);

            int frameIdx = Mathf.Clamp(Mathf.FloorToInt(k * (RingFrameCount - 1)), 0, RingFrameCount - 1);
            float radiusVfx = 2f + (r.maxRadiusCanvasPx * 0.5f) * e * 2f;
            float ringDiam = (radiusVfx * 2f) * VfxPxToWorld;

            r.ringSr.enabled = true;
            r.ringSr.sprite = sharedRingSprites[frameIdx];
            r.ringSr.transform.position = r.pos;
            r.ringSr.transform.localScale = new Vector3(ringDiam, ringDiam, 1f);
            Color rc = k < 0.3f ? ColWhite : ColLight;
            rc.a = Mathf.Clamp01(1.2f - k);
            r.ringSr.color = rc;

            float glowDiam = (16f * 2f) * VfxPxToWorld;
            r.glowSr.enabled = true;
            r.glowSr.transform.position = r.pos;
            r.glowSr.transform.localScale = new Vector3(glowDiam, glowDiam, 1f);
            r.glowSr.color = new Color(ColLight.r, ColLight.g, ColLight.b, 0.5f * (1f - k));
        }

        // Homing burst sparks
        for (int i = 0; i < sparks.Count; i++)
        {
            SparkParticle s = sparks[i];
            float k = Mathf.Clamp01(s.life / s.maxLife);
            int arm = Mathf.Clamp(Mathf.RoundToInt(s.armSize * Mathf.Min(1f, k * 3f)), 0, 3);

            s.starSr.enabled = true;
            s.starSr.sprite = sharedStarSprites[arm];
            s.starSr.color = s.color;
            s.starSr.transform.position = s.pos;

            float gDiam = ((3f + arm * 2.5f) * 2f) * VfxPxToWorld;
            s.glowSr.enabled = true;
            s.glowSr.transform.position = s.pos;
            s.glowSr.transform.localScale = new Vector3(gDiam, gDiam, 1f);
            s.glowSr.color = new Color(s.color.r, s.color.g, s.color.b, 0.4f);

            for (int tIdx = 0; tIdx < s.trailSrs.Length; tIdx++)
            {
                int trailPosIdx = tIdx + 1;
                SpriteRenderer trSr = s.trailSrs[tIdx];
                if (trailPosIdx < s.trailCount)
                {
                    trSr.enabled = true;
                    trSr.transform.position = s.trail[trailPosIdx];
                    Color tc = trailPosIdx < 2 ? ColLight : ColMid;
                    tc.a = 1f - trailPosIdx / 5f;
                    trSr.color = tc;
                }
                else
                {
                    trSr.enabled = false;
                }
            }
        }

        // Rising twinkling motes
        for (int i = 0; i < motes.Count; i++)
        {
            MoteParticle m = motes[i];
            float k = Mathf.Clamp01(m.life / m.maxLife);
            bool visible = k * (0.6f + 0.4f * Mathf.Sin(m.life * 30f)) > 0.25f;

            m.starSr.enabled = visible;
            m.glowSr.enabled = visible;
            if (!visible) continue;

            m.starSr.transform.position = m.pos;
            m.starSr.color = m.color;

            float gDiam = ((3f + m.armSize * 2.5f) * 2f) * VfxPxToWorld;
            m.glowSr.transform.position = m.pos;
            m.glowSr.transform.localScale = new Vector3(gDiam, gDiam, 1f);
            m.glowSr.color = new Color(m.color.r, m.color.g, m.color.b, 0.4f);
        }
    }

    private void DestroySpark(in SparkParticle s)
    {
        DestroyRenderer(s.starSr);
        DestroyRenderer(s.glowSr);
        if (s.trailSrs != null)
        {
            for (int i = 0; i < s.trailSrs.Length; i++)
                DestroyRenderer(s.trailSrs[i]);
        }
    }

    private static void DestroyRenderer(SpriteRenderer sr)
    {
        if (sr != null)
            Destroy(sr.gameObject);
    }

    private static void EnsureSharedAssets()
    {
        if (sharedAdditiveMaterial == null)
        {
            sharedAdditiveMaterial = Resources.Load<Material>("MuzzleGlowAdditive");
            if (sharedAdditiveMaterial == null)
            {
                Shader shader = Shader.Find("Darkzone/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                sharedAdditiveMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
        }

        if (sharedCoinSprite == null)
        {
            sharedCoinSprite = CreateCoinSprite(maskOnly: false);
        }

        if (sharedCoinMaskSprite == null)
        {
            sharedCoinMaskSprite = CreateCoinSprite(maskOnly: true);
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

        if (sharedDiscSprite == null)
        {
            const int size = 16;
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
                float dy = y + 0.5f - r;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    cols[y * size + x] = (dx * dx + dy * dy <= r * r) ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            sharedDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sharedDiscSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedShadowSprite == null)
        {
            const int w = 12;
            const int h = 3;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] cols = new Color[w * h];
            float rx = w * 0.5f;
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - rx;
                float norm = Mathf.Abs(dx / rx);
                float a = Mathf.Clamp01(1f - norm * norm);
                cols[1 * w + x] = new Color(1f, 1f, 1f, a);
                if (Mathf.Abs(dx) < rx * 0.6f)
                    cols[0 * w + x] = new Color(1f, 1f, 1f, a * 0.6f);
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            sharedShadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sharedShadowSprite.hideFlags = HideFlags.DontSave;
        }

        if (sharedStarSprites == null)
        {
            sharedStarSprites = new Sprite[4];
            for (int arm = 0; arm < 4; arm++)
                sharedStarSprites[arm] = CreateStarSprite(arm);
        }

        if (sharedRingSprites == null)
        {
            sharedRingSprites = new Sprite[RingFrameCount];
            for (int i = 0; i < RingFrameCount; i++)
            {
                float k = i / (float)(RingFrameCount - 1);
                sharedRingSprites[i] = CreateRingSprite(k);
            }
        }
    }

    private static Sprite CreateCoinSprite(bool maskOnly)
    {
        Texture2D tex = new Texture2D(CoinSpriteSize, CoinSpriteSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] cols = new Color[CoinSpriteSize * CoinSpriteSize];
        for (int row = 0; row < CoinSpriteSize; row++)
        {
            string line = CoinRows[row];
            int texY = CoinSpriteSize - 1 - row; // Row 0 is top of sprite
            for (int x = 0; x < CoinSpriteSize; x++)
            {
                char ch = line[x];
                if (ch == '.') continue;

                if (maskOnly)
                {
                    cols[texY * CoinSpriteSize + x] = Color.white;
                }
                else
                {
                    Color c = ch switch
                    {
                        'w' => ColWhite,
                        'h' => ColHighlight,
                        'l' => ColLight,
                        'm' => ColMid,
                        'd' => ColDark,
                        'o' => ColOutline,
                        _ => Color.clear
                    };
                    cols[texY * CoinSpriteSize + x] = c;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply(false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, CoinSpriteSize, CoinSpriteSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        sp.hideFlags = HideFlags.DontSave;
        return sp;
    }

    private static Sprite CreateStarSprite(int arm)
    {
        int dim = arm * 2 + 1;
        Texture2D tex = new Texture2D(dim, dim, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] cols = new Color[dim * dim];
        int c = arm;
        cols[c * dim + c] = Color.white;

        for (int d = 1; d <= arm; d++)
        {
            float shade = (d == arm && arm > 1) ? 0.8f : 1f;
            Color armCol = new Color(shade, shade, shade, 1f);
            cols[c * dim + (c + d)] = armCol;
            cols[c * dim + (c - d)] = armCol;
            cols[(c + d) * dim + c] = armCol;
            cols[(c - d) * dim + c] = armCol;
        }

        tex.SetPixels(cols);
        tex.Apply(false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, dim, dim), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        sp.hideFlags = HideFlags.DontSave;
        return sp;
    }

    private static Sprite CreateRingSprite(float k)
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] cols = new Color[size * size];
        float center = size * 0.5f;
        float r = size * 0.42f;
        float thick = Mathf.Max(1.2f, 3.2f * (1f - k));
        float ro = r + thick * 0.5f;
        float ri = Mathf.Max(0f, r - thick * 0.5f);
        float ro2 = ro * ro;
        float ri2 = ri * ri;
        float alpha = 1.2f - k;

        for (int y = 0; y < size; y++)
        {
            float dy = y + 0.5f - center;
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float d2 = dx * dx + dy * dy;
                if (d2 <= ro2 && d2 >= ri2 && (alpha >= 1f || alpha > Bayer(x, y)))
                    cols[y * size + x] = Color.white;
            }
        }

        tex.SetPixels(cols);
        tex.Apply(false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sp.hideFlags = HideFlags.DontSave;
        return sp;
    }

    private static float Bayer(int x, int y)
    {
        return (BayerMatrix[((y & 3) << 2) | (x & 3)] + 0.5f) / 16f;
    }

    private static float OutCubic(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u * u;
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
        public bool Chance(float p) => Next() < p;
        public T Pick<T>(T[] arr) => arr[Mathf.Clamp((int)(Next() * arr.Length), 0, arr.Length - 1)];
    }
}
