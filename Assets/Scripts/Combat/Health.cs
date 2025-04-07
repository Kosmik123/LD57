using UnityEngine;

public class Health : MonoBehaviour
{
    public event System.Action<int> OnHealthChanged;
    public event System.Action OnDied;

    [SerializeField]
    private int maxHealth;
    public int MaxHealth => maxHealth;

    [SerializeField]
    private int currentHealth;
    public int CurrentHealth => currentHealth;

    private void Start()
    {
        FullHeal();
    }

    public void FullHeal() => ChangeHealth(maxHealth);

    public void ChangeHealth(int change)
    {
        currentHealth = Mathf.Clamp(currentHealth + change, 0, maxHealth);
        OnHealthChanged?.Invoke(change);
        if (currentHealth <= 0)
            OnDied?.Invoke();
    }
}
