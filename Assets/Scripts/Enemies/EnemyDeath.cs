using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Śmierć wroga: wyłącza AI i collidery, po opóźnieniu niszczy obiekt. Jedyne miejsce niszczące wroga.</summary>
[RequireComponent(typeof(Health))]
public class EnemyDeath : MonoBehaviour
{
    [SerializeField] private float destroyDelay = 0.5f;
    [SerializeField] private GameObject deathEffect;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    // Start, nie OnEnable: zapewnia rejestrację po EnemyHealth (OnEnable).
    private void Start()
    {
        health.OnDied += Health_OnDied;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDied -= Health_OnDied;
    }

    private void Health_OnDied()
    {
        if (TryGetComponent<NavMeshAgent>(out var agent))
            agent.enabled = false;
        if (TryGetComponent<BehaviorGraphAgent>(out var behavior))
            behavior.enabled = false;
        foreach (var attack in GetComponentsInChildren<EnemyMeleeAttack>())
            attack.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (deathEffect) Instantiate(deathEffect, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        DeathEffects.Play(gameObject);
        Destroy(gameObject, destroyDelay);
    }
}
