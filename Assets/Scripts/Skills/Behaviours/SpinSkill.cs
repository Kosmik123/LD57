using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Wirowanie: co 0,25 s trafia w promieniu Range przez Duration. Tier 3: przyciąga wrogów o 0,5 m.</summary>
public class SpinSkill : ISkillBehaviour
{
    private const float TickInterval = 0.25f;

    public void Execute(SkillContext ctx)
    {
        ctx.Runner.StartCoroutine(Spin(ctx));
    }

    private IEnumerator Spin(SkillContext ctx)
    {
        SkillHit.SpawnEffect(ctx, ctx.Origin.position, ctx.Origin.rotation);
        var color = ctx.TierIndex >= SkillDefinition.MaxTier ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.75f, 0.3f);
        SkillImpactVfx.SpinArc(ctx.Origin, ctx.Tier.Range, ctx.Tier.Duration, color);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.08f);
        float elapsed = 0f;
        while (elapsed < ctx.Tier.Duration)
        {
            Tick(ctx);
            yield return new WaitForSeconds(TickInterval);
            elapsed += TickInterval;
        }
    }

    private void Tick(SkillContext ctx)
    {
        var center = ctx.Origin.position;
        var hits = Physics.OverlapSphere(center, ctx.Tier.Range, ctx.EnemyLayers);
        var already = new HashSet<Health>();
        foreach (var col in hits)
        {
            SkillHit.Apply(col, ctx, center, ctx.Tier.Damage, 0f, 0f, already);
            if (ctx.TierIndex >= SkillDefinition.MaxTier)
                Pull(col, center);
        }
    }

    private void Pull(Collider col, Vector3 center)
    {
        var agent = col.GetComponentInParent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        var dir = center - agent.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.25f) return;
        agent.Move(dir.normalized * 0.5f);
    }
}
