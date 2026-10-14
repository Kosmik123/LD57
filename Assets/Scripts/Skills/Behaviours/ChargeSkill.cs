using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Szarża: ruch o Range w AimDirection w ~0,2 s, trafia raz każdego w kapsule. Tier 3: ślad ognia.</summary>
public class ChargeSkill : ISkillBehaviour
{
    private const float ChargeTime = 0.2f;

    public void Execute(SkillContext ctx)
    {
        ctx.Runner.StartCoroutine(Charge(ctx));
    }

    private IEnumerator Charge(SkillContext ctx)
    {
        var dir = ctx.AimDirection;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        var start = ctx.Origin.position;
        float range = ctx.Tier.Range;
        var already = new HashSet<Health>();
        bool fire = ctx.TierIndex >= SkillDefinition.MaxTier;
        float nextTrail = 0f;

        float speed = range / ChargeTime;
        // Ruch robi PlayerController (ForceDash), bo jego FixedUpdate nadpisuje linearVelocity co krok.
        if (!ctx.Player.ForceDash(dir, range, speed)) yield break;
        VoxelFxRecipes.ChargeStart(ctx, start, dir, ChargeTime, fire);
        float t = 0f;
        var prev = start;
        while (t < ChargeTime)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;

            var pos = ctx.Origin.position;
            var hits = Physics.OverlapCapsule(prev, pos, 0.7f, ctx.EnemyLayers);
            foreach (var col in hits)
                SkillHit.Apply(col, ctx, start, ctx.Tier.Damage, 0f, 0f, already);
            prev = pos;

            if (fire)
            {
                float travelled = Vector3.Distance(start, pos);
                while (nextTrail <= travelled && nextTrail <= range)
                {
                    FireTrail.Spawn(start + dir * nextTrail, ctx);
                    VoxelFxRecipes.FireSpot(start + dir * nextTrail);
                    nextTrail += 1f;
                }
            }
        }
        VoxelFxRecipes.ChargeEnd(ctx.Origin != null ? ctx.Origin.position : prev, fire);
    }
}

/// <summary>Ślad ognia: trigger zadający 2 dmg/s wrogom w środku przez Duration sekund.</summary>
public class FireTrail : MonoBehaviour
{
    private readonly Dictionary<Health, float> timers = new Dictionary<Health, float>();

    public static void Spawn(Vector3 pos, SkillContext ctx)
    {
        var go = new GameObject("FireTrail");
        go.transform.position = pos;
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.8f;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        go.AddComponent<FireTrail>();
        if (ctx.Tier.EffectPrefab != null)
            Instantiate(ctx.Tier.EffectPrefab, pos, Quaternion.identity, go.transform);
        Destroy(go, ctx.Tier.Duration);
    }

    private void OnTriggerStay(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead || !health.CompareTag("Enemy")) return;
        timers.TryGetValue(health, out var next);
        if (Time.time < next) return;
        timers[health] = Time.time + 1f;
        health.ChangeHealth(-2);
    }
}
