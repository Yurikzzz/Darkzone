using UnityEngine;

public class HandsAiming : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform handsTransform;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Orbit Settings")]
    [Tooltip("Distance from the player center that the hands pivot orbits at (in local space).")]
    [SerializeField] private float orbitRadius = 0.21f;

    [Tooltip("Vertical offset of the orbit center relative to the player (in local space).")]
    [SerializeField] private float orbitVerticalOffset = 0f;

    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;

        if (handsTransform == null)
            handsTransform = transform.Find("Hands");

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (handsTransform == null || mainCam == null) return;

        // Get cursor world position
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // Direction from player center to cursor (world space)
        Vector2 playerPos = transform.position;
        Vector2 toMouse = (Vector2)mouseWorld - playerPos;

        // Angle from player center to cursor
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;

        // Determine if we're aiming left (cursor is to the left of the player)
        bool aimingLeft = mouseWorld.x < transform.position.x;

        // ---- Flip the player sprite ----
        playerSpriteRenderer.flipX = aimingLeft;

        // ---- Position the hands pivot on an orbit around the player (local space) ----
        // This keeps the orbit radius independent of the parent's scale.
        Vector2 dir = toMouse.normalized;
        float localX = dir.x * orbitRadius + orbitVerticalOffset * 0f; // horizontal orbit
        float localY = dir.y * orbitRadius + orbitVerticalOffset;
        handsTransform.localPosition = new Vector3(localX, localY, 0f);

        // ---- Rotate the hands group to aim at the cursor ----
        handsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        
        if (aimingLeft)
        {
            // When aiming left, flip the hands vertically by inverting the Y-scale.
            handsTransform.localScale = new Vector3(1f, -1f, 1f);
        }
        else
        {
            handsTransform.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}

