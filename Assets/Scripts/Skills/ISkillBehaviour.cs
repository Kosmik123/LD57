using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;

public interface ISkillBehaviour
{
    void Execute(SkillContext ctx);
}

public struct SkillContext
{
    public PlayerController Player;
    public Transform Origin;
    public SkillTier Tier;
    public int TierIndex;
    public LayerMask EnemyLayers;
    /// <summary>Do uruchamiania coroutine.</summary>
    public MonoBehaviour Runner;
    /// <summary>Kierunek Player.transform.forward w płaszczyźnie XZ.</summary>
    public Vector3 AimDirection;
}

/// <summary>Wspólne pomocnicze metody trafiania wrogów dla skilli.</summary>
public static class SkillHit
{
    public static void Apply(Collider col, SkillContext ctx, Vector3 from, int damage, float knockback, float stun, HashSet<Health> already)
    {
        var health = col.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return;
        if (already != null && !already.Add(health)) return;
        if (!health.CompareTag("Enemy") && !col.CompareTag("Enemy")) return;

        if (damage > 0) health.ChangeHealth(-PlayerStats.Scale(damage, health)); // mnożnik obrażeń, kryt, Łowca elit

        var agent = health.GetComponentInParent<NavMeshAgent>();
        var dir = health.transform.position - from;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : ctx.AimDirection;

        if (knockback > 0f)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(dir * knockback);
            else
            {
                var rb = health.GetComponentInParent<Rigidbody>();
                if (rb != null) rb.AddForce(dir * knockback * 5f, ForceMode.VelocityChange);
            }
        }

        if (stun > 0f && agent != null && ctx.Runner != null)
            ctx.Runner.StartCoroutine(StunRoutine(agent, stun));
    }

    private static IEnumerator StunRoutine(NavMeshAgent agent, float seconds)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) yield break;
        agent.isStopped = true;
        yield return new WaitForSeconds(seconds);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
    }

    public static void SpawnEffect(SkillContext ctx, Vector3 pos, Quaternion rot)
    {
        if (ctx.Tier.EffectPrefab == null) return;
        var fx = Object.Instantiate(ctx.Tier.EffectPrefab, pos, rot);
        Object.Destroy(fx, ctx.Tier.Duration + 1f);
    }
}
