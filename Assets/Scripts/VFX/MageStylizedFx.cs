using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stylizowane efekty low poly dla skilli maga i pociskow. Bryly (tetraedr, oktaedr, low-poly kula, wydluzone
/// krysztaly, torus z 12 segmentow, miekki quad) generowane w kodzie, flat-shaded (zduplikowane wierzcholki,
/// jasnosc scianek zapieczona w kolorze wierzcholka). Cztery wspolne ParticleSystemy (renderMode = Mesh), kolor HDR
/// (mnoznik w materiale Particles/Unlit) swieci od bloomu z LevelAmbience; vertex color to Color32, wiec kolory podajemy w LDR.
/// Tumble = odlamki (oktaedr) z obrotem i grawitacja, Still = krysztaly po osi Z bez grawitacji (blyskawice),
/// StillFall = krysztaly po osi Y z grawitacja (sople), Soft = miekkie quady z gradientem (pierscienie, poswiata).
/// </summary>
public static class MageStylizedFx
{
    public enum Shape { Octa, Tetra, CrystalY, CrystalZ, Sphere, Torus, Quad }
    private enum Kind { Tumble, Still, StillFall, Soft, Smoke }

    private const float GlowIntensity = 3f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly ParticleSystem[] systems = new ParticleSystem[5];
    private static readonly Mesh[] meshes = new Mesh[7];
    private static Material particleMaterial, softMaterial, solidMaterial;
    private static Texture2D softTexture;

    // ------------------------------------------------------------ easing

    public static float EaseOutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    // ------------------------------------------------------------ publiczne helpery

    /// <summary>Wybuch odlamkow (oktaedry) we wszystkie strony, lekka przewaga w gore.</summary>
    public static void Burst(Vector3 pos, Color color, int count = 10, float size = 0.18f, float speed = 5f, float life = 0.5f)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            dir.Normalize();
            float s = size * Random.Range(0.6f, 1.3f);
            Emit(Kind.Tumble, pos + dir * 0.1f, dir * speed * Random.Range(0.5f, 1.1f), Tint(color, 0.3f),
                new Vector3(s, s * Random.Range(0.8f, 1.6f), s), life * Random.Range(0.7f, 1.2f), Random.rotation.eulerAngles);
        }
    }

    /// <summary>Rozchodzacy sie pierscien miekkich blyskow na ziemi.</summary>
    public static void Ring(Vector3 center, float radius, Color color, int count = 24, float life = 0.5f)
    {
        float blob = Mathf.Clamp(radius * 0.32f, 0.35f, 1.1f);
        for (int i = 0; i < count; i++)
        {
            float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float v = radius * 0.85f / life * Random.Range(0.92f, 1.08f);
            Emit(Kind.Soft, center + dir * radius * 0.15f + Vector3.up * 0.1f, dir * v, Tint(color, 0.35f),
                Vector3.one * blob * Random.Range(0.8f, 1.2f), life, Vector3.zero);
        }
    }

    /// <summary>Sople: wydluzone krysztaly wyrastajace z ziemi i opadajace.</summary>
    public static void Spikes(Vector3 center, float radius, Color color, int count = 16)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = radius * Random.Range(0.45f, 1f);
            var pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            float h = Random.Range(0.8f, 1.5f);
            float w = Random.Range(0.22f, 0.36f);
            Emit(Kind.StillFall, pos + Vector3.up * h * 0.4f, new Vector3(0f, Random.Range(3.5f, 6f), 0f), Tint(color, 0.5f),
                new Vector3(w, h, w), Random.Range(0.8f, 1.1f),
                new Vector3(Random.Range(-10f, 10f), Random.value * 360f, Random.Range(-10f, 10f)));
        }
    }

    /// <summary>Duzy jasny blysk (low-poly kula HDR) skalowany z OutBack i Point Light, 0,18 s.</summary>
    public static void Flash(Vector3 pos, Color color, float size = 1.2f, float duration = 0.18f)
    {
        var go = new GameObject("Mage Flash");
        go.transform.position = pos;
        go.AddComponent<MageFlashFx>().Init(color, size, duration);
    }

    /// <summary>Swiecaca plama na ziemi: miekkie blyski unoszace sie z dysku przez `seconds` + swiatlo.</summary>
    public static void GroundGlow(Vector3 pos, float radius, Color color, float seconds, float rise = 0.4f)
    {
        var go = new GameObject("Mage Ground Glow");
        go.transform.position = pos;
        go.AddComponent<MageGroundGlow>().Init(radius, color, seconds, rise);
    }

    /// <summary>Blyskawica: zygzak wydluzonych krysztalow (3 rownolegle) + jasny LineRenderer, migotanie (dwa przebiegi).</summary>
    public static void Bolt(Vector3 a, Vector3 b, Color color, float thickness = 0.3f, float seconds = 0.2f)
    {
        var go = new GameObject("Mage Bolt");
        go.AddComponent<MageBoltFx>().Init(a, b, color, thickness, seconds);
    }

    /// <summary>Odlamki zlatujace sie do punktu (implozja).</summary>
    public static void Implode(Vector3 center, Color color, int count, float radius, float life)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = dir.y * 0.6f;
            dir.Normalize();
            var p = center + dir * radius * Random.Range(0.6f, 1f);
            float s = Random.Range(0.12f, 0.24f);
            Emit(Kind.Tumble, p, (center - p) / life, Tint(color, 0.3f), new Vector3(s, s * 1.5f, s), life,
                Random.rotation.eulerAngles);
        }
    }

    /// <summary>Linia miekkich blyskow gasnacych po `life` sekundach.</summary>
    public static void Line(Vector3 a, Vector3 b, Color color, int count, float life)
    {
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            Emit(Kind.Soft, Vector3.Lerp(a, b, t) + Random.insideUnitSphere * 0.08f, Vector3.zero, Tint(color, 0.4f),
                Vector3.one * Random.Range(0.35f, 0.55f), life, Vector3.zero);
        }
    }

    /// <summary>Orb: 3 warstwy: kula HDR + oktaedr w srodku, 2 torusy w roznych osiach, aura (4 bryly krazace blisko), swiatlo, smuga.
    /// Komponent MageOrbPulse na korzeniu wizualu pozwala "pulsowac" kula przy trafieniu (PulseOrb).</summary>
    public static GameObject OrbVisual(Transform parent, Color color, float size = 0.34f)
    {
        var root = new GameObject("Orb Visual");
        root.transform.SetParent(parent, false);
        var body = new GameObject("Orb Body");
        body.transform.SetParent(root.transform, false);
        root.AddComponent<MageOrbPulse>().target = body.transform;
        MakeSpinShape(body.transform, Shape.Sphere, color, size, 0.9f, 3.2f, new Vector3(40f, 90f, 0f));
        MakeSpinShape(body.transform, Shape.Octa, Color.Lerp(color, Color.white, 0.8f), size * 0.45f, 1f, 4.5f, new Vector3(-200f, 300f, 120f));
        var ring1 = MakeSpinShape(body.transform, Shape.Torus, Color.Lerp(color, Color.white, 0.3f), size * 2.1f, 1f, 3f, new Vector3(0f, 200f, 0f));
        ring1.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        var ring2 = MakeSpinShape(body.transform, Shape.Torus, color, size * 1.7f, 1f, 3f, new Vector3(0f, -260f, 0f));
        ring2.transform.localRotation = Quaternion.Euler(-50f, 0f, 70f);
        // aura: 4 male bryly krazace blisko na pochylonej orbicie
        var aura = new GameObject("Orb Aura");
        aura.transform.SetParent(body.transform, false);
        aura.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);
        aura.AddComponent<MageSpin>().axis = new Vector3(0f, 420f, 0f);
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.PI * 2f;
            var m = MakeShape(aura.transform, "Aura", i % 2 == 0 ? Shape.Octa : Shape.Tetra, Color.Lerp(color, Color.white, 0.5f), size * 0.3f, 0.95f, 3.5f);
            m.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 2f) * 0.2f, Mathf.Sin(a)) * size * 1.15f;
            m.AddComponent<MageSpin>().axis = Random.onUnitSphere * 400f;
        }
        var light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 1.6f;
        light.range = 3f;
        light.shadows = LightShadows.None;
        AddTrail(root, color, size * 0.5f, 0.5f, false);
        return root;
    }

    /// <summary>Puls kuli orba przy trafieniu (skala 1,4 -> 1 w 0,2 s).</summary>
    public static void PulseOrb(Transform orb)
    {
        if (orb == null) return;
        var pulse = orb.GetComponentInChildren<MageOrbPulse>();
        if (pulse != null) pulse.Pulse();
    }

    // ------------------------------------------------------------ rzucanie, ogien, dym, iskry

    /// <summary>Gest rzucania: blysk HDR 0,1 s (+ swiatlo) w dloni (Body + forward*0,4 + up*1,0) i 3 iskry.</summary>
    public static void CastFlash(Transform player, Transform body, Color color)
    {
        if (player == null) return;
        var fwd = player.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        var pos = (body != null ? body.position : player.position) + fwd * 0.4f + Vector3.up * 1f;
        Flash(pos, color, 0.3f, 0.1f);
        for (int i = 0; i < 3; i++) Spark(pos, fwd * 0.5f, color);
    }

    /// <summary>Iskra: mala jasna bryla lecaca w `back` (+ w gore), opadajaca, 0,4-0,7 s.</summary>
    public static void Spark(Vector3 pos, Vector3 back, Color color)
    {
        var vel = back * Random.Range(1.2f, 3.2f) + Vector3.up * Random.Range(1f, 2.5f) + Random.insideUnitSphere * 0.8f;
        float s = Random.Range(0.05f, 0.1f);
        Emit(Kind.Tumble, pos + Random.insideUnitSphere * 0.08f, vel, Tint(color, 0.5f), new Vector3(s, s * 1.4f, s),
            Random.Range(0.4f, 0.7f), Random.rotation.eulerAngles);
    }

    /// <summary>Iskra ognia: od biaozotego do pomaranczowego.</summary>
    public static void FireSpark(Vector3 pos, Vector3 back)
    {
        Spark(pos, back, Color.Lerp(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.45f, 0.1f), Random.value));
    }

    /// <summary>Kleb dymu: ciemnoszara bryla, rosnaca i unoszaca sie, 1,2-1,8 s, alfa gasnie.</summary>
    public static void SmokePuff(Vector3 pos, float rise = 0.6f, float size = 0.36f)
    {
        float g = Random.Range(0.07f, 0.14f);
        Emit(Kind.Smoke, pos + Random.insideUnitSphere * 0.12f,
            Vector3.up * rise * Random.Range(0.7f, 1.4f) + Random.insideUnitSphere * 0.25f,
            new Color(g, g * 0.95f, g * 0.95f, 1f), Vector3.one * size * Random.Range(0.8f, 1.25f),
            Random.Range(1.2f, 1.8f), Random.rotation.eulerAngles);
    }

    /// <summary>Zar na ziemi: miekka, gasnaca plamka (bez swiatla).</summary>
    public static void Ember(Vector3 groundPos, float radius, float seconds)
    {
        EmitSoft(groundPos + Vector3.up * 0.05f, Vector3.zero, new Color(1f, 0.4f, 0.08f), radius * 2f, seconds);
    }

    /// <summary>Spalona plama: ciemna miekka plamka na ziemi, trzyma sie polowe czasu i gasnie.</summary>
    public static void Scorch(Vector3 groundPos, float radius, float seconds = 6f)
    {
        EmitSoft(groundPos + Vector3.up * 0.03f, Vector3.zero, new Color(0.04f, 0.03f, 0.03f), radius * 2.2f, seconds);
    }

    /// <summary>Wola tick(t01) co `interval` s przez `duration` s (pierwszy tick w pierwszej klatce, ostatni przy t = duration).</summary>
    public static void Repeat(float duration, float interval, System.Action<float> tick)
    {
        var go = new GameObject("Mage Repeater");
        go.AddComponent<MageRepeater>().Init(duration, interval, tick);
    }

    /// <summary>Sylwetka rozpada sie: bryly z calej wysokosci postaci rozlatuja sie na boki i w gore.</summary>
    public static void Dissolve(Vector3 feet, Color color, int count)
    {
        var center = feet + Vector3.up * 0.9f;
        for (int i = 0; i < count; i++)
        {
            var disc = Random.insideUnitCircle * 0.3f;
            var p = feet + new Vector3(disc.x, Random.Range(0.1f, 1.7f), disc.y);
            var outward = (p - center); outward.y *= 0.3f;
            var vel = outward.normalized * Random.Range(1f, 2.6f) + Vector3.up * Random.Range(0.4f, 1.6f);
            float s = Random.Range(0.1f, 0.18f);
            Emit(Kind.Tumble, p, vel, Tint(color, 0.3f), new Vector3(s, s * 1.5f, s), Random.Range(0.55f, 0.9f), Random.rotation.eulerAngles);
        }
    }

    /// <summary>Sople rosnace z ziemi (0 -> h w 0,15 s OutBack), stoja 1,2 s, kruszą sie; do tego mgielka mrozu.</summary>
    public static void IceGrowth(Vector3 center, float radius, Color color, int count = 16, int mist = 8)
    {
        var go = new GameObject("Mage Ice Growth");
        go.transform.position = center;
        go.AddComponent<MageIceSpikes>().Init(radius, color, count, mist);
    }

    /// <summary>Elektryzacja: 4 male bryly skaczace wokol celu (domyslnie 0,4 s).</summary>
    public static void Electrify(Transform target, Color color, float seconds = 0.4f)
    {
        if (target == null) return;
        var go = new GameObject("Mage Electrify");
        go.AddComponent<MageElectrify>().Init(target, color, seconds);
    }

    public static MageShardTrail AddTrail(GameObject go, Color color, float size, float life, bool ember)
    {
        var trail = go.GetComponent<MageShardTrail>();
        if (trail == null) trail = go.AddComponent<MageShardTrail>();
        trail.Configure(color, size, life, ember);
        return trail;
    }

    // ------------------------------------------------------------ siatki

    public static Mesh GetMesh(Shape shape)
    {
        int i = (int)shape;
        if (meshes[i] != null) return meshes[i];
        var b = new MeshBuilder();
        switch (shape)
        {
            case Shape.Octa: BuildBipyramid(b, 4, 0.5f, 0f, 0f, 0.5f, -0.5f, false); break;
            case Shape.Tetra: BuildTetra(b); break;
            case Shape.CrystalY: BuildBipyramid(b, 5, 0.5f, 0.25f, -0.2f, 0.5f, -0.5f, false); break;
            case Shape.CrystalZ: BuildBipyramid(b, 5, 0.5f, 0.25f, -0.2f, 0.5f, -0.5f, true); break;
            case Shape.Sphere: BuildIcosphere(b); break;
            case Shape.Torus: BuildTorus(b, 12, 5, 0.5f, 0.07f); break;
            default: BuildQuad(b); break;
        }
        var mesh = b.ToMesh(shape.ToString() + " (low poly)");
        meshes[i] = mesh;
        return mesh;
    }

    private class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> tris = new List<int>();
        private static readonly Vector3 LightDir = new Vector3(0.4f, 0.8f, 0.45f).normalized;

        /// <summary>Plaska sciana; `inside` = punkt wewnatrz bryly (do ustalenia zwrotu normalnej).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) { var t = b; b = c; c = t; n = -n; }
            n.Normalize();
            float shade = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Vector3.Dot(n, LightDir) * 0.5f + 0.5f));
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            for (int k = 0; k < 3; k++) { normals.Add(n); colors.Add(new Color(shade, shade, shade, 1f)); }
            uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Dwuszczytowy ostroslup n-boczny wzdluz Y (lub Z): wierzcholek gorny, pierscien, dolny. Oktaedr to n=4.</summary>
    private static void BuildBipyramid(MeshBuilder b, int sides, float ringRadius, float unused, float ringY, float top, float bottom, bool alongZ)
    {
        Vector3 P(float x, float y, float z) => alongZ ? new Vector3(x, z, y) : new Vector3(x, y, z);
        var apexTop = P(0f, top, 0f);
        var apexBottom = P(0f, bottom, 0f);
        var ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            ring[i] = P(Mathf.Cos(a) * ringRadius, ringY, Mathf.Sin(a) * ringRadius);
        }
        var inside = P(0f, ringY, 0f);
        for (int i = 0; i < sides; i++)
        {
            int n = (i + 1) % sides;
            b.Tri(apexTop, ring[i], ring[n], inside);
            b.Tri(apexBottom, ring[i], ring[n], inside);
        }
    }

    private static void BuildTetra(MeshBuilder b)
    {
        var v = new[] { new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f) };
        b.Tri(v[0], v[1], v[2], Vector3.zero); b.Tri(v[0], v[1], v[3], Vector3.zero);
        b.Tri(v[0], v[2], v[3], Vector3.zero); b.Tri(v[1], v[2], v[3], Vector3.zero);
    }

    private static void BuildIcosphere(MeshBuilder b)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var raw = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < raw.Length; i++) raw[i] = raw[i].normalized * 0.5f;
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        for (int i = 0; i < f.Length; i += 3) b.Tri(raw[f[i]], raw[f[i + 1]], raw[f[i + 2]], Vector3.zero);
    }

    private static void BuildTorus(MeshBuilder b, int segs, int tubeSegs, float major, float minor)
    {
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            var c0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var c1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            var center = (c0 + c1).normalized * major * Mathf.Cos(Mathf.PI / segs);
            for (int j = 0; j < tubeSegs; j++)
            {
                float t0 = j / (float)tubeSegs * Mathf.PI * 2f, t1 = (j + 1) / (float)tubeSegs * Mathf.PI * 2f;
                Vector3 V(Vector3 c, float t) => c * (major + Mathf.Cos(t) * minor) + Vector3.up * Mathf.Sin(t) * minor;
                var p00 = V(c0, t0); var p01 = V(c0, t1); var p10 = V(c1, t0); var p11 = V(c1, t1);
                b.Tri(p00, p10, p11, center); b.Tri(p00, p11, p01, center);
            }
        }
    }

    private static void BuildQuad(MeshBuilder b)
    {
        // poziomy quad z UV 0..1 (dwa trojkaty, normalna w gore)
        var v = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        var uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        int[] idx = { 0, 1, 2, 0, 2, 3 };
        int start = b.verts.Count;
        for (int i = 0; i < 4; i++) { b.verts.Add(v[i]); b.normals.Add(Vector3.up); b.colors.Add(Color.white); b.uvs.Add(uv[i]); }
        foreach (var i in idx) b.tris.Add(start + i);
    }

    // ------------------------------------------------------------ wewnetrzne

    internal static void TrailCube(Vector3 pos, Color color, float size, float life, bool ember)
    {
        var vel = Random.insideUnitSphere * 0.5f;
        if (ember) vel += Vector3.up * Random.Range(0.2f, 0.8f);
        float s = size * Random.Range(0.6f, 1.2f);
        Emit(Kind.Tumble, pos + Random.insideUnitSphere * size * 0.5f, vel, Tint(color, 0.3f),
            new Vector3(s, s * 1.4f, s), life * Random.Range(0.7f, 1.2f), Random.rotation.eulerAngles);
    }

    /// <summary>Krysztal po osi Z (bez obrotu i grawitacji) o dowolnym rozmiarze i rotacji.</summary>
    internal static void EmitStill(Vector3 pos, Vector3 vel, Color color, Vector3 size, float life, Vector3 rotation)
    {
        Emit(Kind.Still, pos, vel, color, size, life, rotation);
    }

    internal static void EmitSoft(Vector3 pos, Vector3 vel, Color color, float size, float life)
    {
        Emit(Kind.Soft, pos, vel, color, Vector3.one * size, life, Vector3.zero);
    }

    private static void Emit(Kind kind, Vector3 pos, Vector3 vel, Color color, Vector3 size, float life, Vector3 rotation)
    {
        var ps = Sys(kind);
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startColor = color,
            startSize3D = size,
            startLifetime = Mathf.Max(0.05f, life),
            rotation3D = rotation,
        };
        ps.Emit(ep, 1);
    }

    private static Color Tint(Color c, float whiten)
    {
        var lerped = Color.Lerp(c, Color.white, Random.value * whiten);
        lerped.a = 1f;
        return lerped;
    }

    /// <summary>Wspolny material dla bryl-obiektow (kolor HDR w MaterialPropertyBlock).</summary>
    internal static Material SolidMaterial()
    {
        if (solidMaterial == null)
            solidMaterial = SwordTrail.CreateTransparentMaterial("Mage Solid (runtime)", Color.white);
        return solidMaterial;
    }

    /// <summary>Bryla-obiekt low poly z kolorem HDR (intensity mnozy RGB). Bez collidera.</summary>
    internal static GameObject MakeShape(Transform parent, string name, Shape shape, Color color, float size, float alpha, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = GetMesh(shape);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SolidMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        SetColor(mr, color, alpha, intensity);
        return go;
    }

    internal static void SetColor(Renderer mr, Color color, float alpha, float intensity)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, new Color(color.r * intensity, color.g * intensity, color.b * intensity, alpha));
        mr.SetPropertyBlock(block);
    }

    private static GameObject MakeSpinShape(Transform parent, Shape shape, Color color, float size, float alpha, float intensity, Vector3 spin)
    {
        var go = MakeShape(parent, "Orb Part", shape, color, size, alpha, intensity);
        go.AddComponent<MageSpin>().axis = spin;
        return go;
    }

    private static Texture2D SoftTexture()
    {
        if (softTexture != null) return softTexture;
        const int n = 64;
        softTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Mage Soft (runtime)", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        softTexture.Apply();
        return softTexture;
    }

    private static ParticleSystem Sys(Kind kind)
    {
        int index = (int)kind;
        if (systems[index] != null) return systems[index];

        var go = new GameObject("Mage Fx " + kind);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = kind == Kind.Tumble ? 0.8f : kind == Kind.StillFall ? 1.3f : kind == Kind.Smoke ? -0.12f : 0f;
        main.startSpeed = 0f; main.startLifetime = 0.5f; main.startSize = 0.15f;
        main.startSize3D = true;
        main.startRotation3D = true;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        if (kind == Kind.Smoke)
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        var curve = kind == Kind.Soft
            ? new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f))
            : kind == Kind.Tumble ? AnimationCurve.Linear(0f, 1f, 1f, 0.2f)
            : kind == Kind.Smoke ? AnimationCurve.Linear(0f, 0.5f, 1f, 1.7f)
            : new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1.05f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        if (kind == Kind.Tumble || kind == Kind.Smoke)
        {
            float rs = kind == Kind.Smoke ? 1.2f : 5f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-rs, rs);
            rot.y = new ParticleSystem.MinMaxCurve(-rs, rs);
            rot.z = new ParticleSystem.MinMaxCurve(-rs, rs);
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetMesh(kind == Kind.Smoke ? Shape.Sphere : kind == Kind.Tumble ? Shape.Octa : kind == Kind.Still ? Shape.CrystalZ : kind == Kind.StillFall ? Shape.CrystalY : Shape.Quad);
        renderer.alignment = ParticleSystemRenderSpace.World;
        if (kind == Kind.Soft)
        {
            if (softMaterial == null)
            {
                softMaterial = SwordTrail.CreateTransparentMaterial("Mage Soft Particles (runtime)", new Color(2.5f, 2.5f, 2.5f, 1f));
                softMaterial.SetTexture(BaseMapId, SoftTexture());
            }
            renderer.sharedMaterial = softMaterial;
        }
        else
        {
            if (particleMaterial == null)
                particleMaterial = SwordTrail.CreateTransparentMaterial("Mage Shard Particles (runtime)", new Color(GlowIntensity, GlowIntensity, GlowIntensity, 1f));
            renderer.sharedMaterial = particleMaterial;
        }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        systems[index] = ps;
        return ps;
    }
}

