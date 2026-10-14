using DG.Tweening;
using UnityEngine;

/// <summary>
/// Biblioteka efektów stylizowanych low poly (API zostało pod nazwami VoxelFx, bo korzysta z nich mag), budowana w całości w kodzie. Jeden współdzielony ParticleSystem na
/// rodzaj (renderMode Mesh, sześcian, alignment World, losowy obrót 3D), emisja wyłącznie przez Emit(EmitParams).
/// Kolory HDR: EmitParams.startColor to Color32 (0-1), więc jasność niesie materiał (mnożnik 1 / 2,5 / 5);
/// funkcja wybiera poziom po największej składowej koloru i normalizuje resztę. Bloom świeci od HDR powyżej 1.
/// Kolory "lit" (ciemne odłamki) idą przez URP Particles/Lit bez świecenia.
/// Grawitacja: dwa warianty systemów (0 i 1,6), parametr gravity większy od 0 wybiera drugi.
/// </summary>
public static class VoxelFx
{
    private const int LevelCount = 3;
    private static readonly float[] LevelIntensity = { 1f, 2.5f, 5f };

    // indeks: rodzaj (0 = lit, 1..3 = glow poziom 0..2) * 2 + (grawitacja ? 1 : 0)
    private static readonly ParticleSystem[] systems = new ParticleSystem[(1 + LevelCount) * 2];
    private static readonly Material[] glowMaterials = new Material[LevelCount];
    private static Material litMaterial, additiveMaterial;

    private static Texture2D softTexture;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock block;

    // ------------------------------------------------------------ rdzeń: emisja pojedynczej kostki

