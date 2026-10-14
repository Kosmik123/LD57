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
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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

        var material = SwordTrail.CreateTransparentMaterial("Arcane Orb (runtime)", Color.white);
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, Violet);
        var orbs = new Transform[count];
        for (int i = 0; i < count; i++)
            orbs[i] = CreateOrb(material, block);
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
                Hit(ctx, pos, cooldowns);
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
        Object.Destroy(material);
    }

    private static Transform CreateOrb(Material material, MaterialPropertyBlock block)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Arcane Orb";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.35f;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.SetPropertyBlock(block);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.2f;
        trail.widthMultiplier = 0.3f;
        trail.sharedMaterial = material;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Violet, 0f), new GradientColorKey(Violet, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Violet;
        light.intensity = 1.2f;
        light.range = 2.5f;
        light.shadows = LightShadows.None;
        return go.transform;
    }

    private static void Hit(SkillContext ctx, Vector3 pos, Dictionary<Health, float> cooldowns)
    {
        foreach (var col in Physics.OverlapSphere(pos, HitRadius, ctx.EnemyLayers))
        {
            var health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;
            if (cooldowns.TryGetValue(health, out var until) && Time.time < until) continue;
            if (!health.CompareTag("Enemy") && !col.CompareTag("Enemy")) continue;
            cooldowns[health] = Time.time + PerEnemyCooldown;
            SkillHit.Apply(col, ctx, pos, ctx.Tier.Damage, 0f, 0f, null);
            HitSplash.Burst(pos, Violet, 6);
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