/// <summary>Emituje odlamki wzdluz ruchu obiektu co 0,15 m.</summary>
public class MageShardTrail : MonoBehaviour
{
    private Color color = Color.white;
    private float size = 0.12f, life = 0.45f;
    private bool ember;
    private const float Spacing = 0.15f;
    private Vector3 last;

    public void Configure(Color newColor, float newSize, float newLife, bool newEmber)
    {
        color = newColor; size = newSize; life = newLife; ember = newEmber;
        last = transform.position;
    }

    public void Restart() { last = transform.position; }

    private void OnEnable() { last = transform.position; }

    private void LateUpdate()
    {
        var p = transform.position;
        var d = p - last;
        float dist = d.magnitude;
        if (dist < Spacing) return;
        if (dist > 8f) { last = p; return; } // teleport
        int n = Mathf.Min(20, (int)(dist / Spacing));
        var step = d / dist * Spacing;
        for (int i = 1; i <= n; i++)
            MageStylizedFx.TrailCube(last + step * i, color, size, life, ember);
        last += step * n;
    }
}

public class MageSpin : MonoBehaviour
{
    public Vector3 axis = new Vector3(0f, 200f, 0f);
    private void Update() { transform.Rotate(axis * Time.deltaTime, Space.Self); }
}

/// <summary>Blysk: low-poly kula 0 - size (OutBack) - 0 (OutCubic) i swiatlo, 0,18 s.</summary>
public class MageFlashFx : MonoBehaviour
{
    private const float RiseShare = 0.35f;
    private float Duration = 0.18f;
    private float size, t;
    private Light glow;
    private Vector3 spin;