    /// <summary>Emituje jedną kostkę. color może być HDR (jasność powyżej 1 = świeci); lit = ciemny odłamek bez świecenia.</summary>
    public static void Emit(Vector3 position, Vector3 velocity, Color color, float size, float life, bool gravity = false, bool lit = false, float spin = 360f)
    {
        int kind = 0;
        Color c = color;
        if (!lit)
        {
            int level = LevelOf(color, out c);
            kind = 1 + level;
        }
        var ps = GetSystem(kind, gravity);
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = position;
        ep.velocity = velocity;
        ep.startSize = size;
        ep.startLifetime = Mathf.Max(0.05f, life);
        ep.startColor = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);
        ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
        ep.angularVelocity3D = new Vector3(Random.Range(-spin, spin), Random.Range(-spin, spin), Random.Range(-spin, spin));
        ps.Emit(ep, 1);
    }

    /// <summary>Wybiera poziom jasności materiału (0/1/2) i zwraca kolor znormalizowany do 0-1.</summary>
    private static int LevelOf(Color color, out Color normalized)
    {
        float m = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        int level = m < 1.6f ? 0 : (m < 3.5f ? 1 : 2);
        float k = 1f / LevelIntensity[level];
        normalized = new Color(color.r * k, color.g * k, color.b * k, 1f);
        return level;
    }

    // ------------------------------------------------------------ prymitywy

    /// <summary>Wybuch kostek we wszystkie strony (z przewagą ku górze). gravity większe od 0 = opadają.</summary>
    public static void Burst(Vector3 pos, Color color, int count = 12, float size = 0.15f, float speed = 5f, float life = 0.45f, float gravity = 0f, bool lit = false)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            Emit(pos + dir * 0.1f, dir.normalized * speed * Random.Range(0.5f, 1f), color,
                size * Random.Range(0.6f, 1.2f), life * Random.Range(0.7f, 1.1f), gravity > 0f, lit);
        }
    }

    /// <summary>Kostki na okręgu o promieniu radius, lecące na zewnątrz po ziemi (lekko w górę).</summary>
    public static void Ring(Vector3 center, float radius, Color color, int count = 28, float size = 0.15f, float speed = 4f, float life = 0.45f)
    {
        float offset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * Mathf.PI * 2f / count;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(center + dir * radius, dir * speed * Random.Range(0.85f, 1.15f) + Vector3.up * Random.Range(0.1f, 0.8f),
                color, size * Random.Range(0.8f, 1.2f), life);
        }
    }

    /// <summary>Helisa kostek wznosząca się nad center (2 obroty) z lekkim ruchem stycznym.</summary>
    public static void Spiral(Vector3 center, float radius, float height, Color color, int count = 30)
    {
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float a = t * Mathf.PI * 4f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var tangent = new Vector3(-dir.z, 0f, dir.x);
            Emit(center + dir * radius + Vector3.up * (t * height * 0.5f),
                Vector3.up * Random.Range(1.5f, 3f) + tangent * 1.5f, color, Random.Range(0.1f, 0.2f), Random.Range(0.4f, 0.7f));
        }
    }

    /// <summary>Kostki lecące w górę z kręgu przy ziemi (iskry kolumny).</summary>
    public static void Column(Vector3 pos, float height, Color color, int count = 24)
    {
        for (int i = 0; i < count; i++)
        {
            var off = Random.insideUnitCircle * 0.5f;
            float life = Random.Range(0.45f, 0.8f);
            Emit(pos + new Vector3(off.x, 0.05f, off.y), Vector3.up * (height / life) * Random.Range(0.6f, 1f)
                 + new Vector3(off.x, 0f, off.y) * 0.5f, color, Random.Range(0.08f, 0.2f), life);
        }
    }

    /// <summary>Odłamki w stożku (kąt ok. 35 stopni) wokół dir, opadające. lit = ciemna skała.</summary>
    public static void Shards(Vector3 pos, Vector3 dir, Color color, int count = 12, bool lit = false, float speed = 7f)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
        var rot = Quaternion.LookRotation(dir.normalized);
        for (int i = 0; i < count; i++)
        {
            var cone = Random.insideUnitCircle * Mathf.Tan(35f * Mathf.Deg2Rad);
            var d = (rot * new Vector3(cone.x, cone.y, 1f)).normalized;
            Emit(pos, d * speed * Random.Range(0.5f, 1.1f), color, Random.Range(0.08f, 0.25f), Random.Range(0.4f, 0.8f), true, lit);
        }
    }

    // ------------------------------------------------------------ obiekty (GameObject)

    /// <summary>Błysk: jedna duża kostka HDR 0, size, 0 w 0,12 s plus Point Light 0,15 s.</summary>
    public static void Flash(Vector3 pos, Color color, float size = 1f)
    {
        var go = new GameObject("Voxel Flash");
        go.transform.position = pos;
        var cube = MakeCube(go.transform, Vector3.zero, color, 1f);
        var tf = cube.transform;
        tf.localRotation = Random.rotation;
        tf.localScale = Vector3.zero;
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(go);
        seq.Append(tf.DOScale(Vector3.one * size, 0.04f).SetEase(Ease.OutQuad));
        seq.Append(tf.DOScale(Vector3.zero, 0.08f).SetEase(Ease.InQuad));
        tf.DOLocalRotate(new Vector3(180f, 270f, 90f), 0.12f, RotateMode.LocalAxisAdd).SetUpdate(true).SetLink(go);

        float m = Mathf.Max(0.01f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(color.r / m, color.g / m, color.b / m);
        light.intensity = Mathf.Clamp(size * 6f, 2f, 14f);
        light.range = 3f + size * 4f;
        light.shadows = LightShadows.None;
        DOTween.To(() => light.intensity, v => light.intensity = v, 0f, 0.15f).SetUpdate(true).SetLink(go);
        Object.Destroy(go, 0.25f);
    }

    /// <summary>Pierścień z 24-36 kostek rozchodzący się od 25% do pełnego promienia i gasnący (kostki maleją).</summary>
    public static void VoxelRing(Vector3 pos, float radius, Color color, float seconds = 0.45f)
    {
        seconds = Mathf.Max(0.1f, seconds);
        SoftRing(pos, radius, color, seconds);
        int outer = Mathf.Clamp(Mathf.RoundToInt(radius * 9f), 24, 36);
        float size = Mathf.Clamp(0.12f + radius * 0.03f, 0.14f, 0.3f);
        float r0 = radius * 0.25f;
        float speed = (radius - r0) / seconds;
        float offset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < outer; i++)
        {
            float a = offset + i * Mathf.PI * 2f / outer;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(pos + dir * r0 + Vector3.up * 0.1f, dir * speed * Random.Range(0.95f, 1.05f), color, size * Random.Range(0.85f, 1.15f), seconds);
        }
        // wewnętrzny, mniejszy i jaśniejszy pierścień, trochę wolniejszy
        int inner = outer * 2 / 3;
        var bright = new Color(color.r * 1.4f, color.g * 1.4f, color.b * 1.4f);
        for (int i = 0; i < inner; i++)
        {
            float a = offset + (i + 0.5f) * Mathf.PI * 2f / inner;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(pos + dir * r0 * 0.6f + Vector3.up * 0.15f, dir * speed * 0.7f, bright, size * 0.6f, seconds * 0.9f);
        }
    }

    /// <summary>Słup z kostek wirujących ku górze przez seconds sekund.</summary>
    public static void Pillar(Vector3 pos, Color color, float height = 4f, float seconds = 0.8f)
    {
        var go = new GameObject("Voxel Pillar");
        go.transform.position = pos;
        var d = go.AddComponent<VoxelPillarDriver>();
        d.Init(color, height, seconds);
    }

    /// <summary>Świecący kwadrat na ziemi z miękką teksturą (HDR, puls), widoczny seconds sekund.</summary>
    public static void GroundGlow(Vector3 pos, float radius, Color color, float seconds)
    {
        SpawnGlow(null, pos, radius, color, seconds);
    }

    /// <summary>To samo, ale podąża za follow (np. gracz) w płaszczyźnie XZ, wysokość z jego pozycji.</summary>
    public static void GroundGlow(Transform follow, float radius, Color color, float seconds)
    {
        if (follow == null) return;
        SpawnGlow(follow, follow.position, radius, color, seconds);
    }

    private static void SpawnGlow(Transform follow, Vector3 pos, float radius, Color color, float seconds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Voxel Ground Glow";
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.position = pos + Vector3.up * 0.06f;
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AdditiveMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var d = go.AddComponent<VoxelGlowDriver>();
        d.Init(color, seconds, follow, pos.y + 0.06f);
    }

    /// <summary>Pojedyncza kostka-obiekt z jasnym materiałem i kolorem HDR (do Flash, Orbit).</summary>
    public static GameObject MakeCube(Transform parent, Vector3 localPos, Color color, float size)
    {
        var go = new GameObject("Voxel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = RandomShape();
        var mr = go.AddComponent<MeshRenderer>();
        Color norm;
        int level = LevelOf(color, out norm);
        mr.sharedMaterial = GlowMaterial(level);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        SetTint(mr, norm);
        return go;
    }

    /// <summary>Ustawia kolor (0-1; jasność niesie materiał) na rendererze kostki.</summary>
    public static void SetTint(Renderer r, Color normalized)
    {
        if (block == null) block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(Mathf.Clamp01(normalized.r), Mathf.Clamp01(normalized.g), Mathf.Clamp01(normalized.b), 1f));
        r.SetPropertyBlock(block);
    }

    // ------------------------------------------------------------ systemy, materiały, mesh

    private static ParticleSystem GetSystem(int kind, bool gravity)
    {
        int idx = kind * 2 + (gravity ? 1 : 0);
        var ps = systems[idx];
        if (ps != null) return ps;
        var go = new GameObject("Voxel Fx " + (kind == 0 ? "lit" : "glow" + (kind - 1)) + (gravity ? " grav" : ""));
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity ? 1.6f : 0f;
        main.startSpeed = 0f; main.startLifetime = 0.5f; main.startSize = 0.15f;
        main.startRotation3D = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.65f, 0.85f), new Keyframe(1f, 0f)));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.alignment = ParticleSystemRenderSpace.World;
        r.SetMeshes(Shapes());
        r.sharedMaterial = kind == 0 ? LitMaterial() : GlowMaterial(kind - 1);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        systems[idx] = ps;
        return ps;
    }

    private static Material GlowMaterial(int level)
    {
        level = Mathf.Clamp(level, 0, LevelCount - 1);
        if (glowMaterials[level] != null) return glowMaterials[level];
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(shader) { name = "Voxel Glow " + level + " (runtime)" };
        float i = LevelIntensity[level];
        m.SetColor("_BaseColor", new Color(i, i, i, 1f));
        glowMaterials[level] = m;
        return m;
    }

    private static Material LitMaterial()
    {
        if (litMaterial != null) return litMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        litMaterial = new Material(shader) { name = "Voxel Lit (runtime)" };
        litMaterial.SetColor("_BaseColor", Color.white);
        litMaterial.SetFloat("_Smoothness", 0.1f);
        return litMaterial;
    }

    private static Material AdditiveMaterial()
    {
        if (additiveMaterial != null) return additiveMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(shader) { name = "Voxel Additive (runtime)" };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", SoftTexture());
        m.mainTexture = SoftTexture();
        additiveMaterial = m;
        return m;
    }

    private static Texture2D SoftTexture()
    {
        if (softTexture != null) return softTexture;
        const int n = 64;
        softTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Voxel Soft" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float body = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f) * 0.7f;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.07f) * 0.6f;
                float a = Mathf.Clamp01(body + rim) * (r < 0.98f ? 1f : 0f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        softTexture.SetPixels(px); softTexture.Apply();
        return softTexture;
    }

    // ------------------------------------------------------------ fabryka siatek: low poly, flat-shaded

    private static Mesh[] shapes;

    /// <summary>Zestaw bryl low poly (tetraedr, oktaedr, ikosfera 1 subdiv, wydłużony kryształ), losowo mieszany w systemach.</summary>
    public static Mesh[] Shapes()
    {
        if (shapes != null && shapes[0] != null) return shapes;
        shapes = new[] { Tetrahedron(), Octahedron(), IcoSphere(), Crystal(), Crystal() };
        return shapes;
    }

    private static Mesh RandomShape()
    {
        var s = Shapes();
        return s[Random.Range(0, s.Length)];
    }

    private static Mesh Tetrahedron()
    {
        var v = new[] { new Vector3(1, 1, 1), new Vector3(-1, -1, 1), new Vector3(-1, 1, -1), new Vector3(1, -1, -1) };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.6f;
        return FlatMesh("Poly Tetra", v, new[] { 0, 1, 2, 0, 3, 1, 0, 2, 3, 1, 3, 2 });
    }

    private static Mesh Octahedron()
    {
        var v = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        for (int i = 0; i < v.Length; i++) v[i] *= 0.6f;
        return FlatMesh("Poly Octa", v, new[] { 2, 4, 0, 2, 1, 4, 2, 5, 1, 2, 0, 5, 3, 0, 4, 3, 4, 1, 3, 1, 5, 3, 5, 0 });
    }

    /// <summary>Wydłużony sześciokątny dwustożek (cienki kryształ/odłamek).</summary>
    private static Mesh Crystal()
    {
        const int n = 6;
        var v = new Vector3[n + 2];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            v[i] = new Vector3(Mathf.Cos(a) * 0.22f, -0.15f, Mathf.Sin(a) * 0.22f);
        }
        v[n] = new Vector3(0f, 0.75f, 0f);
        v[n + 1] = new Vector3(0f, -0.45f, 0f);
        var t = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            int a = i, b = (i + 1) % n;
            t[i * 6] = a; t[i * 6 + 1] = n; t[i * 6 + 2] = b;
            t[i * 6 + 3] = a; t[i * 6 + 4] = b; t[i * 6 + 5] = n + 1;
        }
        return FlatMesh("Poly Crystal", v, t);
    }

    /// <summary>Ikosfera z jednym podziałem (80 trójkątów), płasko cieniowana.</summary>
    private static Mesh IcoSphere()
    {
        float p = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
            new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
            new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        var faces = new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var tris = new System.Collections.Generic.List<int>();
        for (int f = 0; f < faces.Length; f += 3)
        {
            int a = faces[f], b = faces[f + 1], c = faces[f + 2];
            int ab = verts.Count; verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            int bc = verts.Count; verts.Add(((verts[b] + verts[c]) * 0.5f).normalized);
            int ca = verts.Count; verts.Add(((verts[c] + verts[a]) * 0.5f).normalized);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        var arr = verts.ToArray();
        for (int i = 0; i < arr.Length; i++) arr[i] *= 0.5f;
        return FlatMesh("Poly Ico", arr, tris.ToArray());
    }

    /// <summary>Buduje siatkę z osobnymi wierzchołkami na trójkąt (ostre krawędzie) i tonem ściany w kolorze wierzchołka.</summary>
    private static Mesh FlatMesh(string name, Vector3[] v, int[] t)
    {
        var verts = new Vector3[t.Length]; var norms = new Vector3[t.Length]; var cols = new Color[t.Length]; var idx = new int[t.Length];
        var light = new Vector3(0.4f, 0.8f, 0.3f).normalized;
        for (int i = 0; i < t.Length; i += 3)
        {
            var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, (a + b + c) / 3f) < 0f) { n = -n; var tmp = b; b = c; c = tmp; }
            float tone = 0.5f + 0.5f * Mathf.Clamp01(Vector3.Dot(n, light) * 0.5f + 0.5f);
            verts[i] = a; verts[i + 1] = b; verts[i + 2] = c;
            for (int k = 0; k < 3; k++) { norms[i + k] = n; cols[i + k] = new Color(tone, tone, tone, 1f); idx[i + k] = i + k; }
        }
        var m = new Mesh { name = name };
        m.vertices = verts; m.normals = norms; m.colors = cols; m.triangles = idx;
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Miękki pierścień-quad z gradientem, rosnący z OutCubic i gasnący.</summary>
    private static void SoftRing(Vector3 pos, float radius, Color color, float seconds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Poly Soft Ring";
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.position = pos + Vector3.up * 0.05f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(radius * 0.5f, radius * 0.5f, 1f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AdditiveMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var d = go.AddComponent<VoxelGlowDriver>();
        d.Init(new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f), seconds, null, pos.y + 0.05f);
        go.transform.DOScale(new Vector3(radius * 2.2f, radius * 2.2f, 1f), seconds).SetEase(Ease.OutCubic).SetLink(go);
    }
}

