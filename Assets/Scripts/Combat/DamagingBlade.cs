using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.AI;

public class DamagingBlade : MonoBehaviour
{
    [SerializeField, Tag]
    private string[] damagedTags;

    [SerializeField]
    private int damage;

    [SerializeField, Tooltip("Siła odrzutu w metrach (0 = brak).")]
    private float knockback = 0f;

    /// <summary>Trafienie celu; argument = punkt trafienia (świat).</summary>
    public event System.Action<Vector3> OnHit;

    // Każdy Health jest trafiany najwyżej raz na włączenie.
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    private void OnEnable()
    {
        alreadyHit.Clear();
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        var body = otherCollider.attachedRigidbody;
        var root = body != null ? body.gameObject : otherCollider.gameObject;

        if (System.Array.IndexOf(damagedTags, root.tag) < 0)
            return;

        var health = body != null ? body.GetComponent<Health>() : null;
        if (health == null)
            health = otherCollider.GetComponentInParent<Health>();
        if (health == null || !alreadyHit.Add(health))
            return;

        // ostrze gracza (rani tag Enemy): mnożnik obrażeń, kryt, Łowca elit z PlayerStats
        int dealt = System.Array.IndexOf(damagedTags, "Enemy") >= 0 ? PlayerStats.Scale(damage, health) : damage;
        health.ChangeHealth(-dealt);
        OnHit?.Invoke(otherCollider.ClosestPoint(transform.position));
        ApplyKnockback(health.gameObject, body);
    }

    private void ApplyKnockback(GameObject target, Rigidbody body)
    {
        if (knockback <= 0f)
            return;

        var direction = target.transform.position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        if (target.TryGetComponent<Knockback>(out var kb))
        {
            kb.Apply(direction, knockback, 0.15f); // odrzut animowany, nie skok o stałą
        }
        else if (target.TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(direction * knockback);
        }
        else if (body != null && !body.isKinematic)
        {
            body.AddForce(direction * knockback, ForceMode.VelocityChange);
        }
    }
}
