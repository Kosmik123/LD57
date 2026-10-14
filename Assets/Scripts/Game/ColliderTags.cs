using UnityEngine;

/// <summary>
/// Sprawdzanie tagu przez collider. Collidery gracza i wrogów są na dzieciach bez tagu
/// (np. "Capsule" w Player.prefab), tag jest na roocie z Rigidbody. Zawsze używaj tego zamiast
/// other.CompareTag(...) w OnTriggerEnter.
/// </summary>
public static class ColliderTags
{
    public static bool HasTag(Collider collider, string tag)
    {
        if (collider == null) return false;
        var body = collider.attachedRigidbody;
        if (body != null && body.CompareTag(tag)) return true;
        if (collider.CompareTag(tag)) return true;
        var root = collider.transform.root;
        return root != null && root.CompareTag(tag);
    }

    public static bool IsPlayer(Collider collider) => HasTag(collider, "Player");
    public static bool IsEnemy(Collider collider) => HasTag(collider, "Enemy");
}