/// <summary>Emituje kostki wzdłuż ruchu obiektu co spacing metrów. Komponent (Attach), opcjonalnie samousuwający się po czasie.</summary>
public class VoxelTrailEmitter : MonoBehaviour
{
    private Color color = new Color(2f, 1.2f, 0.4f);
    private float size = 0.18f, spacing = 0.25f, life = 0.4f, spread = 0.12f;
    private bool emitting = true;
    private Vector3 last;
    private bool hasLast;

    public bool Emitting
    {
        get { return emitting; }
        set { emitting = value; hasLast = false; }
    }

    /// <summary>Dodaje emiter do obiektu. seconds większe od 0 = usuwa sam komponent po tym czasie.</summary>
    public static VoxelTrailEmitter Attach(GameObject target, Color color, float size = 0.18f, float spacing = 0.25f, float life = 0.4f, float seconds = 0f)
    {
        if (target == null) return null;
        var e = target.GetComponent<VoxelTrailEmitter>();
        if (e == null) e = target.AddComponent<VoxelTrailEmitter>();
        e.color = color; e.size = size; e.spacing = Mathf.Max(0.03f, spacing); e.life = life;
        e.emitting = true; e.hasLast = false;
        e.EnsureTrail();
        if (seconds > 0f) Destroy(e, seconds);
        return e;
    }

