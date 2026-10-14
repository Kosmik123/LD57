using UnityEngine;

/// <summary>
/// Rozbryzg przy trafieniu wroga: kilka kostek (VoxelFx) w kolorze jego bryły, z grawitacją, plus dwie jasne HDR.
/// </summary>
public static class HitSplash
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static void Burst(Vector3 position, Color color, int count = 10, float strength = 1f)
    {
        // kostki w kolorze wroga (lit, opadają) + 2 jasne HDR
        var body = new Color(color.r, color.g, color.b, 1f);
        for (int i = 0; i < count; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.3f;
            VoxelFx.Emit(position + dir * 0.15f, dir.normalized * Random.Range(2.5f, 5f) * strength, body,
                Random.Range(0.1f, 0.22f) * strength, Random.Range(0.3f, 0.55f), true, true);
        }
        var glow = new Color(color.r * 2.5f + 0.4f, color.g * 2.5f + 0.4f, color.b * 2.5f + 0.4f);
        for (int i = 0; i < 2; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y);
            VoxelFx.Emit(position + dir * 0.1f, dir.normalized * Random.Range(3f, 5.5f) * strength, glow,
                Random.Range(0.07f, 0.12f) * strength, 0.3f, false, false);
        }
    }

    /// <summary>Kolor pierwszego renderera bryły (URP _BaseColor) albo zieleń slime'a.</summary>
    public static Color ColorOf(GameObject enemy)
    {
        foreach (var r in enemy.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            var m = r.sharedMaterial;
            if (m == null) continue;
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty("_Color")) return m.color;
        }
        return new Color(0.4f, 0.9f, 0.3f);
    }
}
