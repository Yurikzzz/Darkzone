using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float fadeTime = 0.25f;
    [SerializeField] private Color bulletColor = Color.yellow;
    [SerializeField] private float trailWidth = 0.5f;

    private SpriteRenderer spriteRenderer;
    private float fadeTimer;
    private bool initialized;

    // Init is the function called when you fire the gun to set the start and end points
    public void Init(Vector2 start, Vector2 end)
    {
        fadeTimer = fadeTime;
        initialized = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Calculate the direction and the total distance between start and end
        Vector2 direction = end - start;
        float distance = direction.magnitude;

        // 1. POSITION: Place the sprite exactly at the 'start' point (the firepoint)
        transform.position = start;

        // 2. ROTATION: Calculate the angle to the target. 
        // Because you drew the sprite vertically (pointing Up), we subtract 90 degrees 
        // so that it aligns correctly with the target direction.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // We also need the base width to ensure the X-axis scales relative to the original image size
        float baseWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        if (baseWidth <= 0.001f) baseWidth = 1f;

        // 3. SCALE: Use 9-slicing (Sliced Draw Mode) to stretch the middle but keep the ends intact.
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        
        // Instead of scaling the transform, we change the SpriteRenderer's Size.
        // X = original width * your trailWidth setting
        // Y = the exact distance to the target
        spriteRenderer.size = new Vector2(baseWidth * trailWidth, distance);
        transform.localScale = Vector3.one;

        // 4. COLOR: Apply the starting color
        spriteRenderer.color = bulletColor;
        spriteRenderer.sortingOrder = 5;
    }

    private void Update()
    {
        if (!initialized || spriteRenderer == null) return;

        // Decrease the timer
        fadeTimer -= Time.deltaTime;
        
        // Calculate the alpha (opacity) from 1 to 0
        float alpha = Mathf.Clamp01(fadeTimer / fadeTime);
        
        // Apply the fading alpha to the SpriteRenderer
        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;

        // Destroy when fully faded
        if (fadeTimer <= 0f)
            Destroy(gameObject);
    }
}