    private TrailRenderer ribbon;

    private void EnsureTrail()
    {
        if (ribbon == null)
        {
            var child = new GameObject("Poly Ribbon");
            child.transform.SetParent(transform, false);
            ribbon = child.AddComponent<TrailRenderer>();
            ribbon.sharedMaterial = SwordTrail.CreateTransparentMaterial("Poly Ribbon (runtime)", Color.white);
            ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ribbon.minVertexDistance = 0.08f;
        }
        float m = Mathf.Max(0.01f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
        var c = new Color(color.r / m, color.g / m, color.b / m);
        ribbon.time = Mathf.Max(0.15f, life * 0.6f);
        ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, size * 1.2f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.4f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        ribbon.colorGradient = g;
        ribbon.Clear();
        ribbon.emitting = emitting;
    }

    private void OnDestroy()
    {
        if (ribbon != null) Destroy(ribbon.gameObject, ribbon.time);
    }

    private void LateUpdate()
    {
        var pos = transform.position;
        if (ribbon != null) ribbon.emitting = emitting;
        if (!emitting) { hasLast = false; return; }
        if (!hasLast) { last = pos; hasLast = true; Spawn(pos); return; }
        var delta = pos - last;
        float dist = delta.magnitude;
        if (dist < spacing) return;
        int n = Mathf.Min(12, Mathf.FloorToInt(dist / spacing));
        var step = delta / dist * spacing;
        var p = last;
        for (int i = 0; i < n; i++) { p += step; Spawn(p); }
        last = p;
    }

    private void Spawn(Vector3 p)
    {
        var jitter = Random.insideUnitSphere * spread;
        VoxelFx.Emit(p + jitter, Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f, color,
            size * Random.Range(0.6f, 1.2f), life * Random.Range(0.8f, 1.2f), false, false, 240f);
    }
}

/// <summary>N kostek krążących wokół transformu, pulsujących; pojawiają się i znikają płynnie.</summary>
public class VoxelOrbit : MonoBehaviour
{
    private Transform target;
    private Transform[] cubes;
    private float radius, height, seconds, size, turnsPerSecond, elapsed;

