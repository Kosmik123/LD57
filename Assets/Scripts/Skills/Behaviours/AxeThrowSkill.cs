using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Rzut toporem: leci w AimDirection na Range m (20 m/s), wraca do gracza. Trafia raz tam i raz z powrotem. Tier 3: dwa topory pod kątem ±15°.</summary>
public class AxeThrowSkill : ISkillBehaviour
{
    private const float Speed = 20f;
    private const float HitRadius = 0.6f;
    private const float CatchDistance = 0.7f;

    public void Execute(SkillContext ctx)
    {
        if (ctx.TierIndex >= SkillDefinition.MaxTier)
        {
            ctx.Runner.StartCoroutine(Throw(ctx, Quaternion.Euler(0f, 15f, 0f) * ctx.AimDirection));
            ctx.Runner.StartCoroutine(Throw(ctx, Quaternion.Euler(0f, -15f, 0f) * ctx.AimDirection));
        }
        else
        {
            ctx.Runner.StartCoroutine(Throw(ctx, ctx.AimDirection));
        }
    }

    private IEnumerator Throw(SkillContext ctx, Vector3 dir)
    {
        var start = ctx.Origin.position;
        var axe = CreateAxe(ctx, start);
        VoxelFxRecipes.AxeFlight(axe, ctx.TierIndex >= SkillDefinition.MaxTier);
        var outHits = new HashSet<Health>();
        var backHits = new HashSet<Health>();
        float range = ctx.Tier.Range;
        float traveled = 0f;
        bool returning = false;

        float flightStart = Time.time; // bezpiecznik: topór nie może lecieć w nieskończoność (np. gdy gracz zniknął)
        while (axe != null)
        {
            if (ctx.Origin == null) break;
            var pos = axe.transform.position;
            Vector3 target;
            if (!returning)
            {
                target = start + dir * range;
                if (traveled >= range)
                {
                    returning = true;
                    VoxelFxRecipes.AxeTurn(pos);
                }
            }
            else
            {
                target = ctx.Origin.position;
            }

            if (Time.time - flightStart > 4f) break;
            var to = target - pos;
            float step = Speed * Time.fixedDeltaTime;
            if (returning && to.magnitude <= CatchDistance) break;
            var move = to.magnitude <= step ? to : to.normalized * step;
            axe.transform.position = pos + move;
            axe.transform.Rotate(0f, 720f * Time.fixedDeltaTime, 0f, Space.World);
            if (!returning) traveled += move.magnitude;

            var hits = Physics.OverlapSphere(axe.transform.position, HitRadius, ctx.EnemyLayers);
            var set = returning ? backHits : outHits;
            foreach (var col in hits)
            {
                int before = set.Count;
                SkillHit.Apply(col, ctx, axe.transform.position, ctx.Tier.Damage, 0f, 0f, set);
                if (set.Count > before) VoxelFxRecipes.AxeHit(axe.transform.position);
            }

            yield return new WaitForFixedUpdate();
        }

        if (axe != null) Object.Destroy(axe);
    }

    private static GameObject CreateAxe(SkillContext ctx, Vector3 pos)
    {
        if (ctx.Tier.EffectPrefab != null)
            return Object.Instantiate(ctx.Tier.EffectPrefab, pos, Quaternion.identity);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Axe";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        return go;
    }
}
