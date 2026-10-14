using UnityEngine;

/// <summary>Smuga miecza podczas ataku i iskry w miejscu trafienia. Komponent na graczu.</summary>
public class SwordTrail : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField, Tooltip("Obiekt miecza (SWORD), na którym powstaje TrailRenderer.")]
    private Transform sword;
    [SerializeField] private DamagingBlade blade;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private float attackDuration = 0.3f;

    private TrailRenderer trail;
    private TrailRenderer core;
    private ParticleSystem sparks;
    private float emitUntil = -1f;

    /// <summary>Wspólny materiał URP Particles/Unlit w trybie Transparent (obsługuje kolor wierzchołków).</summary>
    public static Material CreateTransparentMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(shader) { name = name };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", color);
        return m;
    }

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (sword == null) sword = FindDeep(transform, "SWORD");
        if (sword == null) { enabled = false; return; }
        if (blade == null) blade = sword.GetComponentInChildren<DamagingBlade>(true);
        if (trailMaterial == null) trailMaterial = CreateTransparentMaterial("SwordTrail (runtime)", Color.white);

        trail = sword.GetComponent<TrailRenderer>();
        if (trail == null) trail = sword.gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
        trail.sharedMaterial = trailMaterial;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.5f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.minVertexDistance = 0.05f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;

        // Jaśniejszy, cieńszy rdzeń na osobnym obiekcie (jeden TrailRenderer na GameObject).
        var coreTf = sword.Find("Trail Core");
        if (coreTf == null)
        {
            var cgo = new GameObject("Trail Core");
            cgo.transform.SetParent(sword, false);
            coreTf = cgo.transform;
        }
        core = coreTf.GetComponent<TrailRenderer>();
        if (core == null) core = coreTf.gameObject.AddComponent<TrailRenderer>();
        core.time = 0.22f;
        core.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0f));
        core.sharedMaterial = trailMaterial;
        var cg = new Gradient();
        cg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        core.colorGradient = cg;
        core.minVertexDistance = 0.05f;
        core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.emitting = false;

        CreateSparks();
    }

    private void CreateSparks()
    {
        var go = new GameObject("Hit Sparks");
        go.transform.SetParent(null);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.1f), new Color(1f, 0.85f, 0.4f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = sparks.emission;
        emission.enabled = false;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        var fade = sparks.colorOverLifetime;
        fade.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gr;
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = trailMaterial;
    }

    private void OnEnable()
    {
        if (player != null) player.OnAttack += Player_OnAttack;
        if (blade != null) blade.OnHit += Blade_OnHit;
    }

    private void OnDisable()
    {
        if (player != null) player.OnAttack -= Player_OnAttack;
        if (blade != null) blade.OnHit -= Blade_OnHit;
    }

    private void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }

    private void Player_OnAttack()
    {
        if (trail == null) return;
        trail.Clear();
        trail.emitting = true;
        if (core != null) { core.Clear(); core.emitting = true; }
        emitUntil = Time.time + attackDuration;
    }

    private void Blade_OnHit(Vector3 position)
    {
        if (sparks != null)
        {
            sparks.transform.position = position;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 16; i++)
            {
                ep.startColor = i < 10 ? new Color(1f, 0.55f, 0.1f) : Color.white;
                sparks.Emit(ep, 1);
            }
        }
        if (CameraShake.Instance != null)
            CameraShake.Instance.ShakeScaled(2);
    }

    private void Update()
    {
        if (trail != null && trail.emitting && Time.time >= emitUntil)
        {
            trail.emitting = false;
            if (core != null) core.emitting = false;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
