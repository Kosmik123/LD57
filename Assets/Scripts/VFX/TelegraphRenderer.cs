using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implementacja telegrafów: płaskie quady tuż nad podłogą z teksturami rysowanymi w kodzie (bez decali URP).
/// Kontur pokazuje cały obszar, wypełnienie rośnie od 0 do 100 % przez `seconds`, potem błysk i zanik (0,1 s).
/// Czas liczony przez Time.deltaTime, więc pauza (timeScale 0) zatrzymuje telegraf. Obiekty z puli.
/// </summary>
public class TelegraphRenderer : MonoBehaviour, ITelegraphRenderer
{
    private const float FlashTime = 0.08f;
    private const float PulseTime = 0.15f;
    private const float GroundOffset = 0.05f;
    private const int ConeSegments = 24;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField, Tooltip("URP/Unlit (przypisuje skrypt wiringu, żeby shader trafił do builda)")] private Shader unlitShader;
    [SerializeField] private Color outlineColor = new Color(1f, 0.45f, 0.08f, 0.9f);
    [SerializeField] private Color fillColor = new Color(1f, 0.22f, 0.05f, 0.65f);
    [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.55f, 1f);
    [SerializeField] private float lineWidth = 0.7f;
    [SerializeField] private LayerMask groundMask = ~0;

    private sealed class Marker
    {
        public GameObject root;
        public Transform fillT;
        public MeshRenderer outlineR, fillR;
        public MeshFilter outlineF, fillF;
        public Mesh coneMesh;
        public float coneAngle = -1f;
        public float duration, elapsed;
        public Vector3 outlineScale;
        public int kind;               // 0 koło, 1 stożek, 2 linia
    }

    private readonly Stack<Marker> pool = new Stack<Marker>();
    private readonly List<Marker> active = new List<Marker>();
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private MaterialPropertyBlock blockInstance;
    private MaterialPropertyBlock block => blockInstance ??= new MaterialPropertyBlock(); // nie w inicjalizatorze pola: Unity tego zabrania

    private Mesh centeredQuad, forwardQuad;
    private Material matRing, matBorder, matDisc, matPlain;
    private Texture2D texRing, texBorder, texDisc, texPlain;

    private void OnEnable() => Telegraph.Renderer = this;

    private void OnDisable()
    {
        if (Telegraph.Renderer == (ITelegraphRenderer)this) Telegraph.Renderer = null;
        foreach (var m in active) { m.root.SetActive(false); pool.Push(m); }
        active.Clear();
    }

    private void OnDestroy()
    {
        foreach (var o in new Object[] { matRing, matBorder, matDisc, matPlain, texRing, texBorder, texDisc, texPlain, centeredQuad, forwardQuad })
            if (o != null) Destroy(o);
        foreach (var m in pool) if (m.coneMesh != null) Destroy(m.coneMesh);
    }

    // ------------------------------------------------------------ ITelegraphRenderer

    public void Circle(Vector3 center, float radius, float seconds)
    {
        Init();
        var m = Acquire(0, seconds);
        float y = GroundY(center);
        m.root.transform.SetPositionAndRotation(new Vector3(center.x, y, center.z), Quaternion.identity);
        m.outlineF.sharedMesh = m.fillF.sharedMesh = centeredQuad;
        m.outlineR.sharedMaterial = matRing; m.fillR.sharedMaterial = matDisc;
        m.outlineScale = new Vector3(radius * 2f, 1f, radius * 2f);
        Begin(m);
    }

    public void Cone(Vector3 origin, Vector3 direction, float angleDeg, float range, float seconds)
    {
        Init();
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
        var m = Acquire(1, seconds);
        float y = GroundY(origin);
        m.root.transform.SetPositionAndRotation(new Vector3(origin.x, y, origin.z), Quaternion.LookRotation(direction.normalized, Vector3.up));
        if (m.coneMesh == null) m.coneMesh = new Mesh { name = "Telegraph Cone" };
        if (!Mathf.Approximately(m.coneAngle, angleDeg)) { BuildCone(m.coneMesh, angleDeg); m.coneAngle = angleDeg; }
        m.outlineF.sharedMesh = m.fillF.sharedMesh = m.coneMesh;
        m.outlineR.sharedMaterial = matBorder; m.fillR.sharedMaterial = matPlain;
        m.outlineScale = new Vector3(range, 1f, range);
        Begin(m);
    }

    public void Line(Vector3 from, Vector3 to, float seconds)
    {
        Init();
        var d = to - from; d.y = 0f;
        float len = d.magnitude;
        if (len < 0.01f) return;
        var m = Acquire(2, seconds);
        float y = GroundY(from);
        m.root.transform.SetPositionAndRotation(new Vector3(from.x, y, from.z), Quaternion.LookRotation(d / len, Vector3.up));
        m.outlineF.sharedMesh = m.fillF.sharedMesh = forwardQuad;
        m.outlineR.sharedMaterial = matBorder; m.fillR.sharedMaterial = matPlain;
        m.outlineScale = new Vector3(lineWidth, 1f, len);
        Begin(m);
    }

    // ------------------------------------------------------------ animacja

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var m = active[i];
            m.elapsed += dt;
            if (m.elapsed >= m.duration + FlashTime)
            {
                m.root.SetActive(false);
                pool.Push(m);
                active.RemoveAt(i);
                continue;
            }
            Apply(m);
        }
    }

    private void Apply(Marker m)
    {
        if (m.elapsed < m.duration)
        {
            float t = Mathf.Clamp01(m.elapsed / m.duration);
            m.outlineR.transform.localScale = m.outlineScale;
            m.fillT.localScale = FillScale(m, t);
            // ostatnie 0,15 s: marker pulsuje jaśniej (alfa x1,5)
            float boost = m.duration - m.elapsed < PulseTime ? 1.25f + 0.25f * Mathf.Sin(m.elapsed * 80f) : 1f;
            var oc = outlineColor; oc.a = Mathf.Clamp01(oc.a * boost);
            var fc = fillColor; fc.a = Mathf.Clamp01(fc.a * boost);
            SetColor(m.outlineR, oc);
            SetColor(m.fillR, fc);
        }
        else
        {
            float f = Mathf.Clamp01((m.elapsed - m.duration) / FlashTime);
            float grow = 1f + 0.1f * f;
            m.outlineR.transform.localScale = m.outlineScale * grow;
            m.fillT.localScale = Vector3.Scale(m.outlineScale, new Vector3(grow, 1f, grow));
            var c = flashColor; c.a = 1f - f;
            SetColor(m.outlineR, c);
            SetColor(m.fillR, c);
        }
    }

    private static Vector3 FillScale(Marker m, float t)
    {
        var s = m.outlineScale;
        // linia rośnie wzdłuż osi; koło i stożek rosną od środka / wierzchołka
        return m.kind == 2 ? new Vector3(s.x, 1f, s.z * t) : new Vector3(s.x * t, 1f, s.z * t);
    }

    private void SetColor(Renderer r, Color c)
    {
        block.SetColor(BaseColorId, c);
        r.SetPropertyBlock(block);
    }

    private void Begin(Marker m)
    {
        m.root.SetActive(true);
        active.Add(m);
        Apply(m);
    }

    // ------------------------------------------------------------ pula

    private Marker Acquire(int kind, float seconds)
    {
        var m = pool.Count > 0 ? pool.Pop() : Create();
        m.kind = kind;
        m.duration = Mathf.Max(0.01f, seconds);
        m.elapsed = 0f;
        return m;
    }

    private Marker Create()
    {
        var m = new Marker();
        m.root = new GameObject("Telegraph");
        m.root.transform.SetParent(transform, false);
        var outline = new GameObject("Outline");
        outline.transform.SetParent(m.root.transform, false);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(m.root.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        m.fillT = fill.transform;
        m.outlineF = outline.AddComponent<MeshFilter>(); m.outlineR = outline.AddComponent<MeshRenderer>();
        m.fillF = fill.AddComponent<MeshFilter>(); m.fillR = fill.AddComponent<MeshRenderer>();
        foreach (var r in new[] { m.outlineR, m.fillR })
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        m.root.SetActive(false);
        return m;
    }

    private float GroundY(Vector3 p)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.5f, Vector3.down, hits, 6f, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = p.y;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.rigidbody != null || h.distance >= best) continue; // pomijamy wrogów i gracza
            best = h.distance; y = h.point.y;
        }
        return y + GroundOffset;
    }

    // ------------------------------------------------------------ zasoby generowane w kodzie

    private void Init()
    {
        if (centeredQuad != null) return;
        centeredQuad = BuildQuad(-0.5f, 0.5f, "Telegraph Quad Centered");
        forwardQuad = BuildQuad(0f, 1f, "Telegraph Quad Forward");
        texRing = MakeTexture(128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            if (d > 1f) return 0f;
            if (d > 0.9f) return 0.95f * Mathf.Clamp01((1f - d) / 0.03f);
            return 0.2f;
        });
        texBorder = MakeTexture(64, (u, v) =>
        {
            float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            return e < 0.04f ? 0.95f : 0.2f;
        });
        texDisc = MakeTexture(128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            return d >= 1f ? 0f : Mathf.Clamp01((1f - d) / 0.04f) * (0.7f + 0.3f * d);
        });
        texPlain = MakeTexture(8, (u, v) => 0.75f);
        matRing = MakeMaterial(texRing, 3000);
        matBorder = MakeMaterial(texBorder, 3000);
        matDisc = MakeMaterial(texDisc, 3001);
        matPlain = MakeMaterial(texPlain, 3001);
    }

    private Material MakeMaterial(Texture2D tex, int queue)
    {
        var shader = unlitShader != null ? unlitShader : Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader) { name = "Telegraph" };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = queue;
        return mat;
    }

    private static Texture2D MakeTexture(int size, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = (byte)(Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // quad leżący w XZ, normalna w górę, uv 0..1; z od z0 do z1, x -0,5..0,5
    private static Mesh BuildQuad(float z0, float z1, string name)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-0.5f, 0, z0), new Vector3(-0.5f, 0, z1), new Vector3(0.5f, 0, z1), new Vector3(0.5f, 0, z0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // wycinek o promieniu 1, wierzchołek w (0,0,0), kierunek +Z; u = odległość od wierzchołka, v = pozycja kątowa
    private static void BuildCone(Mesh mesh, float angleDeg)
    {
        int n = ConeSegments;
        var verts = new Vector3[(n + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[n * 6];
        float half = angleDeg * 0.5f * Mathf.Deg2Rad;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float a = Mathf.Lerp(-half, half, t);
            verts[i * 2] = Vector3.zero;
            uvs[i * 2] = new Vector2(0f, t);
            verts[i * 2 + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            uvs[i * 2 + 1] = new Vector2(1f, t);
        }
        for (int i = 0; i < n; i++)
        {
            int k = i * 6, a = i * 2;
            tris[k] = a; tris[k + 1] = a + 1; tris[k + 2] = a + 3;
            tris[k + 3] = a; tris[k + 4] = a + 3; tris[k + 5] = a + 2;
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
