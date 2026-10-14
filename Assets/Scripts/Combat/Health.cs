using UnityEngine;

public class Health : MonoBehaviour
{
    /// <summary>Zmiana HP (ujemna = obrażenia, dodatnia = leczenie). Wywoływane także gdy zmiana została zablokowana nietykalnością? Nie: wtedy nie ma zdarzenia.</summary>
    public event System.Action<int> OnHealthChanged;
    public event System.Action OnDied;

    [SerializeField]
    private int maxHealth;
    public int MaxHealth => maxHealth;

    [SerializeField]
    private int currentHealth;
    public int CurrentHealth => currentHealth;

    /// <summary>Gdy true, ujemne zmiany HP są ignorowane (dash gracza, zeskok pająka itp.). Leczenie działa zawsze.</summary>
    public bool IsInvulnerable { get; set; }

    public bool IsDead => currentHealth <= 0;

    private void Start()
    {
        FullHeal();
    }

    public void FullHeal() => ChangeHealth(maxHealth);

    public void ChangeHealth(int change)
    {
        if (change < 0 && (IsInvulnerable || IsDead))
            return;

        currentHealth = Mathf.Clamp(currentHealth + change, 0, maxHealth);
        OnHealthChanged?.Invoke(change);
        if (currentHealth <= 0)
            OnDied?.Invoke();
    }
}
