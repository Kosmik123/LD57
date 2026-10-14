using UnityEngine;

/// <summary>
/// Rozbryzg przy trafieniu wroga: kilka kulek w kolorze jego bryły, z grawitacją. Jeden ParticleSystem na scenę,
/// cząstki emitowane przez Emit(EmitParams) z kolorem per trafienie.
/// </summary>
public static class HitSplash
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static ParticleSystem system;

    public static void Burst(Vector3 position, Color color, int count = 10, float strength = 1f)
    {
        var ps = System();
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.3f;
            ep.position = position + dir * 0.15f;
            ep.velocity = dir.normalized * Random.Range(2.5f, 5f) * strength;
            ep.startColor = color;
            ep.startSize = Random.Range(0.08f, 0.2f) * strength;
            ep.startLifetime = Random.Range(0.25f, 0.45f);
            ps.Emit(ep, 1);
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

    private static ParticleSystem System()
    {
        if (system != null) return system;
        var go = new GameObject("Hit Splash");
        system = go.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;
        main.startSpeed = 0f; main.startLifetime = 0.4f; main.startSize = 0.15f;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var col = system.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        renderer.sharedMaterial = new Material(shader) { name = "HitSplash (runtime)" };
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return system;
    }
}
