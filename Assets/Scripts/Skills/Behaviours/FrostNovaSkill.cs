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
        WorldVfx.FlashLight(center + Vector3.up, Ice, 2.5f, radius * 2f, 0.3f);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var vel = dir * Random.Range(radius * 1.2f, radius * 2.2f) + Vector3.up * Random.Range(1f, 3f);
            WorldVfx.Shard(center + Vector3.up * 0.3f + dir * 0.4f, vel, Color.Lerp(Ice, Color.white, Random.value), Random.Range(0.3f, 0.6f), Random.Range(0.4f, 0.7f));
        }
    }
}