    /// <summary>Tworzy orbitę wokół target na seconds sekund. height = wysokość nad pozycją celu.</summary>
    public static VoxelOrbit Attach(Transform target, Color color, int count = 8, float radius = 1.3f, float height = 0.9f, float seconds = 1f, float size = 0.18f, float turnsPerSecond = 1.2f)
    {
        if (target == null) return null;
        var go = new GameObject("Voxel Orbit");
        go.transform.position = target.position;
        var o = go.AddComponent<VoxelOrbit>();
        o.target = target; o.radius = radius; o.height = height; o.seconds = Mathf.Max(0.2f, seconds);
        o.size = size; o.turnsPerSecond = turnsPerSecond;
        o.cubes = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            var c = VoxelFx.MakeCube(go.transform, Vector3.zero, color, size);
            c.transform.localRotation = Random.rotation;
            o.cubes[i] = c.transform;
        }
        o.Place();
        return o;
    }

    private void LateUpdate()
    {
        elapsed += Time.deltaTime;
        if (target == null || elapsed >= seconds) { Destroy(gameObject); return; }
        Place();
    }

    private void Place()
    {
        if (target != null) transform.position = target.position;
        float fadeIn = Mathf.Clamp01(elapsed / 0.15f);
        float fadeOut = Mathf.Clamp01((seconds - elapsed) / 0.2f);
        float env = Mathf.Min(fadeIn, fadeOut);
        int n = cubes.Length;
        for (int i = 0; i < n; i++)
        {
            float a = (elapsed * turnsPerSecond + i / (float)n) * Mathf.PI * 2f;
            float r = radius * (1f + 0.12f * Mathf.Sin(elapsed * 8f + i));
            float y = height + 0.25f * Mathf.Sin(elapsed * 5f + i * 1.7f);
            cubes[i].localPosition = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            cubes[i].localScale = Vector3.one * (size * env * (1f + 0.3f * Mathf.Sin(elapsed * 10f + i * 2.3f)));
            cubes[i].Rotate(120f * Time.deltaTime, 200f * Time.deltaTime, 70f * Time.deltaTime, Space.Self);
        }
    }
}

