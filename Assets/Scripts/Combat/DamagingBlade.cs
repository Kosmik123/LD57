using NaughtyAttributes;
using UnityEngine;

public class DamagingBlade : MonoBehaviour
{
    [SerializeField, Tag]
    private string[] damagedTags;

    [SerializeField]
    private int damage;

    private void OnTriggerEnter(Collider otherCollider)
    {
        var other = otherCollider.attachedRigidbody;
        if (System.Array.IndexOf(damagedTags, other.tag) < 0)
            return;

        if (other.TryGetComponent<Health>(out var health))
        {
            health.ChangeHealth(-damage);
        }
    }
}
