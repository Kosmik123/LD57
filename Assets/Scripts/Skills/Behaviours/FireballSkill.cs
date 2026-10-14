using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kula ognia: leci 14 m/s do 12 m, przy trafieniu/ścianie/końcu zasięgu wybucha w promieniu Range (Damage).
/// Ewolucja (tier 3): trzy dodatkowe ticki w tym miejscu co 0,5 s po pół obrażeń.
/// </summary>
public class FireballSkill : ISkillBehaviour
{
    private const float Speed = 14f;
    private const float FlightRange = 12f;
    private const float TickInterval = 0.5f;
    private const int Ticks = 3;
    private static readonly Color Fire = new Color(1f, 0.3f, 0.1f);

    public void Execute(SkillContext ctx)
    {
        var dir = ctx.AimDirection;
        var origin = ctx.Origin.position + Vector3.up * 0.9f + dir * 0.5f;
        PlayerBoltPool.Get().Launch(ctx.Origin, origin, dir, Speed, FlightRange, 0, Fire, 0.5f, true, pos => Explode(ctx, pos));
        Sfx.Play(SfxKind.Shot);
    }

    private void Explode(SkillContext ctx, Vector3 pos)
    {
        Blast(ctx, pos, ctx.Tier.Damage, true);
        if (ctx.TierIndex >= SkillDefinition.MaxTier && ctx.Runner != null)
            ctx.Runner.StartCoroutine(Burn(ctx, pos));
    }

    private IEnumerator Burn(SkillContext ctx, Vector3 pos)
    {
        for (int i = 0; i < Ticks; i++)
        {
            yield return new WaitForSeconds(TickInterval);
            Blast(ctx, pos, Mathf.Max(1, ctx.Tier.Damage / 2), false);
        }
    }

    private static void Blast(SkillContext ctx, Vector3 pos, int damage, bool big)
    {
        float radius = ctx.Tier.Range;
        var ground = WorldVfx.GroundBelow(pos);
        if (big) SkillImpactVfx.Shockwave(ground, radius, Fire);
        else WorldVfx.Ring(ground, radius * 0.8f, Fire, 0.35f, 0.5f, false);
        WorldVfx.FlashLight(pos, Fire, 3f, radius * 2.5f, 0.3f);
        SkillHit.SpawnEffect(ctx, pos, Quaternion.identity);

        var already = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(pos, radius, ctx.EnemyLayers))
            SkillHit.Apply(col, ctx, pos, damage, 0f, 0f, already);
    }
}
