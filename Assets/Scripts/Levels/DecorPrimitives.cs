using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fabryki placeholderow dekoracji jaskini z prymitywow (bez colliderow: uzywamy samych siatek prymitywow).
/// Tekstura pajeczyny rysowana w kodzie. Materialy i siatki sa wspolne i tworzone leniwie.
/// </summary>
public static class DecorPrimitives
{
    private static Mesh quadMesh, sphereMesh, cylinderMesh, capsuleMesh, cubeMesh, coneMesh;
    private static Material matWeb, matThread, matCocoon, matNest, matBone, matMetal, matSpider, matRock, matRubble, matFang;
    private static Texture2D webTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        quadMesh = sphereMesh = cylinderMesh = capsuleMesh = cubeMesh = coneMesh = null;
        matWeb = matThread = matCocoon = matNest = matBone = matMetal = matSpider = matRock = matRubble = matFang = null;
        webTexture = null;
    }

    // ------------------------------------------------------------ fabryki

    /// <summary>Dwustronny, polprzezroczysty quad z pajeczyna o boku `size` (m).</summary>
    public static GameObject Web(float size)
    {
        return Part("Web", PrimMesh(PrimitiveType.Quad, ref quadMesh), WebMat(), null, Vector3.zero, Vector3.one * size, false);
    }

    /// <summary>Cienka nitka (cylinder) miedzy dwoma punktami swiata.</summary>
    public static GameObject Thread(Vector3 a, Vector3 b)
    {
        var go = new GameObject("Thread");
        Segment(go.transform, PrimMesh(PrimitiveType.Cylinder, ref cylinderMesh), ThreadMat(), a, b, 0.012f, false);
        return go;
    }

    /// <summary>Wydluzony kokon (kula) z lekko nierownym skalowaniem. Pivot w srodku, os dluga = Y.</summary>
    public static GameObject Cocoon(float size, System.Random rng = null)
    {
        float j(float amp) => rng == null ? 1f : 1f + ((float)rng.NextDouble() * 2f - 1f) * amp;
        var root = new GameObject("Cocoon");
        var sph = PrimMesh(PrimitiveType.Sphere, ref sphereMesh);
        Part("Body", sph, CocoonMat(), root.transform,
            Vector3.zero, new Vector3(0.45f * j(0.12f), 1f * j(0.1f), 0.45f * j(0.12f)) * size, false);
        Part("Bulge", sph, CocoonMat(), root.transform,
            new Vector3(0.1f, -0.15f, 0.05f) * size, new Vector3(0.3f, 0.5f, 0.3f) * size, false);
        return root;
    }

    /// <summary>Gniazdo: ciemnofioletowy worek z nitkami do sufitu (pivot na gorze worka, wisi w dol).</summary>
    public static GameObject Nest(float size)
    {
        var root = new GameObject("Nest");
        var sph = PrimMesh(PrimitiveType.Sphere, ref sphereMesh);
        Part("Sack", sph, NestMat(), root.transform, new Vector3(0f, -0.65f * size, 0f), new Vector3(1f, 1.3f, 1f) * size, false);
        Part("Bump A", sph, NestMat(), root.transform, new Vector3(0.35f, -0.9f, 0.2f) * size, Vector3.one * size * 0.45f, false);
        Part("Bump B", sph, NestMat(), root.transform, new Vector3(-0.3f, -0.5f, -0.3f) * size, Vector3.one * size * 0.4f, false);
        var cyl = PrimMesh(PrimitiveType.Cylinder, ref cylinderMesh);
        for (int i = 0; i < 5; i++)
        {
            float ang = i * 72f * Mathf.Deg2Rad;
            var low = new Vector3(Mathf.Cos(ang) * 0.3f, 0f, Mathf.Sin(ang) * 0.3f) * size;
            var high = new Vector3(Mathf.Cos(ang) * 0.6f, 0.7f, Mathf.Sin(ang) * 0.6f) * size;
            Segment(root.transform, cyl, ThreadMat(), low, high, 0.025f * Mathf.Max(1f, size * 0.5f), false);
        }
        return root;
    }

    /// <summary>Lezacy szkielet (czaszka, kregoslup, zebra, kosci konczyn) z mieczem wbitym obok. Pivot na podlodze.</summary>
    public static GameObject Skeleton()
    {
        var root = new GameObject("Skeleton");
        var t = root.transform;
        var sph = PrimMesh(PrimitiveType.Sphere, ref sphereMesh);
        var cap = PrimMesh(PrimitiveType.Capsule, ref capsuleMesh);
        var box = PrimMesh(PrimitiveType.Cube, ref cubeMesh);
        var m = BoneMat();
        // lezy wzdluz Z: glowa +Z
        Part("Skull", sph, m, t, new Vector3(0f, 0.12f, 0.95f), new Vector3(0.22f, 0.24f, 0.26f), false);
        Part("Jaw", sph, m, t, new Vector3(0f, 0.06f, 1.05f), new Vector3(0.14f, 0.07f, 0.16f), false);
        Segment(t, cap, m, new Vector3(0f, 0.07f, 0.8f), new Vector3(0f, 0.07f, 0.05f), 0.045f, false);
        for (int i = 0; i < 4; i++)
        {
            float z = 0.7f - i * 0.14f;
            float w = 0.2f - i * 0.025f;
            Segment(t, cap, m, new Vector3(-w, 0.05f, z), new Vector3(w, 0.05f, z), 0.025f, false);
        }
        Segment(t, cap, m, new Vector3(-0.2f, 0.06f, 0.72f), new Vector3(-0.55f, 0.05f, 0.5f), 0.03f, false);   // rece
        Segment(t, cap, m, new Vector3(0.2f, 0.06f, 0.72f), new Vector3(0.45f, 0.05f, 1.05f), 0.03f, false);
        Segment(t, cap, m, new Vector3(-0.1f, 0.06f, 0.05f), new Vector3(-0.25f, 0.05f, -0.7f), 0.04f, false);  // nogi
        Segment(t, cap, m, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(0.35f, 0.05f, -0.65f), 0.04f, false);
        // miecz wbity w podloge obok, ostrzem w dol
        var sword = new GameObject("Sword");
        sword.transform.SetParent(t, false);
        sword.transform.localPosition = new Vector3(0.7f, 0f, 0.35f);
        sword.transform.localRotation = Quaternion.Euler(8f, 20f, 6f);
        Part("Blade", box, MetalMat(), sword.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.02f), false);
        Part("Guard", box, MetalMat(), sword.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 0.04f, 0.04f), false);
        Part("Grip", box, BoneMat(), sword.transform, new Vector3(0f, 1.25f, 0f), new Vector3(0.04f, 0.26f, 0.04f), false);
        return root;
    }

    /// <summary>Pajak-prop: cialo (kula) + 8 nog z dwoch cylindrow, fiolet. Nogi leza w plaszczyznie XZ, os Y = "gora" ciala.</summary>
    public static GameObject SpiderProp()
    {
        var root = new GameObject("Spider Prop");
        var t = root.transform;
        var sph = PrimMesh(PrimitiveType.Sphere, ref sphereMesh);
        var cyl = PrimMesh(PrimitiveType.Cylinder, ref cylinderMesh);
        var m = SpiderMat();
        Part("Body", sph, m, t, Vector3.zero, new Vector3(0.38f, 0.28f, 0.5f), false);
        Part("Head", sph, m, t, new Vector3(0f, 0.02f, 0.32f), new Vector3(0.22f, 0.18f, 0.2f), false);
        float[] angles = { 35f, 65f, 100f, 135f };
        for (int side = -1; side <= 1; side += 2)
            foreach (float deg in angles)
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector3(side * Mathf.Sin(a), 0f, Mathf.Cos(a));
                Segment(t, cyl, m, dir * 0.15f, dir * 0.55f + Vector3.up * 0.3f, 0.025f, false);
                Segment(t, cyl, m, dir * 0.55f + Vector3.up * 0.3f, dir * 0.95f + Vector3.down * 0.25f, 0.02f, false);
            }
        return root;
    }

    /// <summary>
    /// Stalagmit: stozek (nisko-wielokatny, plaskie scianki) o wysokosci `height` m, ciemniejszy od podlogi, z 1-2 mniejszymi
    /// stozkami obok. Pivot na podlodze (podstawa lekko zapuszczona). Zamiast kapsul uzyty prawdziwy stozek (kapsula skalowana
    /// nie daje szpica).
    /// </summary>
    public static GameObject Stalagmite(float height, System.Random rng)
    {
        var root = new GameObject("Stalagmite");
        var cone = ConeMesh();
        var m = RockMat();
        Part("Spike", cone, m, root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(height * 0.5f, height + 0.05f, height * 0.5f), false);
        int extra = rng.Next(1, 3);
        for (int i = 0; i < extra; i++)
        {
            float h = height * (0.35f + (float)rng.NextDouble() * 0.25f);
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * height * 0.22f + Vector3.down * 0.05f;
            var s = Part("Spike", cone, m, root.transform, pos, new Vector3(h * 0.5f, h + 0.05f, h * 0.5f), false);
            s.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 10f - 5f, 0f, (float)rng.NextDouble() * 10f - 5f);
        }
        return root;
    }

    /// <summary>Kupka rumowiska: 3-5 szescianow 0,2-0,5 m obroconych losowo. Pivot na podlodze, bez colliderow.</summary>
    public static GameObject Rubble(System.Random rng)
    {
        var root = new GameObject("Rubble");
        var box = PrimMesh(PrimitiveType.Cube, ref cubeMesh);
        int n = rng.Next(3, 6);
        for (int i = 0; i < n; i++)
        {
            float size = 0.2f + (float)rng.NextDouble() * 0.3f;
            var pos = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, size * 0.35f, ((float)rng.NextDouble() - 0.5f) * 0.9f);
            var cube = Part("Rock", box, RubbleMat(), root.transform, pos, new Vector3(size, size * (0.7f + (float)rng.NextDouble() * 0.5f), size), false);
            cube.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
        }
        return root;
    }

    /// <summary>Kiel gniazda: cienki stozek 2-3 m, fioletowo-czarny. Pivot u podstawy.</summary>
    public static GameObject Fang(float height, float baseDiameter)
    {
        var root = new GameObject("Nest Fang");
        Part("Fang", ConeMesh(), FangMat(), root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(baseDiameter, height + 0.1f, baseDiameter), false);
        return root;
    }

    /// <summary>Stozek o wysokosci 1 i srednicy podstawy 1 (8 scianek, plaskie normalne), podstawa w y = 0.</summary>
    private static Mesh ConeMesh()
    {
        if (coneMesh != null) return coneMesh;
        const int N = 8;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
            int b = verts.Count;
            verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f));
            verts.Add(new Vector3(0f, 1f, 0f));
            verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }
        coneMesh = new Mesh { name = "Decor Cone" };
        coneMesh.SetVertices(verts);
        coneMesh.SetTriangles(tris, 0);
        coneMesh.RecalculateNormals();
        coneMesh.RecalculateBounds();
        return coneMesh;
    }

    // ------------------------------------------------------------ pomocnicze

    /// <summary>Obiekt z MeshFilter + MeshRenderer (bez collidera).</summary>
    private static GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Vector3 localScale, bool shadows)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    /// <summary>Walec/kapsula rozpieta miedzy dwoma punktami w ukladzie `parent`. Mesh musi miec wysokosc 2 (cylinder, capsule).</summary>
    private static void Segment(Transform parent, Mesh mesh, Material mat, Vector3 a, Vector3 b, float radius, bool shadows)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f) return;
        var go = Part("Segment", mesh, mat, parent, (a + b) * 0.5f, new Vector3(radius * 2f, len * 0.5f, radius * 2f), shadows);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
    }

    /// <summary>Siatka prymitywu pobrana raz; tymczasowy obiekt (z colliderem) niszczony od razu.</summary>
    private static Mesh PrimMesh(PrimitiveType type, ref Mesh cache)
    {
        if (cache != null) return cache;
        var tmp = GameObject.CreatePrimitive(type);
        cache = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return cache;
    }

    private static Material WebMat() => matWeb != null ? matWeb : matWeb = MakeMaterial("Decor Web", Color.white, true, true, WebTex());
    private static Material ThreadMat() => matThread != null ? matThread : matThread = MakeMaterial("Decor Thread", new Color(0.8f, 0.8f, 0.78f), true, false, null);
    private static Material CocoonMat() => matCocoon != null ? matCocoon : matCocoon = MakeMaterial("Decor Cocoon", new Color(0.78f, 0.77f, 0.72f), false, false, null, 0.12f);
    private static Material NestMat() => matNest != null ? matNest : matNest = MakeMaterial("Decor Nest", new Color(0.13f, 0.05f, 0.2f), false, false, null, 0.2f);
    private static Material BoneMat() => matBone != null ? matBone : matBone = MakeMaterial("Decor Bone", new Color(0.8f, 0.76f, 0.65f), false, false, null, 0.15f);
    private static Material MetalMat() => matMetal != null ? matMetal : matMetal = MakeMaterial("Decor Metal", new Color(0.45f, 0.47f, 0.5f), false, false, null, 0.5f, 0.7f);
    private static Material RockMat() => matRock != null ? matRock : matRock = MakeMaterial("Decor Rock", new Color(0.1f, 0.095f, 0.11f), false, false, null, 0.1f);
    private static Material RubbleMat() => matRubble != null ? matRubble : matRubble = MakeMaterial("Decor Rubble", new Color(0.16f, 0.15f, 0.16f), false, false, null, 0.08f);
    private static Material FangMat() => matFang != null ? matFang : matFang = MakeMaterial("Decor Fang", new Color(0.11f, 0.03f, 0.15f), false, false, null, 0.35f);
    private static Material SpiderMat() => matSpider != null ? matSpider : matSpider = MakeMaterial("Decor Spider", new Color(0.22f, 0.08f, 0.32f), false, false, null, 0.25f);

    /// <summary>URP/Lit (albo Unlit gdy `unlit`); `transparent` = alpha blend, dwustronny, bez ZWrite (jak telegrafy).</summary>
    private static Material MakeMaterial(string name, Color color, bool unlit, bool transparent, Texture2D tex, float smoothness = 0f, float metallic = 0f)
    {
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = name };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (tex != null) { mat.mainTexture = tex; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }
        return mat;
    }

    /// <summary>Tekstura 128x128: 8 promieni + 5 okregow z lekkim ugieciem miedzy promieniami, bialy alfa 0,5.</summary>
    private static Texture2D WebTex()
    {
        if (webTexture != null) return webTexture;
        const int N = 128;
        const int Spokes = 8;
        var px = new Color32[N * N];
        float seg = 2f * Mathf.PI / Spokes;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = 0f;
                if (r < 0.99f)
                {
                    float th = Mathf.Atan2(v, u) + Mathf.PI;
                    float s = Mathf.Repeat(th, seg) / seg;                 // 0..1 miedzy promieniami
                    float sag = 1f - 0.09f * 4f * s * (1f - s);            // ugiecie okregu w pol drogi
                    float spokeDist = Mathf.Min(s, 1f - s) * seg * r * (N * 0.5f);   // w pikselach
                    a = Mathf.Max(a, 1f - Mathf.SmoothStep(0.4f, 1.4f, spokeDist));
                    for (int k = 1; k <= 5; k++)
                    {
                        float d = Mathf.Abs(r - k * 0.19f * sag) * (N * 0.5f);
                        a = Mathf.Max(a, 1f - Mathf.SmoothStep(0.4f, 1.3f, d));
                    }
                }
                px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 0.5f * 255f));
            }
        webTexture = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Decor Web", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        webTexture.SetPixels32(px);
        webTexture.Apply(true);
        return webTexture;
    }
}
