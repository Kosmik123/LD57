using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lodowa nova: AoE wokół gracza (Range, Damage), spowolnienie wrogów do 40% prędkości na Duration sekund.
/// Ewolucja: Tier.Stun (ogłuszenie przez SkillHit.Apply).
/// Spowolnienie nadpisuje agent.speed co klatkę (kontrolery wrogów resetują prędkość przy zmianie stanu),
/// po czasie wraca do ostatniej wartości ustawionej przez wroga.
/// </summary>
public class FrostNovaSkill : ISkillBehaviour
{
    private const float SlowFactor = 0.4f;
    private static readonly Color Ice = new Color(0.6f, 0.9f, 1f);
    private static readonly Dictionary<NavMeshAgent, float> slowedUntil = new Dictionary<NavMeshAgent, float>();

    public void Execute(SkillContext ctx)
    {
        var center = ctx.Origin.position;
        float radius = ctx.Tier.Range;
        Visual(center, radius);
        SkillHit.SpawnEffect(ctx, center, Quaternion.identity);
        Sfx.Play(SfxKind.Attack);

        var already = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(center, radius, ctx.EnemyLayers))
        {
            SkillHit.Apply(col, ctx, center, ctx.Tier.Damage, 0f, ctx.Tier.Stun, already);
        }
        foreach (var health in already)
        {
            if (health == null || health.IsDead || !health.CompareTag("Enemy")) continue;
            // oblodzenie: 4 kostki przyczepione na czas spowolnienia, 8 przy ewolucji (zamrożeni)
            MageStylizedFx.Burst(health.transform.position + Vector3.up * 0.8f, Ice, 4, 0.1f, 3f, 0.4f);
            MageFrostTint.Apply(health.gameObject, new Color(0.5f, 0.85f, 1f), ctx.Tier.Duration); // jasnoniebieski tint na czas spowolnienia
            MageAttachedCubes.Attach(health.transform, Ice, ctx.Tier.Stun > 0f ? 8 : 4, ctx.Tier.Duration, false, 0.16f);
            var agent = health.GetComponentInParent<NavMeshAgent>();
            if (agent == null || ctx.Runner == null) continue;
            bool running = slowedUntil.ContainsKey(agent);
            slowedUntil[agent] = Time.time + ctx.Tier.Duration;
            if (!running) ctx.Runner.StartCoroutine(Slow(agent));
        }
    }

    private static IEnumerator Slow(NavMeshAgent agent)
    {
        float baseSpeed = agent != null ? agent.speed : 0f;
        float applied = -1f;
        while (agent != null && slowedUntil.TryGetValue(agent, out var until) && Time.time < until)
        {
            if (!Mathf.Approximately(agent.speed, applied)) baseSpeed = agent.speed; // wróg ustawił własną prędkość
            applied = baseSpeed * SlowFactor;
            agent.speed = applied;
            yield return null;
        }
        if (agent != null)
        {
            if (Mathf.Approximately(agent.speed, applied)) agent.speed = baseSpeed;
            slowedUntil.Remove(agent);
        }
    }

    private static void Visual(Vector3 center, float radius)
    {
        var ground = WorldVfx.GroundBelow(center);
        WorldVfx.Ring(ground, radius, Ice, 0.5f, 0.8f, false);
        WorldVfx.Ring(ground, radius * 0.55f, Color.white, 0.35f, 0.6f, false);
        MageStylizedFx.Flash(center + Vector3.up * 0.5f, Ice, 1.5f);
        MageStylizedFx.Ring(ground, radius, Ice, 36);
        MageStylizedFx.Ring(ground, radius * 0.55f, Color.white, 20);
        MageStylizedFx.IceGrowth(ground, radius, Ice, 16, 8); // sople rosną, stoją 1,2 s, kruszą się; mgiełka mrozu
        MageStylizedFx.GroundGlow(ground, radius * 0.8f, Ice, 1.5f, 0.5f);
    }
}
