using UnityEngine;

/// <summary>
/// Placed on the LoadoutStation object in the scene.
/// Allows the player to interact with the station to open the Loadout & Inventory UI.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LoadoutStation : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Key the player presses to open the loadout station.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Prompt displayed above the station when player is in range.")]
    [SerializeField] private string promptText = "[E] Open Loadout";

    private bool playerInRange;
    private InteractionPromptUI promptUI;

    private void Awake()
    {
        // Ensure collider is set to trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        promptUI = GetComponent<InteractionPromptUI>();
        if (promptUI == null)
        {
            promptUI = gameObject.AddComponent<InteractionPromptUI>();
        }
        promptUI.PromptText = promptText;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            // Automatically close the loadout menu if player walks away
            if (LoadoutUI.Instance != null && LoadoutUI.Instance.IsOpen)
            {
                LoadoutUI.Instance.Close();
            }
        }
    }

    private void Update()
    {
        if (!playerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (LoadoutUI.Instance != null)
            {
                LoadoutUI.Instance.Toggle();
            }
        }
    }
}