    public void Init(Color color, float newSize, float duration)
    {
        size = newSize;
        Duration = Mathf.Max(0.05f, duration);
        spin = Random.onUnitSphere * 300f;
        transform.rotation = Random.rotation;
        gameObject.AddComponent<MeshFilter>().sharedMesh = MageStylizedFx.GetMesh(MageStylizedFx.Shape.Sphere);
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MageStylizedFx.SolidMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        MageStylizedFx.SetColor(mr, Color.Lerp(color, Color.white, 0.35f), 0.9f, 4f);
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = Mathf.Max(3f, size * 5f);
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float u = t / Duration;
        if (u >= 1f) { Destroy(gameObject); return; }
        float k = u < RiseShare ? MageStylizedFx.EaseOutBack(u / RiseShare) : 1f - MageStylizedFx.EaseOutCubic((u - RiseShare) / (1f - RiseShare));
        transform.localScale = Vector3.one * size * Mathf.Max(0f, k);
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        glow.intensity = 3f * Mathf.Clamp01(k);
    }
}

/// <summary>Plama na ziemi z unoszacych sie miekkich blyskow i swiatla.</summary>
public class MageGroundGlow : MonoBehaviour
{
    private float radius, seconds, rise, t, carry;
    private Color color;
    private Light glow;

