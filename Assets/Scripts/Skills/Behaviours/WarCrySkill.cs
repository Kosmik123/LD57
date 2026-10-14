using System.Collections.Generic;
using UnityEngine;

/// <summary>Krzyk bojowy: natychmiast w promieniu Range wokół gracza: Damage, odrzut Knockback, ogłuszenie Stun.</summary>
public class WarCrySkill : ISkillBehaviour
{
    public void Execute(SkillContext ctx)
    {
        var center = ctx.Origin.position;
        SkillHit.SpawnEffect(ctx, center, ctx.Origin.rotation);
        var already = new HashSet<Health>();
        var hits = Physics.OverlapSphere(center, ctx.Tier.Range, ctx.EnemyLayers);
        foreach (var col in hits)
            SkillHit.Apply(col, ctx, center, ctx.Tier.Damage, ctx.Tier.Knockback, ctx.Tier.Stun, already);
    }
}
