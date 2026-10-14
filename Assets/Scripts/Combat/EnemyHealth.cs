using UnityEngine;

[RequireComponent(typeof (Health))]
public class EnemyHealth : MonoBehaviour
{
    /// <summary>Wróg otrzymał obrażenia (wartość dodatnia). Używane przez liczby obrażeń w świecie.</summary>
    public static event System.Action<EnemyHealth, int> OnEnemyDamaged;

    /// <summary>Wróg zginął. Wywoływane PRZED jakimkolwiek Destroy, więc subskrybenci mogą jeszcze odczytać pozycję i definicję.</summary>
    public static event System.Action<EnemyHealth> OnEnemyDied;

    [SerializeField, Tooltip("XP za zabicie. Slime 10, pająk 15, strzelec 20, boss 0.")]
    private int experienceReward = 10;
    public int ExperienceReward => experienceReward;

    [SerializeField, Tooltip("Boss: jego śmierć kończy run wygraną.")]
    private bool isBoss;
    public bool IsBoss => isBoss;

    private Health health;
    public Health Health => health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnHealthChanged += Health_OnHealthChanged;
        health.OnDied += Health_OnDied;
    }

    private void Health_OnHealthChanged(int change)
    {
        if (change < 0)
            OnEnemyDamaged?.Invoke(this, -change);
    }

    private void Health_OnDied()
    {
        OnEnemyDied?.Invoke(this);
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= Health_OnHealthChanged;
        health.OnDied -= Health_OnDied;
    }
}
