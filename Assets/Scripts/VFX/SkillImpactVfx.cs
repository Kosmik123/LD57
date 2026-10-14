using DG.Tweening;
using UnityEngine;

/// <summary>
/// Impakt wizualny skilli, budowany w kodzie (bez assetów): łuk ostrza dla wiru (obracający się wycinek pierścienia
/// z gradientem alfa, podpięty pod gracza) i fala uderzeniowa dla uderzenia w ziemię (rozchodzący się pierścień,
/// pęknięcia, pył, wstrząs kamery, hit stop). Materiały z SwordTrail.CreateTransparentMaterial (URP Particles/Unlit).
/// </summary>
public static class SkillImpactVfx
{
    private static Texture2D ringTexture, cracksTexture;
    private static Mesh arcMesh;
    private static ParticleSystem dust;

    // ------------------------------------------------------------ wir

    public static void SpinArc(Transform origin, float radius, float duration, Color color)
    {
        if (origin == null) return;
        var go = new GameObject("Spin Arc");
        go.transform.SetParent(origin, false);
        go.transform.localPosition = Vector3.up * 0.9f;
        go.transform.localScale = new Vector3(radius, 1f, radius);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = ArcMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SwordTrail.CreateTransparentMaterial("Spin Arc (runtime)", Color.white);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var spin = go.AddComponent<RotateAndFade>();
        spin.Init(color, 900f, duration, 0.25f);

        // błysk pierścienia na ziemi przy starcie
        Ring(origin.position + Vector3.up * 0.05f, radius * 0.9f, color, 0.25f, 0.35f);
    }

