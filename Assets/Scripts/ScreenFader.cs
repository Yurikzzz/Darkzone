using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles screen fade transitions. Automatically finds a UI element named "blackscreen"
/// (even if inactive) and manages fading its alpha over time.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    private CanvasGroup blackscreenGroup;
    private GameObject blackscreenObj;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        FindBlackscreen();
    }

    private void FindBlackscreen()
    {
        // Search through all RectTransforms (finds inactive UI elements too)
        var allRects = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (var rect in allRects)
        {
            if (rect.gameObject.name.Equals("blackscreen", System.StringComparison.OrdinalIgnoreCase))
            {
                // Ensure it's not a prefab from the project folder (hideFlags check)
                if (rect.gameObject.scene.rootCount > 0 || rect.gameObject.scene.isLoaded)
                {
                    blackscreenObj = rect.gameObject;
                    blackscreenGroup = blackscreenObj.GetComponent<CanvasGroup>();
                    
                    if (blackscreenGroup == null)
                    {
                        blackscreenGroup = blackscreenObj.AddComponent<CanvasGroup>();
                    }
                    
                    // Initialize state
                    blackscreenGroup.alpha = 0f;
                    blackscreenObj.SetActive(false);
                    return;
                }
            }
        }
        
        if (blackscreenObj == null)
        {
            Debug.LogWarning("ScreenFader: Could not find a UI element named 'blackscreen'. Fade effects will be ignored.");
        }
    }

    /// <summary>
    /// Executes a fade sequence, invoking the onMidpoint callback when the screen is fully black.
    /// </summary>
    public IEnumerator DoFadeSequence(float fadeDuration, float holdDuration, System.Action onMidpoint)
    {
        if (blackscreenObj == null)
            FindBlackscreen(); // Try again just in case it was created later

        if (blackscreenObj == null)
        {
            // Fallback if no blackscreen exists
            onMidpoint?.Invoke();
            yield break;
        }

        // Enable and start fade in
        blackscreenObj.SetActive(true);
        blackscreenGroup.alpha = 0f;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            blackscreenGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }
        blackscreenGroup.alpha = 1f;

        // Perform the teleport / logic
        onMidpoint?.Invoke();

        // Hold the black screen
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            blackscreenGroup.alpha = 1f - Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }
        blackscreenGroup.alpha = 0f;
        blackscreenObj.SetActive(false);
    }
}
