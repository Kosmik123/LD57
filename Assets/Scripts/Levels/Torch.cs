using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pochodnia na ścianie jaskini. Obiekt ma Point Light (ciepły) z LightFlicker i uchwyt (cylinder) z P2Wiring_Ambience;
/// płomień (ParticleSystem: iskry/żar lecące w górę) powstaje tutaj, w kodzie, więc prefab poziomu go nie zawiera.
/// Czysto wizualne: bez colliderów.
/// </summary>
public class Torch : MonoBehaviour
{
    [SerializeField, Tooltip("Cząstek płomienia na sekundę")] private float particlesPerSecond = 2f;
    [SerializeField, Tooltip("Pozycja płomienia względem pochodni")] private Vector3 flameOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private Color flameColor = new Color(1f, 0.55f, 0.15f, 1f);

    private const string FlameName = "Flame";
    private static Material flameMaterial;
    private static Texture2D softDot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { flameMaterial = null; softDot = null; }

    private void Awake()
    {
        if (transform.Find(FlameName) != null) return;
        BuildFlame();
    }

    private void BuildFlame()
    {
        var go = new GameObject(FlameName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = flameOffset;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emisja wzdłuż +Y świata

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = flameColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;

        var emission = ps.emission;
        emission.rateOverTime = particlesPerSecond;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = FlameMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play(true);
    }

    private static Material FlameMaterial()
    {
        if (flameMaterial != null) return flameMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "Torch Flame" };
        var tex = SoftDot();
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }
        return flameMaterial = mat;
    }

    private static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;
        const int N = 32;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                a *= a;
                px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        softDot = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "Torch Soft Dot", wrapMode = TextureWrapMode.Clamp };
        softDot.SetPixels32(px);
        softDot.Apply(false);
        return softDot;
    }
}
