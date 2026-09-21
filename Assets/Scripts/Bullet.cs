using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float fadeTime = 0.25f;
    [SerializeField] private Color bulletColor = Color.yellow;
    [SerializeField] private float lineWidth = 0.05f;

    private LineRenderer line;
    private float fadeTimer;
    private bool initialized;

    public void Init(Vector2 start, Vector2 end)
    {
        fadeTimer = fadeTime;
        initialized = true;

        line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = bulletColor;
        line.endColor = bulletColor;
        line.useWorldSpace = true;
        line.sortingOrder = 5;
        line.SetPosition(0, (Vector3)start);
        line.SetPosition(1, (Vector3)end);
    }

    private void Update()
    {
        if (!initialized || line == null) return;

        fadeTimer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(fadeTimer / fadeTime);
        Color c = new Color(bulletColor.r, bulletColor.g, bulletColor.b, alpha);
        line.startColor = c;
        line.endColor = c;

        if (fadeTimer <= 0f)
            Destroy(gameObject);
    }
}