    public void Init(float newRadius, Color newColor, float newSeconds, float newRise)
    {
        radius = Mathf.Max(0.3f, newRadius);
        color = newColor;
        seconds = Mathf.Max(0.1f, newSeconds);
        rise = newRise;
        var lg = new GameObject("Glow Light");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = Vector3.up * 0.6f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = radius * 2f + 1f;
        glow.shadows = LightShadows.None;
        // duza miekka plama pod spodem na caly czas trwania
        MageStylizedFx.EmitSoft(transform.position + Vector3.up * 0.04f, Vector3.zero, color, radius * 2.2f, seconds);
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (t >= seconds) { Destroy(gameObject); return; }
        float fade = Mathf.Clamp01((seconds - t) / Mathf.Min(0.5f, seconds));
        glow.intensity = 1.8f * fade;
        carry += Mathf.Clamp(radius * radius * 1.6f, 4f, 30f) * fade * Time.deltaTime;
        while (carry >= 1f)
        {
            carry -= 1f;
            var disc = Random.insideUnitCircle * radius;
            var pos = transform.position + new Vector3(disc.x, 0.06f, disc.y);
            MageStylizedFx.EmitSoft(pos, Vector3.up * rise * Random.Range(0.5f, 1.4f),
                Color.Lerp(color, Color.white, Random.value * 0.25f), Random.Range(0.4f, 0.8f), Random.Range(0.5f, 0.85f));
        }
    }
}

