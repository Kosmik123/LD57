using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kula ognia: leci 16 m/s do 20 m, przy trafieniu/ścianie/końcu zasięgu wybucha w promieniu Range (Damage).
/// Ewolucja (tier 3): trzy dodatkowe ticki w tym miejscu co 0,5 s po pół obrażeń.
/// </summary>
public class FireballSkill : ISkillBehaviour
{
    private const float Speed = 16f;
    private const float FlightRange = 20f;
    private const float TickInterval = 0.5f;
    private const int Ticks = 3;
    private static readonly Color Fire = new Color(1f, 0.3f, 0.1f);
    private static readonly Color Ember = new Color(1f, 0.65f, 0.1f);
    private static readonly Color Smoke = new Color(0.12f, 0.1f, 0.1f);

    public void Execute(SkillContext ctx)
    {
        var dir = ctx.AimDirection;
        var origin = ctx.Origin.position + Vector3.up * 0.9f + dir * 0.5f;
        PlayerBoltPool.Get().Launch(ctx.Origin, origin, dir, Speed, FlightRange, 0, Fire, 0.5f, true, pos => Explode(ctx, pos), true);
        Sfx.Play(SfxKind.Shot);
    }

    private void Explode(SkillContext ctx, Vector3 pos)
    {
        Blast(ctx, pos, ctx.Tier.Damage, true);
        if (ctx.TierIndex >= SkillDefinition.MaxTier && ctx.Runner != null)
        {
            ctx.Runner.StartCoroutine(Burn(ctx, pos));
            // stałe tlenie: iskry z płonącej ziemi co 0,3 s przez 3 s
            var ground = WorldVfx.GroundBelow(pos);
            float radius = ctx.Tier.Range;
            MageStylizedFx.Repeat(3f, 0.3f, _ =>
            {
                var p = ground + Random.insideUnitSphere * radius * 0.6f;
                p.y = ground.y + 0.1f;
                MageStylizedFx.FireSpark(p, Vector3.zero);
            });
        }
    }

    private IEnumerator Burn(SkillContext ctx, Vector3 pos)
    {
        for (int i = 0; i < Ticks; i++)
        {
            yield return new WaitForSeconds(TickInterval);
            Blast(ctx, pos, Mathf.Max(1, ctx.Tier.Damage / 2), false);
            Flames(pos, ctx.Tier.Range);
        }
        yield return new WaitForSeconds(TickInterval);
        Flames(pos, ctx.Tier.Range); // płonąca ziemia: wizual trwa 2 s (4 rzuty płomieni co 0,5 s), obrażenia dalej 3 ticki
    }

    private static void Flames(Vector3 pos, float radius)
    {
        var ground = WorldVfx.GroundBelow(pos);
        for (int i = 0; i < 3; i++)
        {
            var p = ground + Random.insideUnitSphere * radius * 0.6f;
            p.y = ground.y + 0.1f;
            MageStylizedFx.Burst(p, Random.value < 0.5f ? Fire : Ember, 4, 0.12f, 2.5f, 0.6f);
        }
    }

    private static void Blast(SkillContext ctx, Vector3 pos, int damage, bool big)
    {
        float radius = ctx.Tier.Range;
        var ground = WorldVfx.GroundBelow(pos);
        if (big) SkillImpactVfx.Shockwave(ground, radius, Fire);
        else WorldVfx.Ring(ground, radius * 0.8f, Fire, 0.35f, 0.5f, false);
        MageStylizedFx.Flash(pos, Ember, big ? 2.2f : 1f);
        if (big)
        {
            // duży biały błysk, kula ognia rozlatuje się na 24 bryły, 2 pierścienie, słup dymu, żar i spalona plama
            MageStylizedFx.Flash(pos, Color.white, 3.2f);
            MageStylizedFx.Ring(ground, radius, new Color(1f, 0.85f, 0.5f), 28);                 // szybki jasny
            MageStylizedFx.Ring(ground, radius * 0.9f, new Color(0.85f, 0.12f, 0.05f), 20, 1.1f); // wolny czerwony
            MageStylizedFx.Burst(pos, Fire, 12, 0.28f, 7f, 0.75f);
            MageStylizedFx.Burst(pos, Ember, 12, 0.22f, 8f, 0.65f);
            MageStylizedFx.Burst(pos, Smoke, 6, 0.3f, 4f, 1.1f);
            var column = ground;
            MageStylizedFx.Repeat(1.5f, 0.125f, k => MageStylizedFx.SmokePuff(column + Vector3.up * (0.3f + k * 0.8f), 1.6f, 0.5f)); // 12 kłębów w górę
            MageStylizedFx.GroundGlow(ground, radius, Fire, 3f, 1f);
            MageStylizedFx.Scorch(ground, radius * 0.9f, 6f);
        }
        else
        {
            MageStylizedFx.Ring(ground, radius * 0.8f, Fire, 14);
        }
        SkillHit.SpawnEffect(ctx, pos, Quaternion.identity);

        var already = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(pos, radius, ctx.EnemyLayers))
            SkillHit.Apply(col, ctx, pos, damage, 0f, 0f, already);
    }
}
