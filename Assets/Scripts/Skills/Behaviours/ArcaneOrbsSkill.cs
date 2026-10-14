using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Magiczne kule: 3/4/5 kul (Tier 0/1/2/3 = 3/4/5/5) krążą wokół gracza Duration sekund (promień = Range, 180 st./s),
/// trafiają przy kontakcie (OverlapSphere 0,4 m, cooldown 0,5 s na wroga), Damage. Ewolucja: co 1 s jedna kula
/// strzela pociskiem (Damage) w najbliższego wroga w 10 m.
/// </summary>
public class ArcaneOrbsSkill : ISkillBehaviour
{
    private const float AngularSpeed = 180f;
    private const float HitRadius = 0.4f;
    private const float PerEnemyCooldown = 0.5f;
    private const float ShotInterval = 1f;
    private const float ShotRange = 10f;
    private static readonly Color Violet = new Color(0.7f, 0.4f, 1f);

    public void Execute(SkillContext ctx)
    {
        ctx.Runner.StartCoroutine(Run(ctx));
    }

    private IEnumerator Run(SkillContext ctx)
    {
        int count = 3 + Mathf.Min(ctx.TierIndex, 2);
        bool shoots = ctx.TierIndex >= SkillDefinition.MaxTier;
        float radius = ctx.Tier.Range;
        float duration = Mathf.Max(0.5f, ctx.Tier.Duration);
        var origin = ctx.Origin;

        var orbs = new Transform[count];
        for (int i = 0; i < count; i++)
            orbs[i] = CreateOrb();
        SkillHit.SpawnEffect(ctx, origin.position, Quaternion.identity);
        Sfx.Play(SfxKind.LevelUp, origin.position, 0.5f);

        var cooldowns = new Dictionary<Health, float>();
        float angle = 0f, t = 0f, nextShot = ShotInterval;
        int shooter = 0;
        while (t < duration && origin != null)
        {
            angle += AngularSpeed * Time.deltaTime;
            t += Time.deltaTime;
            var center = origin.position + Vector3.up * 0.9f;
            for (int i = 0; i < count; i++)
            {
                if (orbs[i] == null) continue;
                float a = (angle + 360f * i / count) * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                orbs[i].position = pos;
                Hit(ctx, pos, cooldowns, orbs[i]);
            }

            if (shoots && t >= nextShot)
            {
                nextShot += ShotInterval;
                Shoot(ctx, orbs[shooter % count], center);
                shooter++;
            }
            yield return null;
        }

        foreach (var orb in orbs)
            if (orb != null) Object.Destroy(orb.gameObject);
    }

    private static Transform CreateOrb()
    {
        var go = new GameObject("Arcane Orb");
        MageStylizedFx.OrbVisual(go.transform, Violet, 0.36f);
        return go.transform;
    }

    private static void Hit(SkillContext ctx, Vector3 pos, Dictionary<Health, float> cooldowns, Transform orb)
    {
        foreach (var col in Physics.OverlapSphere(pos, HitRadius, ctx.EnemyLayers))
        {
            var health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;
            if (cooldowns.TryGetValue(health, out var until) && Time.time < until) continue;
            if (!health.CompareTag("Enemy") && !col.CompareTag("Enemy")) continue;
            cooldowns[health] = Time.time + PerEnemyCooldown;
            SkillHit.Apply(col, ctx, pos, ctx.Tier.Damage, 0f, 0f, null);
            MageStylizedFx.PulseOrb(orb);
            MageStylizedFx.Flash(pos, Violet, 0.6f);
            MageStylizedFx.Burst(pos, Violet, 6, 0.14f, 4f, 0.45f);
        }
    }

    private static void Shoot(SkillContext ctx, Transform orb, Vector3 center)
    {
        var target = MageTargeting.Nearest(ctx.Origin.position, ShotRange, ctx.EnemyLayers, null, out var point);
        if (target == null) return;
        var from = orb != null ? orb.position : center;
        var dir = point - from;
        PlayerBoltPool.Get().Launch(ctx.Origin, from, dir, 16f, ShotRange + 3f, ctx.Tier.Damage, Violet, 0.22f);
        Sfx.Play(SfxKind.Shot, from, 0.5f);
    }
}