/// <summary>Blyskawica z wydluzonych krysztalow + jasny LineRenderer: dwa przebiegi (klatka 0 i 2) dla migotania.</summary>
public class MageBoltFx : MonoBehaviour
{
    private Vector3 a, b;
    private Color color;
    private float thickness, seconds;
    private int frame;
    private LineRenderer line;
    private LineRenderer[] forks;
    private Material lineMaterial;
    private const float MainWidth = 0.25f, ForkWidth = 0.11f;

    public void Init(Vector3 from, Vector3 to, Color newColor, float newThickness, float newSeconds)
    {
        a = from; b = to; color = newColor; thickness = newThickness; seconds = Mathf.Max(0.06f, newSeconds);
        line = gameObject.AddComponent<LineRenderer>();
        lineMaterial = SwordTrail.CreateTransparentMaterial("Mage Bolt Line (runtime)", new Color(3f, 3f, 3f, 1f));
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.widthMultiplier = MainWidth;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.35f, 0.65f, 1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = g;
        forks = new LineRenderer[2];
        for (int i = 0; i < forks.Length; i++)
        {
            var fgo = new GameObject("Fork");
            fgo.transform.SetParent(transform, false);
            var fl = fgo.AddComponent<LineRenderer>();
            fl.sharedMaterial = lineMaterial;
            fl.useWorldSpace = true;
            fl.widthMultiplier = ForkWidth;
            fl.numCornerVertices = 2;
            fl.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fl.colorGradient = g;
            fl.positionCount = 0;
            forks[i] = fl;
        }
        EmitPath(seconds * 0.55f);
    }

