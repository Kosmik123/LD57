using UnityEngine;

[RequireComponent(typeof (Health))]
public class EnemyHealth : MonoBehaviour
{
    public static event System.Action<EnemyHealth, int> OnEnemyDamaged;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnHealthChanged += Health_OnHealthChanged;
    }

    private void Health_OnHealthChanged(int change)
    {
        if (change < 0)
            OnEnemyDamaged?.Invoke(this, -change);
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= Health_OnHealthChanged;
    }
}
