using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Enemy enemy;

    private void OnEnable()
    {
        if (enemy == null) return;
        enemy.OnHealthChanged += UpdateBar;
        // Sync immediately in case Enemy.Start() already fired before we subscribed
        UpdateBar(enemy.CurrentHealth, enemy.MaxHealth);
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnHealthChanged -= UpdateBar;
    }

    private void UpdateBar(float current, float max)
    {
        if (fillImage != null && max > 0f)
            fillImage.fillAmount = current / max;
    }
}