    /// <summary>Wycinek pierścienia 300° (promień 0,35–1,05) z alfą malejącą ku ogonowi.</summary>
    private static Mesh ArcMesh()
    {
        if (arcMesh != null) return arcMesh;
        const int segments = 28;
        const float span = 300f * Mathf.Deg2Rad;
        var verts = new Vector3[(segments + 1) * 2];
        var cols = new Color[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = -t * span;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float inner = Mathf.Lerp(0.55f, 0.35f, t); // ogon węższy
            verts[i * 2] = dir * inner;
            verts[i * 2 + 1] = dir * 1.05f;
            float alpha = Mathf.Pow(1f - t, 1.6f);
            cols[i * 2] = new Color(1f, 1f, 1f, alpha * 0.35f);
            cols[i * 2 + 1] = new Color(1f, 1f, 1f, alpha);
            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);
            if (i < segments)
            {
                int b = i * 2, k = i * 6;
                tris[k] = b; tris[k + 1] = b + 2; tris[k + 2] = b + 1;
                tris[k + 3] = b + 1; tris[k + 4] = b + 2; tris[k + 5] = b + 3;
            }
        }
        arcMesh = new Mesh { name = "SpinArc" };
        arcMesh.vertices = verts; arcMesh.colors = cols; arcMesh.uv = uvs; arcMesh.triangles = tris;
        arcMesh.RecalculateNormals();
        arcMesh.RecalculateBounds();
        return arcMesh;
    }

    // ------------------------------------------------------------ uderzenie w ziemię

    public static void Shockwave(Vector3 position, float radius, Color color)
    {
        var ground = position + Vector3.up * 0.06f;
        Ring(ground, radius * 1.25f, color, 0.35f, 0.5f);
        Ring(ground, radius * 0.6f, Color.Lerp(color, Color.white, 0.6f), 0.2f, 0.3f);
        Cracks(ground, radius, 1.6f);
        Dust(position, radius, 28);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.3f);
        HitStop.Freeze(0.08f);
    }

    /// <summary>Pierścień na ziemi rosnący do średnicy 2·radius i gasnący.</summary>
    private static void Ring(Vector3 position, float radius, Color color, float seconds, float startAlpha)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Shockwave Ring";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.3f;
        var mr = go.GetComponent<MeshRenderer>();
        var mat = SwordTrail.CreateTransparentMaterial("Shockwave Ring (runtime)", Color.white);
        mat.mainTexture = RingTexture();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fade = go.AddComponent<RotateAndFade>();
        fade.Init(new Color(color.r, color.g, color.b, startAlpha), 0f, seconds, seconds);
        go.transform.DOScale(Vector3.one * radius * 2f, seconds).SetEase(Ease.OutCubic);
    }

    private static void Cracks(Vector3 position, float radius, float seconds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Slam Cracks";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position - Vector3.up * 0.02f;
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * radius * 1.6f;
        var mr = go.GetComponent<MeshRenderer>();
        var mat = SwordTrail.CreateTransparentMaterial("Slam Cracks (runtime)", Color.white);
        mat.mainTexture = CracksTexture();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fade = go.AddComponent<RotateAndFade>();
        fade.Init(new Color(0.08f, 0.05f, 0.03f, 0.9f), 0f, seconds, seconds * 0.6f);
    }

    private static void Dust(Vector3 position, float radius, int count)
    {
        var ps = DustSystem();
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            var a = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            ep.position = position + dir * 0.3f + Vector3.up * 0.1f;
            ep.velocity = dir * Random.Range(radius * 1.2f, radius * 2.4f) + Vector3.up * Random.Range(0.5f, 2f);
            ep.startSize = Random.Range(0.25f, 0.55f);
            ep.startLifetime = Random.Range(0.4f, 0.8f);
            ep.startColor = new Color(0.55f, 0.45f, 0.35f, 0.8f);
            ps.Emit(ep, 1);
        }
    }

    private static ParticleSystem DustSystem()
    {
        if (dust != null) return dust;
        var go = new GameObject("Slam Dust");
        dust = go.AddComponent<ParticleSystem>();
        var main = dust.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f; main.startSpeed = 0f;
        var em = dust.emission; em.enabled = false;
        var sh = dust.shape; sh.enabled = false;
        var col = dust.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = dust.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        var lim = dust.limitVelocityOverLifetime; lim.enabled = true; lim.dampen = 0.4f;
        var r = dust.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SwordTrail.CreateTransparentMaterial("Slam Dust (runtime)", Color.white);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return dust;
    }

    // ------------------------------------------------------------ tekstury rysowane w kodzie

    private static Texture2D RingTexture()
    {
        if (ringTexture != null) return ringTexture;
        const int n = 128;
        ringTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ring" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 w środku, 1 na krawędzi
                float band = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.82f) / 0.14f);
                float inner = Mathf.Clamp01((r - 0.3f) / 0.5f) * 0.25f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Max(band * band, inner) * (r < 0.98f ? 1f : 0f));
            }
        ringTexture.SetPixels(px); ringTexture.Apply();
        return ringTexture;
    }

    private static Texture2D CracksTexture()
    {
        if (cracksTexture != null) return cracksTexture;
        const int n = 160;
        cracksTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Cracks" };
        var px = new Color[n * n];
        var rng = new System.Random(57);
        // promieniste, łamane szczeliny od środka, z bocznymi odnogami
        for (int line = 0; line < 9; line++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float x = n / 2f, y = n / 2f;
            float len = n * (0.3f + 0.2f * (float)rng.NextDouble());
            for (float d = 0; d < len; d += 1f)
            {
                a += (float)(rng.NextDouble() - 0.5) * 0.35f;
                x += Mathf.Cos(a); y += Mathf.Sin(a);
                float w = Mathf.Lerp(2.2f, 0.6f, d / len);
                Plot(px, n, x, y, w, 1f - d / len * 0.5f);
                if (rng.NextDouble() < 0.04 && d > 8f)
                {
                    float ba = a + (float)(rng.NextDouble() - 0.5) * 1.6f; float bx = x, by = y;
                    for (int k = 0; k < 10 + rng.Next(12); k++) { bx += Mathf.Cos(ba); by += Mathf.Sin(ba); Plot(px, n, bx, by, 0.8f, 0.6f); }
                }
            }
        }
        cracksTexture.SetPixels(px); cracksTexture.Apply();
        return cracksTexture;
    }

    private static void Plot(Color[] px, int n, float cx, float cy, float w, float alpha)
    {
        int r = Mathf.CeilToInt(w);
        for (int y = (int)cy - r; y <= (int)cy + r; y++)
            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                float dd = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(1f - dd / w) * alpha;
                int i = y * n + x;
                if (a > px[i].a) px[i] = new Color(1f, 1f, 1f, a);
            }
    }
}

/// <summary>Obrót wokół Y i wygaszanie alfy (MaterialPropertyBlock _BaseColor), potem Destroy.</summary>
public class RotateAndFade : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Color color;
    private float speed, life, fadeSeconds, elapsed;
    private Renderer rend;
    private MaterialPropertyBlock block;

    public void Init(Color tint, float degreesPerSecond, float lifetime, float fade)
    {
        color = tint; speed = degreesPerSecond; life = lifetime; fadeSeconds = Mathf.Max(0.01f, fade);
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        Apply(1f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (speed != 0f) transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
        float remaining = life - elapsed;
        Apply(Mathf.Clamp01(remaining / fadeSeconds));
        if (remaining <= 0f) Destroy(gameObject);
    }

    private void Apply(float alphaMul)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, color.a * alphaMul));
        block.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * alphaMul));
        rend.SetPropertyBlock(block);
    }
}
