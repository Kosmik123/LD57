using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Teleport na spacji (zamiast uderzenia w ziemię): Range metrów w kierunku ruchu (bez ruchu: forward), cel z NavMesh,
/// skrócony przed ścianą. Nietykalność Duration sekund, powidok na starcie, wybuch w celu (Damage, promień 1,5 m).
/// Ewolucja: wybuch także na starcie.
/// </summary>
public class BlinkSkill : ISkillBehaviour
{
    private const float BlastRadius = 1.5f;
    private const float GhostFade = 0.35f;
    private static readonly Color Arcane = new Color(0.6f, 0.45f, 1f);

    public void Execute(SkillContext ctx)
    {
        var player = ctx.Player;
        var rb = player.GetComponent<Rigidbody>();
        var start = player.transform.position;

        var dir = Vector3.zero;
        if (rb != null)
        {
            dir = rb.linearVelocity;
            dir.y = 0f;
        }
        dir = dir.sqrMagnitude > 0.25f ? dir.normalized : ctx.AimDirection;

        if (!FindTarget(player.transform, start, dir, ctx.Tier.Range, out var target)) return;

        SpawnGhost(player.transform, ctx.Runner);
        Sfx.Play(SfxKind.Dash);
        bool evolved = ctx.TierIndex >= SkillDefinition.MaxTier;
        if (evolved) Blast(ctx, start);

        if (rb != null) rb.position = target;
        player.transform.position = target;
        Physics.SyncTransforms();

        Blast(ctx, target);
        var health = player.GetComponent<Health>();
        if (health != null && ctx.Runner != null)
            ctx.Runner.StartCoroutine(Invulnerable(health, ctx.Tier.Duration));
    }

    private static bool FindTarget(Transform player, Vector3 start, Vector3 dir, float range, out Vector3 target)
    {
        target = start;
        float dist = range;
        var hits = Physics.RaycastAll(start + Vector3.up * 0.5f, dir, range, ~0, QueryTriggerInteraction.Ignore);
        float wall = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(player)) continue;
            if (ColliderTags.IsEnemy(h.collider)) continue; // przez wrogów można się teleportować
            if (h.distance < wall) wall = h.distance;
        }
        if (wall < float.MaxValue) dist = Mathf.Max(0f, wall - 0.6f);

        float yOffset = 0f;
        if (NavMesh.SamplePosition(start, out var startNav, 1.5f, NavMesh.AllAreas))
            yOffset = Mathf.Clamp(start.y - startNav.position.y, -0.5f, 2f);

        for (float d = dist; d >= 1f; d -= 1f)
        {
            if (!NavMesh.SamplePosition(start + dir * d, out var nav, 1f, NavMesh.AllAreas)) continue;
            target = nav.position + Vector3.up * yOffset;
            return true;
        }
        return false;
    }

    private static void Blast(SkillContext ctx, Vector3 pos)
    {
        var ground = WorldVfx.GroundBelow(pos);
        WorldVfx.Ring(ground, BlastRadius, Arcane, 0.35f, 0.7f, false);
        WorldVfx.FlashLight(pos + Vector3.up * 0.8f, Arcane, 2.5f, 5f, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            var v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + 0.3f;
            WorldVfx.Shard(pos + Vector3.up * 0.5f, v * Random.Range(1.5f, 3f), Arcane, Random.Range(0.2f, 0.4f), Random.Range(0.3f, 0.5f));
        }
        var already = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(pos, BlastRadius, ctx.EnemyLayers))
            SkillHit.Apply(col, ctx, pos, ctx.Tier.Damage, 0f, 0f, already);
    }

    private static IEnumerator Invulnerable(Health health, float seconds)
    {
        bool was = health.IsInvulnerable;
        health.IsInvulnerable = true;
        yield return new WaitForSeconds(seconds);
        if (health != null && !was) health.IsInvulnerable = false;
    }

    /// <summary>Półprzezroczysta kopia siatki postaci w miejscu startu, gaśnie w GhostFade sekund.</summary>
    private static void SpawnGhost(Transform player, MonoBehaviour runner)
    {
        var smr = player.GetComponentInChildren<SkinnedMeshRenderer>(false);
        if (smr == null || runner == null) return;
        var mesh = new Mesh();
        smr.BakeMesh(mesh, false);
        var go = new GameObject("Blink Afterimage");
        go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
        go.transform.localScale = smr.transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mat = SwordTrail.CreateTransparentMaterial("Blink Ghost (runtime)", new Color(Arcane.r, Arcane.g, Arcane.b, 0.6f));
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        runner.StartCoroutine(FadeGhost(go, mesh, mat));
    }

    private static IEnumerator FadeGhost(GameObject go, Mesh mesh, Material mat)
    {
        float t = 0f;
        while (t < GhostFade && go != null)
        {
            t += Time.deltaTime;
            mat.SetColor("_BaseColor", new Color(Arcane.r, Arcane.g, Arcane.b, 0.6f * (1f - t / GhostFade)));
            yield return null;
        }
        if (go != null) Object.Destroy(go);
        Object.Destroy(mesh);
        Object.Destroy(mat);
    }
}