    private void Update()
    {
        frame++;
        // migotanie 3 klatki: przygaszona, pelna (nowy zygzak), przygaszona
        float w = frame == 2 ? 1f : frame == 1 ? 0.5f : 0.65f;
        if (line != null) line.widthMultiplier = MainWidth * w;
        if (forks != null) foreach (var f in forks) if (f != null) f.widthMultiplier = ForkWidth * w;
        if (frame == 2) EmitPath(seconds * 0.45f);
        else if (frame > 3) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    private void EmitPath(float life)
    {
        float dist = Vector3.Distance(a, b);
        int n = Mathf.Clamp(Mathf.RoundToInt(dist / 0.45f), 8, 14);
        var axis = (b - a).normalized;
        if (axis.sqrMagnitude < 0.01f) axis = Vector3.forward;
        var side = Vector3.Cross(axis, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();
        var up = Vector3.Cross(side, axis).normalized;
        float third = thickness / 3f;
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)n);
            if (i > 0 && i < n) p += side * Random.Range(-0.32f, 0.32f) + up * Random.Range(-0.22f, 0.22f);
            pts[i] = p;
        }
        if (line != null)
        {
            line.positionCount = pts.Length;
            line.SetPositions(pts);
        }
        // dwie losowe odnogi z punktow zygzaku
        for (int f = 0; f < forks.Length; f++)
        {
            if (forks[f] == null) continue;
            if (n < 4) { forks[f].positionCount = 0; continue; }
            int j = Random.Range(1, n - 1);
            var dirF = (side * Random.Range(-1f, 1f) + up * Random.Range(-0.6f, 0.6f) + axis * 0.5f).normalized;
            int fn = Random.Range(3, 5);
            forks[f].positionCount = fn;
            var fp = pts[j];
            for (int k = 0; k < fn; k++)
            {
                forks[f].SetPosition(k, fp);
                fp += dirF * 0.5f;
                dirF = (dirF + Random.insideUnitSphere * 0.6f).normalized;
            }
        }
        var core = Color.Lerp(color, Color.white, 0.8f);
        for (int i = 0; i < n; i++)
        {
            var seg = pts[i + 1] - pts[i];
            float len = seg.magnitude;
            if (len < 0.01f) continue;
            var q = Quaternion.LookRotation(seg / len, up);
            var rot = q.eulerAngles;
            var mid = (pts[i] + pts[i + 1]) * 0.5f;
            var size = new Vector3(third * 1.3f, third * 1.3f, len * 1.25f); // wydluzone krysztaly po osi segmentu
            var sideVec = q * Vector3.right * third;
            MageStylizedFx.EmitStill(mid, Vector3.zero, core, size, life, rot);
            MageStylizedFx.EmitStill(mid + sideVec, Vector3.zero, color, size, life, rot);
            MageStylizedFx.EmitStill(mid - sideVec, Vector3.zero, color, size, life, rot);
            if (i > 0 && Random.value < 0.35f)
            {
                var dir = (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f) + axis * 0.4f).normalized;
                var p = pts[i];
                for (int k = 0; k < 3; k++)
                {
                    var nd = (dir + Random.insideUnitSphere * 0.5f).normalized;
                    float s = third * (1f - k * 0.2f);
                    MageStylizedFx.EmitStill(p + dir * 0.15f, Vector3.zero, color, new Vector3(s, s, 0.32f),
                        life * (1f - k * 0.2f), Quaternion.LookRotation(dir).eulerAngles);
                    p += dir * 0.3f;
                    dir = nd;
                }
            }
        }
    }
}

/// <summary>Krysztaly przyczepione do wroga (oblodzenie) albo krazace nad jego glowa (ogluszenie). Znikaja po czasie.</summary>
public class MageAttachedCubes : MonoBehaviour
{
    private static readonly Dictionary<(Transform, bool), MageAttachedCubes> active = new Dictionary<(Transform, bool), MageAttachedCubes>();

    private Transform target;
    private Transform[] cubes;
    private Vector3[] local;
    private float life, t, height, radius, size;
    private bool orbit;
    private (Transform, bool) key;

    public static void Attach(Transform target, Color color, int count, float duration, bool orbit, float size)
    {
        if (target == null || duration <= 0f) return;
        var key = (target, orbit);
        if (active.TryGetValue(key, out var old) && old != null) Destroy(old.gameObject);
        var go = new GameObject(orbit ? "Mage Orbit Shards" : "Mage Rime Shards");
        var fx = go.AddComponent<MageAttachedCubes>();
        active[key] = fx;
        fx.Setup(key, color, count, duration, orbit, size);
    }

    private void Setup((Transform, bool) newKey, Color color, int count, float duration, bool isOrbit, float cubeSize)
    {
        key = newKey; target = newKey.Item1; life = duration; orbit = isOrbit; size = cubeSize;
        bool has = false;
        var bounds = new Bounds(target.position, Vector3.zero);
        foreach (var r in target.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!has) bounds = new Bounds(target.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
        var ext = bounds.extents;
        height = bounds.max.y - target.position.y + 0.35f;
        radius = Mathf.Max(0.4f, Mathf.Max(ext.x, ext.z) * 0.8f);
        cubes = new Transform[count];
        local = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var c = Color.Lerp(color, Color.white, Random.value * 0.3f);
            var shape = orbit ? MageStylizedFx.Shape.Octa : MageStylizedFx.Shape.CrystalY;
            cubes[i] = MageStylizedFx.MakeShape(transform, "Shard", shape, c, size, 0.9f, 2.5f).transform;
            var d = Random.onUnitSphere;
            cubes[i].rotation = orbit ? Random.rotation : Quaternion.FromToRotation(Vector3.up, d); // krysztaly sterczą na zewnątrz
            local[i] = target.InverseTransformPoint(bounds.center + Vector3.Scale(d, ext) * 0.95f);
        }
        Place();
    }

    private void Place()
    {
        float k = MageStylizedFx.EaseOutBack(t / 0.18f) * Mathf.Clamp01((life - t) / 0.25f);
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null) continue;
            Vector3 pos;
            if (orbit)
            {
                float a = t * 4.7f + i * Mathf.PI * 2f / cubes.Length;
                pos = target.position + Vector3.up * (height + Mathf.Sin(t * 6f + i) * 0.05f)
                    + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.7f;
                cubes[i].Rotate(new Vector3(60f, 90f, 30f) * Time.deltaTime, Space.Self);
            }
            else
            {
                pos = target.TransformPoint(local[i]);
            }
            cubes[i].position = pos;
            cubes[i].localScale = Vector3.one * size * Mathf.Max(0f, k);
        }
    }

    private void LateUpdate()
    {
        t += Time.deltaTime;
        if (target == null || t >= life) { Destroy(gameObject); return; }
        Place();
    }

    private void OnDestroy()
    {
        if (active.TryGetValue(key, out var cur) && cur == this) active.Remove(key);
    }
}


/// <summary>Wykonuje tick(t01) co `interval` s przez `duration` s i znika.</summary>
public class MageRepeater : MonoBehaviour
{
    private System.Action<float> tick;
    private float duration, interval, t, next;

    public void Init(float newDuration, float newInterval, System.Action<float> action)
    {
        duration = Mathf.Max(0.01f, newDuration);
        interval = Mathf.Max(0.01f, newInterval);
        tick = action;
    }

