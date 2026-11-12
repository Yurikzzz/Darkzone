using System.Collections;
using UnityEngine;

/// <summary>
/// Thrown projectile for the grenade consumable.
/// Flies in the throw direction, slows down with drag, and explodes after fuse duration.
/// Deals area damage, applies knockback to enemies, and displays an animated blast effect.
/// </summary>
public class ThrownGrenade : MonoBehaviour
{
    private ConsumableData data;
    private Vector2 velocity;
    private float fuseTimer;
    private bool detonated;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer fuseLight;
    private static Sprite circleSprite;

    public void Initialize(ConsumableData grenadeData, Vector2 startPosition, Vector2 targetWorldPos)
    {
        data = grenadeData;
        transform.position = startPosition;

        Vector2 diff = targetWorldPos - startPosition;
        float dist = diff.magnitude;
        Vector2 dir = diff.normalized;

        // Flight speed scales slightly with throw distance (clamped)
        float speed = data != null ? data.throwSpeed : 12f;
        velocity = dir * Mathf.Min(dist * 2.5f + 4f, speed);
        fuseTimer = data != null ? data.fuseTime : 1.5f;

        BuildVisuals();
    }

    private void BuildVisuals()
    {
        // 1. Grenade Body
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (data != null && data.icon != null)
        {
            spriteRenderer.sprite = data.icon;
        }
        else
        {
            spriteRenderer.sprite = GetCircleSprite();
        }
        spriteRenderer.color = data != null ? data.tintColor : new Color(0.3f, 0.45f, 0.25f, 1f); // Olive green
        spriteRenderer.sortingOrder = 25;
        transform.localScale = new Vector3(0.35f, 0.35f, 1f);

        // 2. Blinking Fuse Light
        GameObject fuseObj = new GameObject("FuseLight");
        fuseObj.transform.SetParent(transform, false);
        fuseObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        fuseObj.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        fuseLight = fuseObj.AddComponent<SpriteRenderer>();
        fuseLight.sprite = GetCircleSprite();
        fuseLight.color = Color.red;
        fuseLight.sortingOrder = 26;
    }

    private void Update()
    {
        if (detonated) return;

        // Move with drag
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 3.5f);

        // Spin while flying
        transform.Rotate(0f, 0f, velocity.magnitude * 90f * Time.deltaTime);

        // Fuse countdown and blinking light
        fuseTimer -= Time.deltaTime;
        float totalFuse = data != null ? data.fuseTime : 1.5f;
        float progress = Mathf.Clamp01(1f - (fuseTimer / totalFuse));

        // Blink frequency accelerates as detonation approaches
        float blinkFreq = Mathf.Lerp(4f, 20f, progress);
        if (fuseLight != null)
        {
            float blink = (Mathf.Sin(Time.time * blinkFreq) + 1f) * 0.5f;
            fuseLight.color = Color.Lerp(Color.yellow, Color.red, blink);
            fuseLight.enabled = blink > 0.35f;
        }

        if (fuseTimer <= 0f)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        if (detonated) return;
        detonated = true;

        float radius = data != null ? data.explosionRadius : 3.5f;
        float maxDamage = data != null ? data.grenadeDamage : 80f;
        float knockbackForce = data != null ? data.explosionKnockback : 6f;
        Color blastColor = data != null ? data.explosionColor : new Color(1f, 0.55f, 0.1f, 0.9f);

        // 1. Detect targets in blast radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            // Don't damage player
            if (hit.CompareTag("Player")) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                // Linear falloff: 100% damage at center down to 50% damage at edge
                float falloff = Mathf.Clamp01(1f - (dist / radius) * 0.5f);
                damageable.TakeDamage(maxDamage * falloff);
            }

            // Apply knockback to enemies
            EnemyMovement em = hit.GetComponent<EnemyMovement>() ?? hit.GetComponentInParent<EnemyMovement>();
            if (em != null)
            {
                Vector2 knockDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                if (knockDir == Vector2.zero) knockDir = Random.insideUnitCircle.normalized;
                em.ApplyKnockback(knockDir, knockbackForce, 0.3f);
            }
            else
            {
                Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 knockDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                    rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }

        // 2. Spawn explosion visual effect
        SpawnExplosionVisual(transform.position, radius, blastColor);

        Destroy(gameObject);
    }

    private void SpawnExplosionVisual(Vector2 position, float radius, Color color)
    {
        GameObject fx = new GameObject("GrenadeExplosionFX");
        fx.transform.position = position;

        // Outer expanding shockwave
        GameObject ringObj = new GameObject("BlastRing");
        ringObj.transform.SetParent(fx.transform, false);
        SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetCircleSprite();
        ringSr.color = color;
        ringSr.sortingOrder = 35;

        // Inner flash
        GameObject flashObj = new GameObject("CoreFlash");
        flashObj.transform.SetParent(fx.transform, false);
        SpriteRenderer flashSr = flashObj.AddComponent<SpriteRenderer>();
        flashSr.sprite = GetCircleSprite();
        flashSr.color = Color.white;
        flashSr.sortingOrder = 36;

        fx.AddComponent<ExplosionEffectRunner>().StartAnimation(radius, color, ringSr, flashSr);
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01((r - d) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return circleSprite;
    }

    private class ExplosionEffectRunner : MonoBehaviour
    {
        public void StartAnimation(float targetRadius, Color color, SpriteRenderer ring, SpriteRenderer flash)
        {
            StartCoroutine(Animate(targetRadius, color, ring, flash));
        }

        private IEnumerator Animate(float targetRadius, Color color, SpriteRenderer ring, SpriteRenderer flash)
        {
            float duration = 0.35f;
            float elapsed = 0f;
            float targetDiameter = targetRadius * 2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                // Expanding ring
                float currentScale = Mathf.Lerp(0.2f, targetDiameter, easeOut);
                if (ring != null)
                {
                    ring.transform.localScale = new Vector3(currentScale, currentScale, 1f);
                    Color c = color;
                    c.a = Mathf.Lerp(color.a, 0f, t * t);
                    ring.color = c;
                }

                // Rapid flash core
                if (flash != null)
                {
                    float coreScale = Mathf.Lerp(0.4f, targetDiameter * 0.5f, easeOut);
                    flash.transform.localScale = new Vector3(coreScale, coreScale, 1f);
                    Color fc = Color.white;
                    fc.a = Mathf.Lerp(1f, 0f, t * 2.5f);
                    flash.color = fc;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
