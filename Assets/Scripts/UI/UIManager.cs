using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private Health playerHealth;

    [SerializeField]
    private Image healthBar;

    private void OnEnable()
    {
        playerHealth.OnHealthChanged += PlayerHealth_OnHealthChanged;
    }

    private void PlayerHealth_OnHealthChanged(int dmg)
    {
        healthBar.fillAmount = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;
    }

    private void OnDisable()
    {
        playerHealth.OnHealthChanged -= PlayerHealth_OnHealthChanged;
    }
}