    private void Update()
    {
        t += Time.deltaTime;
        while (next <= duration + 0.0001f && next <= t)
        {
            if (tick != null) tick(Mathf.Clamp01(next / duration));
            next += interval;
        }
        if (next > duration + 0.0001f) Destroy(gameObject);
    }
}

/// <summary>Puls kuli orba: skala 1,4 -> 1 w 0,2 s.</summary>
public class MageOrbPulse : MonoBehaviour
{
    public Transform target;
    private float left;
    private const float Duration = 0.2f;

    public void Pulse() { left = Duration; if (target != null) target.localScale = Vector3.one * 1.4f; }

    private void Update()
    {
        if (left <= 0f || target == null) return;
        left -= Time.deltaTime;
        target.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, Mathf.Clamp01(left / Duration));
    }
}

/// <summary>
/// Rdzen kuli ognia: 6 brył low-poly od bialozoltego srodka przez pomarancz do czerwieni (obrot + pulsowanie skali,
/// losowe fazy) i 3 "liżące" plomienie wyciagniete do tylu (lokalne -Z, wiec obiekt musi byc obrocony w kierunku lotu).
/// </summary>
public class MageFireCore : MonoBehaviour
{
    private Transform[] parts;
    private Vector3[] spin;
    private float[] phase, freq, baseScale;
    private Transform[] tongues;
    private float[] tonguePhase;
    private Vector3[] tongueScale;
    private float seed;

    public static MageFireCore Create(Transform parent)
    {
        var go = new GameObject("Fire Core");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 1.25f;
        var fc = go.AddComponent<MageFireCore>();
        fc.Build();
        return fc;
    }

    private void Build()
    {
        seed = Random.value * 100f;
        var shapes = new[] { MageStylizedFx.Shape.Octa, MageStylizedFx.Shape.CrystalY, MageStylizedFx.Shape.Tetra,
                             MageStylizedFx.Shape.Octa, MageStylizedFx.Shape.CrystalZ, MageStylizedFx.Shape.Tetra };
        var colors = new[]
        {
            new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.25f), new Color(1f, 0.65f, 0.15f),
            new Color(1f, 0.45f, 0.08f), new Color(1f, 0.3f, 0.06f), new Color(0.9f, 0.12f, 0.04f),
        };
        var intens = new[] { 4.5f, 3.6f, 3.3f, 3f, 2.8f, 2.4f };
        var sizes = new[] { 0.5f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f };
        int n = shapes.Length;
        parts = new Transform[n]; spin = new Vector3[n]; phase = new float[n]; freq = new float[n]; baseScale = new float[n];
        for (int i = 0; i < n; i++)
        {
            float alpha = i < 2 ? 1f : 0.85f;
            var go = MageStylizedFx.MakeShape(transform, "Fire Part", shapes[i], colors[i], sizes[i], alpha, intens[i]);
            go.transform.localPosition = i == 0 ? Vector3.zero : Random.insideUnitSphere * 0.12f;
            go.transform.localRotation = Random.rotation;
            parts[i] = go.transform;
            spin[i] = Random.onUnitSphere * Random.Range(180f, 420f);
            phase[i] = Random.value * 6.28f;
            freq[i] = Random.Range(9f, 16f);
            baseScale[i] = sizes[i];
        }
        tongues = new Transform[3]; tonguePhase = new float[3]; tongueScale = new Vector3[3];
        var tcol = new[] { new Color(1f, 0.55f, 0.1f), new Color(1f, 0.35f, 0.06f), new Color(0.95f, 0.2f, 0.05f) };
        for (int i = 0; i < 3; i++)
        {
            float ang = i * 120f + Random.Range(-20f, 20f);
            var spread = Quaternion.AngleAxis(ang, Vector3.forward) * Vector3.up * 0.35f;
            var dir = new Vector3(spread.x, spread.y, -1f).normalized;
            var go = MageStylizedFx.MakeShape(transform, "Fire Tongue", MageStylizedFx.Shape.CrystalZ, tcol[i], 1f, 0.75f, 3f);
            go.transform.localPosition = dir * 0.85f;
            go.transform.localRotation = Quaternion.LookRotation(dir);
            tongueScale[i] = new Vector3(0.32f, 0.32f, 1.5f - i * 0.2f);
            go.transform.localScale = tongueScale[i];
            tongues[i] = go.transform;
            tonguePhase[i] = Random.value * 6.28f;
        }
    }

    private void OnEnable() { seed = Random.value * 100f; }

    private void Update()
    {
        if (parts == null) return;
        float time = Time.time;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].Rotate(spin[i] * Time.deltaTime, Space.Self);
            parts[i].localScale = Vector3.one * baseScale[i] * (1f + 0.25f * Mathf.Sin(time * freq[i] + phase[i]));
        }
        for (int i = 0; i < tongues.Length; i++)
        {
            float k = 0.65f + 0.7f * Mathf.PerlinNoise(time * 14f + tonguePhase[i], seed);
            var sc = tongueScale[i];
            tongues[i].localScale = new Vector3(sc.x * (0.8f + 0.4f * k), sc.y * (0.8f + 0.4f * k), sc.z * k);
        }
    }
}

/// <summary>Sople lodu: rosna z ziemi, stoja, kruszą sie. Mgielka mrozu: unoszace sie polprzezroczyste bryly.</summary>
public class MageIceSpikes : MonoBehaviour
{
    private const float Grow = 0.15f, Stand = 1.2f, MistLife = 2.2f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Transform[] spikes;
    private float[] height, width;
    private Vector3[] basePos;
    private Transform[] mist;
    private MeshRenderer[] mistRenderers;
    private Vector3[] mistVel;
    private float[] mistSize;
    private Color mistColor, iceColor;
    private float t;
    private bool shattered;
    private MaterialPropertyBlock block;