/// <summary>Sterownik świecącej plamy na ziemi: wejście, puls, wygaszenie; opcjonalnie podąża za transformem.</summary>
public class VoxelGlowDriver : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Color color;
    private float seconds, elapsed, y;
    private Transform follow;
    private Renderer rend;
    private MaterialPropertyBlock block;

    public void Init(Color hdr, float lifetime, Transform followTarget, float height)
    {
        color = hdr; seconds = Mathf.Max(0.1f, lifetime); follow = followTarget; y = height;
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        Apply();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= seconds) { Destroy(gameObject); return; }
        if (follow != null) transform.position = new Vector3(follow.position.x, y, follow.position.z);
        Apply();
    }

    private void Apply()
    {
        if (rend == null) return;
        float env = Mathf.Min(Mathf.Clamp01(elapsed / (seconds * 0.12f)), Mathf.Clamp01((seconds - elapsed) / (seconds * 0.35f)));
        float pulse = 1f + 0.25f * Mathf.Sin(elapsed * 9f);
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(color.r * pulse, color.g * pulse, color.b * pulse, env));
        rend.SetPropertyBlock(block);
    }
}

/// <summary>Emituje wznoszącą się spiralę kostek w osi obiektu przez zadany czas.</summary>
public class VoxelPillarDriver : MonoBehaviour
{
    private Color color;
    private float height, seconds, elapsed, carry;

    public void Init(Color hdr, float pillarHeight, float lifetime)
    {
        color = hdr; height = pillarHeight; seconds = Mathf.Max(0.1f, lifetime);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= seconds) { Destroy(gameObject); return; }
        carry += Time.deltaTime * 100f;
        int n = Mathf.FloorToInt(carry);
        carry -= n;
        var origin = transform.position;
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(0.25f, 0.65f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var tangent = new Vector3(-dir.z, 0f, dir.x);
            float life = Random.Range(0.5f, 0.85f);
            VoxelFx.Emit(origin + dir * r + Vector3.up * 0.05f,
                Vector3.up * (height / life) * Random.Range(0.55f, 1f) + tangent * Random.Range(0.8f, 1.8f) - dir * 0.3f,
                color, Random.Range(0.1f, 0.24f), life);
        }
    }
}
