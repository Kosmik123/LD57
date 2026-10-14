using UnityEngine;

/// <summary>
/// Gotowe, warstwowe przepisy efektów wojownika z kostek (VoxelFx): cios, wir, szarża, uderzenie w ziemię,
/// rzut toporem, krzyk bojowy. Skille wołają tylko te metody (logika obrażeń zostaje w skillach).
/// Kolory HDR (powyżej 1) świecą dzięki bloomowi z LevelAmbience.
/// </summary>
public static class VoxelFxRecipes
{
    public static readonly Color HotWhite = new Color(5f, 4.2f, 3f);
    public static readonly Color Orange = new Color(3.2f, 1.5f, 0.4f);
    public static readonly Color Gold = new Color(3.2f, 2.3f, 0.7f);
    public static readonly Color Red = new Color(3.6f, 0.55f, 0.3f);
    public static readonly Color Rock = new Color(0.32f, 0.27f, 0.23f);

    private static Color Accent(bool evolved) { return evolved ? Red : Orange; }

    // ------------------------------------------------------------ cios

    /// <summary>Łuk cięcia: 12 kostek rozstawionych na łuku przed graczem, lecących wzdłuż niego (0,2 s).</summary>
    public static void SwordSlash(Transform player)
    {
        if (player == null) return;
        var fwd = player.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        var center = player.position + Vector3.up * 0.9f;
        const int count = 12;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float angle = Mathf.Lerp(-70f, 70f, t);
            var dir = Quaternion.Euler(0f, angle, 0f) * fwd;
            var tangent = Quaternion.Euler(0f, 90f, 0f) * dir;
            var color = (i % 3 == 0) ? HotWhite : Orange;
            VoxelFx.Emit(center + dir * Random.Range(1.3f, 1.7f) + Vector3.up * Random.Range(-0.1f, 0.15f),
                tangent * Random.Range(3f, 5f) + dir * 0.8f, color, Random.Range(0.12f, 0.22f), 0.2f, false, false, 500f);
        }
    }

    /// <summary>Trafienie ostrzem: mały błysk plus iskry z kostek.</summary>
    public static void SwordHit(Vector3 pos)
    {
        VoxelFx.Flash(pos, HotWhite, 0.45f);
        VoxelFx.Burst(pos, Orange, 10, 0.1f, 6f, 0.3f, 1f);
        VoxelFx.Burst(pos, HotWhite, 4, 0.08f, 7f, 0.22f, 0f);
    }

    // ------------------------------------------------------------ wir

    public static void SpinStart(Transform origin, float range, float duration, bool evolved)
    {
        if (origin == null) return;
        var color = Accent(evolved);
        VoxelFx.GroundGlow(origin, Mathf.Max(1.5f, range), new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f), duration);
        VoxelOrbit.Attach(origin, evolved ? Red : Gold, 8, Mathf.Clamp(range * 0.45f, 1f, 2f), 0.9f, duration, 0.2f, 1.2f);
        VoxelFx.Flash(origin.position + Vector3.up * 0.9f, HotWhite, 0.8f);
        if (evolved) VoxelFx.Pillar(origin.position, Red, 4f, Mathf.Min(duration, 1.2f));
    }

    /// <summary>Pierścień kostek na każdy tick wiru.</summary>
    public static void SpinTick(Vector3 center, float range, bool evolved)
    {
        VoxelFx.VoxelRing(center + Vector3.up * 0.1f, Mathf.Max(1.5f, range), Accent(evolved), 0.4f);
    }

    // ------------------------------------------------------------ szarża

    public static void ChargeStart(SkillContext ctx, Vector3 start, Vector3 dir, float seconds, bool evolved)
    {
        var color = Accent(evolved);
        VoxelFx.Shards(start + Vector3.up * 0.8f, -dir, color, 14);
        VoxelFx.Flash(start + Vector3.up * 0.9f, HotWhite, 1.0f);
        if (ctx.Origin != null)
            VoxelTrailEmitter.Attach(ctx.Origin.gameObject, color, 0.26f, 0.14f, 0.5f, seconds + 0.05f);
    }

    public static void ChargeEnd(Vector3 pos, bool evolved)
    {
        VoxelFx.VoxelRing(pos + Vector3.up * 0.1f, 2.2f, Accent(evolved), 0.4f);
        VoxelFx.Burst(pos + Vector3.up * 0.3f, Gold, 8, 0.12f, 5f, 0.35f, 1f);
    }

    /// <summary>Kępa ognia na śladzie ewoluowanej szarży (co metr).</summary>
    public static void FireSpot(Vector3 pos)
    {
        VoxelFx.Column(pos, 1.6f, Red, 8);
        VoxelFx.GroundGlow(pos, 0.9f, new Color(1.6f, 0.3f, 0.12f), 1.2f);
    }

    // ------------------------------------------------------------ uderzenie w ziemię

    public static void GroundSlam(Vector3 center, float range, bool evolved)
    {
        var color = Accent(evolved);
        VoxelFx.Flash(center + Vector3.up * 0.5f, HotWhite, 2.2f);
        VoxelFx.VoxelRing(center + Vector3.up * 0.1f, range * 1.2f, color, 0.7f);
        VoxelFx.Burst(center + Vector3.up * 0.2f, Rock, 22, 0.22f, 7f, 0.8f, 1f, true);
        VoxelFx.Burst(center + Vector3.up * 0.2f, new Color(0.5f, 0.4f, 0.3f), 10, 0.16f, 5f, 0.7f, 1f, true);
        VoxelFx.Column(center, 3.5f, Gold, 26);
        VoxelFx.GroundGlow(center, range * 1.1f, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f), 1.5f);
    }

    // ------------------------------------------------------------ rzut toporem

    public static void AxeFlight(GameObject axe, bool evolved)
    {
        VoxelTrailEmitter.Attach(axe, evolved ? Red : Gold, 0.2f, 0.18f, 0.45f);
    }

    public static void AxeHit(Vector3 pos)
    {
        VoxelFx.Flash(pos, Gold, 0.6f);
        VoxelFx.Burst(pos, Orange, 9, 0.12f, 6f, 0.3f, 1f);
    }

    public static void AxeTurn(Vector3 pos)
    {
        VoxelFx.VoxelRing(pos, 1.2f, Gold, 0.3f);
    }

    // ------------------------------------------------------------ krzyk bojowy

    public static void WarCry(MonoBehaviour runner, Transform origin, float range, float duration, bool evolved)
    {
        if (origin == null) return;
        var color = evolved ? Red : Gold;
        var center = origin.position;
        VoxelFx.VoxelRing(center + Vector3.up * 0.1f, range, color, 0.5f);
        if (runner != null) runner.StartCoroutine(DelayedRings(center, range, color));
        VoxelFx.Column(center, 3f, Orange, 26);
        VoxelFx.Flash(center + Vector3.up * 1f, HotWhite, 1.3f);
        VoxelOrbit.Attach(origin, color, 6, 1.4f, 1.1f, Mathf.Max(duration, 0.8f), 0.24f, 0.8f);
        VoxelFx.GroundGlow(origin, range * 0.8f, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f), Mathf.Max(duration, 0.8f));
    }

    private static System.Collections.IEnumerator DelayedRings(Vector3 center, float range, Color color)
    {
        for (int i = 1; i <= 2; i++)
        {
            yield return new WaitForSeconds(0.1f);
            VoxelFx.VoxelRing(center + Vector3.up * 0.1f, range * (1f - i * 0.1f), color, 0.5f);
        }
    }
}
