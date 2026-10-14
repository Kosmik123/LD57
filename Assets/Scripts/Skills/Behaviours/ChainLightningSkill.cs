using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Łańcuch błyskawic: najbliższy wróg w 8 m od gracza, potem 3/4/5 skoków po maks. 5 m (Tier 0/1/2/3 = 3/4/5/5).
/// Obrażenia Damage, każdy skok -1 (min. 1). Ewolucja: Tier.Stun na każdym trafionym. Zygzak LineRenderer 0,15 s.
/// </summary>
public class ChainLightningSkill : ISkillBehaviour
{
    private const float FirstRange = 8f;
    private const float JumpRange = 5f;
    private const float ArcTime = 0.15f;
    private static readonly Color Bolt = new Color(0.7f, 0.85f, 1f);

    public void Execute(SkillContext ctx)
    {
        int jumps = 3 + Mathf.Min(ctx.TierIndex, 2);
        var from = ctx.Origin.position + Vector3.up * 0.9f;
        var points = new List<Vector3> { from };
        var hit = new HashSet<Health>();
        int damage = ctx.Tier.Damage;
        var cursor = ctx.Origin.position;
        float range = FirstRange;

        for (int i = 0; i <= jumps; i++)
        {
            var target = MageTargeting.Nearest(cursor, range, ctx.EnemyLayers, hit, out var point);
            if (target == null) break;
            hit.Add(target);
            points.Add(point);
            Strike(ctx, target, point, Mathf.Max(1, damage));
            damage--;
            cursor = target.transform.position;
            range = JumpRange;
        }

        if (points.Count == 1) points.Add(from + ctx.AimDirection * 2.5f); // pudło: krótki trzask przed graczem
        Sfx.Play(SfxKind.Hit);
        var go = new GameObject("Chain Lightning");
        go.AddComponent<LightningArc>().Init(points, ArcTime, Bolt);
    }

    private static void Strike(SkillContext ctx, Health target, Vector3 point, int damage)
    {
        var col = target.GetComponentInChildren<Collider>();
        if (col != null)
            SkillHit.Apply(col, ctx, ctx.Origin.position, damage, 0f, ctx.Tier.Stun, null);
        HitSplash.Burst(point, Bolt, 8);
        WorldVfx.FlashLight(point, Bolt, 2f, 4f, 0.15f);
    }
}

/// <summary>Wyszukiwanie wrogów dla skilli maga (ten sam test co SkillHit.Apply: żywy Health z tagiem Enemy).</summary>
public static class MageTargeting
{
    public static Health Nearest(Vector3 from, float range, LayerMask layers, HashSet<Health> exclude, out Vector3 point)
    {
        Health best = null;
        float bestSqr = float.MaxValue;
        point = from;
        foreach (var col in Physics.OverlapSphere(from, range, layers))
        {
            var health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;
            if (!health.CompareTag("Enemy") && !col.CompareTag("Enemy")) continue;
            if (exclude != null && exclude.Contains(health)) continue;
            var flat = health.transform.position - from;
            flat.y = 0f;
            float sqr = flat.sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = health;
            point = col.bounds.center;
        }
        return best;
    }
}

/// <summary>Zygzak LineRenderer przez punkty, znika po `life` sekundach.</summary>
public class LightningArc : MonoBehaviour
{
    private LineRenderer line;
    private Material material;
    private float life, elapsed;

    public void Init(List<Vector3> points, float seconds, Color color)
    {
        life = seconds;
        var path = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            int segs = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.6f));
            var side = Vector3.Cross((b - a).normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();
            for (int s = 0; s < segs; s++)
            {
                var p = Vector3.Lerp(a, b, s / (float)segs);
                if (s > 0) p += side * Random.Range(-0.25f, 0.25f) + Vector3.up * Random.Range(-0.15f, 0.15f);
                path.Add(p);
            }
        }
        path.Add(points[points.Count - 1]);

        line = gameObject.AddComponent<LineRenderer>();
        material = SwordTrail.CreateTransparentMaterial("Lightning (runtime)", Color.white);
        line.sharedMaterial = material;
        line.positionCount = path.Count;
        line.SetPositions(path.ToArray());
        line.useWorldSpace = true;
        line.widthMultiplier = 0.15f;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = g;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (line != null)
        {
            line.widthMultiplier = Mathf.Lerp(0.15f, 0.02f, elapsed / life);
        }
        if (elapsed >= life) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
