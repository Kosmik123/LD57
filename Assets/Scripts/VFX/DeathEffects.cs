using UnityEngine;

/// <summary>
/// Efekty śmierci per typ wroga (rozpoznawany po komponencie). Zapadnięcie bryły robi HitReaction (VisualRig.Collapse),
/// więc tu nie ma skalowania Visual. Wołane z EnemyDeath po wyłączeniu colliderów.
/// </summary>
public static class DeathEffects
{
    private static readonly Color ShooterCore = new Color(1f, 0.95f, 0.2f);
    private static readonly Color SpiderDust = new Color(0.45f, 0.25f, 0.6f, 0.8f);

    public static void Play(GameObject enemy)
    {
        if (enemy == null) return;
        var pos = enemy.transform.position;
        var body = HitSplash.ColorOf(enemy);

        if (enemy.GetComponent<SpiderMotherBoss>() != null) Boss(pos, body);
        else if (enemy.GetComponent<SpiderController>() != null) Spider(pos, body, 1);
        else if (enemy.GetComponent<ShooterController>() != null) Shooter(enemy, pos);
        else if (enemy.GetComponent<SlimeController>() != null) Slime(pos, body);
    }

    private static void Slime(Vector3 pos, Color body)
    {
        var center = pos + Vector3.up * 0.4f;
        for (int i = 0; i < 14; i++)
        {
            var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            WorldVfx.Emit(WorldVfx.Kind.Spark, center, dir.normalized * Random.Range(2f, 5f), body,
                          Random.Range(0.12f, 0.28f), Random.Range(0.35f, 0.6f));
        }
        WorldVfx.Puddle(WorldVfx.GroundBelow(pos), 0.7f, body, 2.5f);
    }

    private static void Spider(Vector3 pos, Color body, int scale)
    {
        var center = pos + Vector3.up * 0.4f;
        int legs = 8 * scale;
        for (int i = 0; i < legs; i++)
        {
            var side = Random.insideUnitCircle.normalized;
            var v = new Vector3(side.x, 0f, side.y) * Random.Range(2f, 4.5f) + Vector3.up * Random.Range(2f, 4.5f);
            WorldVfx.Shard(center + Random.insideUnitSphere * 0.15f * scale, v, body, Random.Range(0.35f, 0.6f), 0.6f);
        }
        for (int i = 0; i < 8 * scale; i++)
            WorldVfx.Emit(WorldVfx.Kind.Puff, center + Random.insideUnitSphere * 0.25f * scale,
                          Random.insideUnitSphere * 1.2f + Vector3.up * 0.5f, SpiderDust,
                          Random.Range(0.25f, 0.5f) * scale, Random.Range(0.5f, 0.8f));
    }

    private static void Shooter(GameObject enemy, Vector3 pos)
    {
        var center = pos + Vector3.up * 0.6f;
        var core = CoreColor(enemy);
        WorldVfx.Ring(center, 1.2f, core, 0.2f, 1f, true);
        for (int i = 0; i < 10; i++)
        {
            var dir = Random.insideUnitSphere * 0.5f + Vector3.up;
            WorldVfx.Emit(WorldVfx.Kind.Spark, center, dir.normalized * Random.Range(2.5f, 5f), core,
                          Random.Range(0.06f, 0.12f), Random.Range(0.3f, 0.5f));
        }
        WorldVfx.FlashLight(center, core, 4f, 4f, 0.2f);
    }

    private static void Boss(Vector3 pos, Color body)
    {
        Spider(pos, body, 3);
        var ground = WorldVfx.GroundBelow(pos);
        WorldVfx.Ring(ground, 3.5f, SpiderDust, 0.5f, 0.8f, false);
        WorldVfx.Ring(ground + Vector3.up * 0.02f, 2f, Color.white, 0.35f, 0.7f, false);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f, 0.35f);
        HitStop.Freeze(0.12f);
    }

    private static Color CoreColor(GameObject enemy)
    {
        foreach (var t in enemy.GetComponentsInChildren<Transform>())
        {
            if (t.name != "Core") continue;
            var r = t.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                return r.sharedMaterial.GetColor("_BaseColor");
        }
        return ShooterCore;
    }
}
