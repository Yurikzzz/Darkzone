using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Image crossFill;
    [SerializeField] private PlayerHealth playerHealth;

    private void OnEnable()
    {
        if (playerHealth == null) return;
        playerHealth.OnHealthChanged += UpdateCross;
        UpdateCross(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateCross;
    }

    private void UpdateCross(float current, float max)
    {
        if (crossFill != null && max > 0f)
            crossFill.fillAmount = current / max;
    }
}