    public void Init(float radius, Color color, int count, int mistCount)
    {
        iceColor = color;
        mistColor = Color.Lerp(color, Color.white, 0.4f);
        block = new MaterialPropertyBlock();
        spikes = new Transform[count]; height = new float[count]; width = new float[count]; basePos = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = radius * 0.9f * Mathf.Sqrt(Random.Range(0.25f, 1f));
            basePos[i] = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            height[i] = Random.Range(0.8f, 1.5f);
            width[i] = Random.Range(0.22f, 0.36f);
            var c = Color.Lerp(color, Color.white, Random.value * 0.5f);
            var go = MageStylizedFx.MakeShape(transform, "Ice Spike", MageStylizedFx.Shape.CrystalY, c, 1f, 0.95f, 2.2f);
            go.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.value * 360f, Random.Range(-10f, 10f));
            go.transform.localScale = new Vector3(width[i], 0.001f, width[i]);
            go.transform.position = basePos[i];
            spikes[i] = go.transform;
        }
        mist = new Transform[mistCount]; mistRenderers = new MeshRenderer[mistCount]; mistVel = new Vector3[mistCount]; mistSize = new float[mistCount];
        for (int i = 0; i < mistCount; i++)
        {
            var disc = Random.insideUnitCircle * radius * 0.8f;
            var go = MageStylizedFx.MakeShape(transform, "Frost Mist", i % 2 == 0 ? MageStylizedFx.Shape.Sphere : MageStylizedFx.Shape.Octa,
                mistColor, 1f, 0.2f, 1.3f);
            go.transform.position = transform.position + new Vector3(disc.x, Random.Range(0.15f, 0.5f), disc.y);
            go.transform.rotation = Random.rotation;
            mistSize[i] = Random.Range(0.6f, 1.1f);
            go.transform.localScale = Vector3.one * mistSize[i];
            mistVel[i] = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.2f, 0.4f), Random.Range(-0.15f, 0.15f));
            mist[i] = go.transform;
            mistRenderers[i] = go.GetComponent<MeshRenderer>();
        }
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t < Grow ? MageStylizedFx.EaseOutBack(t / Grow) : 1f;
        if (!shattered)
        {
            for (int i = 0; i < spikes.Length; i++)
            {
                if (spikes[i] == null) continue;
                float h = height[i] * Mathf.Max(0.001f, k);
                spikes[i].localScale = new Vector3(width[i], h, width[i]);
                spikes[i].position = basePos[i] + Vector3.up * h * 0.5f;
            }
            if (t >= Grow + Stand)
            {
                shattered = true;
                for (int i = 0; i < spikes.Length; i++)
                {
                    if (spikes[i] == null) continue;
                    var tip = basePos[i] + Vector3.up * height[i] * 0.6f;
                    MageStylizedFx.Burst(tip, iceColor, 3, 0.09f, 2.5f, 0.5f);
                    Destroy(spikes[i].gameObject);
                }
            }
        }
        float fade = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01(1f - t / MistLife);
        for (int i = 0; i < mist.Length; i++)
        {
            if (mist[i] == null) continue;
            mist[i].position += mistVel[i] * Time.deltaTime;
            mist[i].localScale = Vector3.one * mistSize[i] * (1f + t * 0.25f);
            block.SetColor(BaseColorId, new Color(mistColor.r * 1.3f, mistColor.g * 1.3f, mistColor.b * 1.3f, 0.22f * fade));
            mistRenderers[i].SetPropertyBlock(block);
        }
        if (t >= MistLife) Destroy(gameObject);
    }
}

/// <summary>Elektryzacja wroga: 4 male jasne bryly skaczace losowo wokol celu.</summary>
public class MageElectrify : MonoBehaviour
{
    private Transform target;
    private Transform[] bits;
    private float life, t, hop;
    private Vector3 center;
    private float radius, height;

    public void Init(Transform newTarget, Color color, float seconds)
    {
        target = newTarget; life = seconds;
        var bounds = new Bounds(target.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
        bool has = false;
        foreach (var r in target.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
        }
        center = bounds.center - target.position;
        radius = Mathf.Max(0.35f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.9f);
        height = Mathf.Max(0.3f, bounds.extents.y);
        bits = new Transform[4];
        for (int i = 0; i < bits.Length; i++)
        {
            var c = Color.Lerp(color, Color.white, 0.5f + 0.3f * Random.value);
            bits[i] = MageStylizedFx.MakeShape(transform, "Spark Bit", MageStylizedFx.Shape.Octa, c, 0.12f, 1f, 4f).transform;
        }
        Hop();
    }

    private void Hop()
    {
        foreach (var b in bits)
        {
            var d = Random.onUnitSphere;
            b.position = target.position + center + new Vector3(d.x * radius, d.y * height, d.z * radius);
            b.rotation = Random.rotation;
            b.localScale = Vector3.one * Random.Range(0.09f, 0.16f);
        }
    }

    private void LateUpdate()
    {
        t += Time.deltaTime;
        if (target == null || t >= life) { Destroy(gameObject); return; }
        hop += Time.deltaTime;
        if (hop >= 0.06f) { hop = 0f; Hop(); }
    }
}

/// <summary>
/// Lodowy tint wroga na czas spowolnienia przez MaterialPropertyBlock (_BaseColor). Odswieza blok co klatke w LateUpdate
/// (animatory tintu wroga nadpisuja bloki), po czasie zdejmuje blok (SetPropertyBlock(null)).
/// </summary>
public class MageFrostTint : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Renderer[] renderers;
    private Color[] baseColors;
    private Color tint;
    private float until;
    private MaterialPropertyBlock block;

    public static void Apply(GameObject enemy, Color ice, float seconds)
    {
        if (enemy == null || seconds <= 0f) return;
        var fx = enemy.GetComponent<MageFrostTint>();
        if (fx == null) fx = enemy.AddComponent<MageFrostTint>();
        fx.Begin(ice, seconds);
    }

    private void Begin(Color ice, float seconds)
    {
        tint = ice;
        until = Time.time + seconds;
        if (renderers != null) return;
        block = new MaterialPropertyBlock();
        var list = new List<Renderer>();
        var cols = new List<Color>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty(BaseColorId)) continue;
            list.Add(r); cols.Add(m.GetColor(BaseColorId));
        }
        renderers = list.ToArray();
        baseColors = cols.ToArray();
    }

    private void LateUpdate()
    {
        if (renderers == null) { Destroy(this); return; }
        if (Time.time >= until) { Destroy(this); return; } // OnDestroy zdejmuje bloki
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(baseColors[i], tint, 0.6f));
            renderers[i].SetPropertyBlock(block);
        }
    }

    private void OnDestroy()
    {
        if (renderers == null) return;
        foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
    }
}
