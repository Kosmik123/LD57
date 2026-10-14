using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fabryki placeholderow dekoracji jaskini z prymitywow (bez colliderow: uzywamy samych siatek prymitywow).
/// Tekstura pajeczyny rysowana w kodzie. Materialy i siatki sa wspolne i tworzone leniwie.
/// </summary>
public static class DecorPrimitives
{
    private static Mesh quadMesh, sphereMesh, cylinderMesh, capsuleMesh, cubeMesh, coneMesh, icoMesh;
    private static Material matWeb, matThread, matCocoon, matNest, matBone, matMetal, matSpider, matRock, matRubble, matFang,
        matTrunk, matDeadWood, matLeafA, matLeafB, matBush, matStone, matGrass, matBlueCrystal, matBoulder, matMushroomCap, matMushroomStem;
    private static Texture2D webTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        quadMesh = sphereMesh = cylinderMesh = capsuleMesh = cubeMesh = coneMesh = icoMesh = null;
        matWeb = matThread = matCocoon = matNest = matBone = matMetal = matSpider = matRock = matRubble = matFang = null;
        matTrunk = matDeadWood = matLeafA = matLeafB = matBush = matStone = matGrass = matBlueCrystal = null;
        matBoulder = matMushroomCap = matMushroomStem = null;
        propCache.Clear();
        shapeCache.Clear();
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

    // ------------------------------------------------------------ propy podlogowe: jedna siatka na prop (rozklad DecorLayout)

    /// <summary>Liczba wariantow siatki na typ propa (wybierana w planie).</summary>
    public const int Variants = 4;

    private sealed class PropMesh { public Mesh Mesh; public Material[] Mats; }

    private static readonly System.Collections.Generic.Dictionary<int, PropMesh> propCache = new System.Collections.Generic.Dictionary<int, PropMesh>();
    private static readonly System.Collections.Generic.Dictionary<string, Mesh> shapeCache = new System.Collections.Generic.Dictionary<string, Mesh>();

    /// <summary>Tworzy materialy i (opcjonalnie) siatki wariantow podanych typow, zeby pomiar czasu generowania nie wliczal jednorazowych kosztow.</summary>
    public static void WarmUpNature(PropType[] types)
    {
        IcoMesh(); ConeMesh();
        TrunkMat(); LeafMatA(); LeafMatB(); BushMat(); StoneMat(); GrassMat(); BlueCrystalMat(); DeadWoodMat();
        BoulderMat(); MushroomCapMat(); MushroomStemMat(); RockMat(); RubbleMat(); FangMat(); CocoonMat(); WebMat();
        if (types == null) return;
        foreach (var t in types)
            for (int v = 0; v < Variants; v++) GetProp(t, v);
    }

    public static string PropName(PropType t)
    {
        switch (t)
        {
            case PropType.DeadTree: return "Dead Tree";
            case PropType.StalagmiteSmall: return "Stalagmite";
            case PropType.CrystalBig: case PropType.CrystalSmall: case PropType.CrystalTiny: return "Crystal Prop";
            case PropType.FangSmall: return "Fang";
            case PropType.WebLean: return "Web";
            case PropType.LandmarkTree: return "Landmark Tree";
            case PropType.Menhir: return "Landmark Menhir";
            case PropType.LandmarkCrystals: return "Landmark Crystals";
            case PropType.LandmarkFang: return "Landmark Fang";
            default: return t.ToString();   // Tree, Bush, Stone, Grass, Pebble, Boulder, Rubble, Stalagmite, Mushroom, Fang, Cocoon
        }
    }

    /// <summary>Prop podlogowy: jeden GameObject (MeshFilter + MeshRenderer, bez collidera), pivot na podlodze, skala jednorodna.</summary>
    public static GameObject Prop(PropType type, int variant, float scale)
    {
        var pm = GetProp(type, variant);
        var go = new GameObject(PropName(type));
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = pm.Mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = pm.Mats;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    private static PropMesh GetProp(PropType type, int variant)
    {
        variant = Mathf.Abs(variant) % Variants;
        int key = (int)type * 16 + variant;
        if (propCache.TryGetValue(key, out var cached) && cached.Mesh != null) return cached;
        var pm = BuildProp(type, variant);
        propCache[key] = pm;
        return pm;
    }

    private sealed class MeshBuilder
    {
        private readonly System.Collections.Generic.List<Vector3> v = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector3> n = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.List<int>[] tris;

        public MeshBuilder(int slots)
        {
            tris = new System.Collections.Generic.List<int>[slots];
            for (int i = 0; i < slots; i++) tris[i] = new System.Collections.Generic.List<int>();
        }

        public void Add(Mesh src, Vector3 pos, Quaternion rot, Vector3 scale, int slot)
        {
            var m = Matrix4x4.TRS(pos, rot, scale);
            var nm = m.inverse.transpose;
            var sv = src.vertices; var sn = src.normals; var suv = src.uv; var st = src.triangles;
            int b = v.Count;
            for (int i = 0; i < sv.Length; i++)
            {
                v.Add(m.MultiplyPoint3x4(sv[i]));
                n.Add(i < sn.Length ? nm.MultiplyVector(sn[i]).normalized : Vector3.up);
                uv.Add(i < suv.Length ? suv[i] : Vector2.zero);
            }
            for (int i = 0; i < st.Length; i++) tris[slot].Add(b + st[i]);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // --- ksztalty bazowe (low poly, plaskie sciany, wlasne siatki: nie zalezymy od odczytywalnosci wbudowanych prymitywow)

    private static void FlatTri(System.Collections.Generic.List<Vector3> o, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        var nrm = Vector3.Cross(b - a, c - a);
        var centroid = (a + b + c) / 3f;
        if (Vector3.Dot(nrm, centroid - inside) < 0f) { var t = b; b = c; c = t; }   // normalna na zewnatrz bryly wypuklej
        o.Add(a); o.Add(b); o.Add(c);
    }

    private static Mesh FlatMesh(string name, System.Collections.Generic.List<Vector3> triVerts)
    {
        var tris = new System.Collections.Generic.List<int>(triVerts.Count);
        for (int i = 0; i < triVerts.Count; i++) tris.Add(i);
        var mesh = new Mesh { name = name };
        mesh.SetVertices(triVerts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Graniastoslup o `sides` bokach, y od 0 do 1, promien dolu 0,5, gory 0,5 * topRatio (pien, galaz).</summary>
    private static Mesh PrismShape(int sides, float topRatio)
    {
        string key = "prism" + sides + "_" + Mathf.RoundToInt(topRatio * 100f);
        if (shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var o = new System.Collections.Generic.List<Vector3>();
        var inside = new Vector3(0f, 0.5f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            var b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            var t0 = new Vector3(Mathf.Cos(a0) * 0.5f * topRatio, 1f, Mathf.Sin(a0) * 0.5f * topRatio);
            var t1 = new Vector3(Mathf.Cos(a1) * 0.5f * topRatio, 1f, Mathf.Sin(a1) * 0.5f * topRatio);
            FlatTri(o, b0, t0, t1, inside);
            FlatTri(o, b0, t1, b1, inside);
            FlatTri(o, Vector3.zero, b0, b1, inside);
            FlatTri(o, new Vector3(0f, 1f, 0f), t0, t1, inside);
        }
        return shapeCache[key] = FlatMesh("Decor Prism", o);
    }

    /// <summary>Szescian o boku 1, srodek w zerze.</summary>
    private static Mesh BoxShape()
    {
        const string key = "box";
        if (shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var o = new System.Collections.Generic.List<Vector3>();
        var c = new Vector3[8];
        for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
        int[][] faces = { new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 } };
        foreach (var f in faces)
        {
            FlatTri(o, c[f[0]], c[f[1]], c[f[2]], Vector3.zero);
            FlatTri(o, c[f[0]], c[f[2]], c[f[3]], Vector3.zero);
        }
        return shapeCache[key] = FlatMesh("Decor Box", o);
    }

    /// <summary>Wydluzony oktaedr: dol w y=0, "rownik" (4 wierzcholki, promien 0,5) w y=0,3, szczyt w y=1. Menhir.</summary>
    private static Mesh OctaShape()
    {
        const string key = "octa";
        if (shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var o = new System.Collections.Generic.List<Vector3>();
        var ring = new[] { new Vector3(0.5f, 0.3f, 0f), new Vector3(0f, 0.3f, 0.5f), new Vector3(-0.5f, 0.3f, 0f), new Vector3(0f, 0.3f, -0.5f) };
        var inside = new Vector3(0f, 0.3f, 0f);
        for (int i = 0; i < 4; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % 4];
            FlatTri(o, Vector3.zero, a, b, inside);
            FlatTri(o, new Vector3(0f, 1f, 0f), a, b, inside);
        }
        return shapeCache[key] = FlatMesh("Decor Octa", o);
    }

    /// <summary>Krysztal: szesciokatny slup (y 0..0,65, promien 0,5) zakonczony ostrym szczytem w y=1.</summary>
    private static Mesh CrystalShape()
    {
        const string key = "crystal";
        if (shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var o = new System.Collections.Generic.List<Vector3>();
        var inside = new Vector3(0f, 0.4f, 0f);
        const int N = 6;
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
            var b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            var b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            var t0 = b0 + Vector3.up * 0.65f; var t1 = b1 + Vector3.up * 0.65f;
            FlatTri(o, b0, t0, t1, inside);
            FlatTri(o, b0, t1, b1, inside);
            FlatTri(o, Vector3.zero, b0, b1, inside);
            FlatTri(o, new Vector3(0f, 1f, 0f), t0, t1, inside);
        }
        return shapeCache[key] = FlatMesh("Decor Crystal", o);
    }

    /// <summary>Kwadrat 1x1 w plaszczyznie XY (dwustronny przez material), z UV.</summary>
    private static Mesh QuadShape()
    {
        const string key = "quad";
        if (shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var mesh = new Mesh { name = "Decor Quad" };
        mesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        mesh.RecalculateBounds();
        return shapeCache[key] = mesh;
    }

    private static Quaternion Rot(System.Random r, float tilt) => Quaternion.Euler(R(r, -tilt, tilt), R(r, 0f, 360f), R(r, -tilt, tilt));

    private static PropMesh BuildProp(PropType type, int variant)
    {
        var r = new System.Random(((int)type + 1) * 7919 + variant * 104729 + 17);
        var ico = IcoMesh(); var cone = ConeMesh(); var crystal = CrystalShape();
        Material[] mats;
        MeshBuilder b;
        switch (type)
        {
            case PropType.Tree:
            {
                b = new MeshBuilder(3);
                float trunkH = R(r, 3.3f, 3.8f);
                b.Add(PrismShape(6, 0.7f), new Vector3(0f, -0.05f, 0f), Rot(r, 2f), new Vector3(0.35f, trunkH + 0.05f, 0.35f), 0);
                Crowns(b, r, trunkH + 0.4f, 2.5f, 3.5f, 1f, 1, 2);
                mats = new[] { TrunkMat(), LeafMatA(), LeafMatB() };
                break;
            }
            case PropType.LandmarkTree:
            {
                b = new MeshBuilder(3);
                b.Add(PrismShape(7, 0.65f), new Vector3(0f, -0.05f, 0f), Rot(r, 1.5f), new Vector3(0.6f, 5.15f, 0.6f), 0);
                for (int i = 0; i < 3; i++)   // korzenie podporowe
                {
                    float a = (i * 120f + R(r, -20f, 20f)) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    b.Add(cone, dir * 0.3f + Vector3.up * 0.05f, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(65f, 0f, 0f), new Vector3(0.45f, 1.5f, 0.45f), 0);
                }
                Crowns(b, r, 5.6f, 3.0f, 4.0f, 1.25f, 1, 2);
                mats = new[] { TrunkMat(), LeafMatA(), LeafMatB() };
                break;
            }
            case PropType.Bush:
            {
                b = new MeshBuilder(1);
                float d = R(r, 1.15f, 1.4f);
                b.Add(ico, new Vector3(0f, d * 0.3f, 0f), Quaternion.Euler(0f, R(r, 0f, 360f), 0f), new Vector3(d, d * 0.75f, d), 0);
                float d2 = d * R(r, 0.55f, 0.7f);
                b.Add(ico, new Vector3(R(r, -0.4f, 0.4f), d2 * 0.28f, R(r, -0.4f, 0.4f)), Quaternion.Euler(0f, R(r, 0f, 360f), 0f), new Vector3(d2, d2 * 0.75f, d2), 0);
                mats = new[] { BushMat() };
                break;
            }
            case PropType.Stone:
            {
                b = new MeshBuilder(1);
                float w = 1.2f * R(r, 0.82f, 1f);
                b.Add(ico, new Vector3(0f, w * 0.12f, 0f), Rot(r, 8f), new Vector3(w * R(r, 0.9f, 1.15f), w * R(r, 0.5f, 0.55f), w * R(r, 0.9f, 1.15f)), 0);
                mats = new[] { StoneMat() };
                break;
            }
            case PropType.Pebble:
            {
                b = new MeshBuilder(1);
                b.Add(ico, new Vector3(0f, 0.04f, 0f), Rot(r, 6f), new Vector3(0.3f * R(r, 0.85f, 1f), 0.15f, 0.3f * R(r, 0.85f, 1f)), 0);
                b.Add(ico, new Vector3(R(r, 0.15f, 0.25f), 0.025f, R(r, -0.2f, 0.2f)), Rot(r, 6f), new Vector3(0.16f, 0.09f, 0.18f), 0);
                mats = new[] { StoneMat() };
                break;
            }
            case PropType.Grass:
            {
                b = new MeshBuilder(1);
                for (int i = 0; i < 5; i++)
                {
                    float h = R(r, 0.25f, 0.3f);
                    b.Add(cone, new Vector3(R(r, -0.07f, 0.07f), 0f, R(r, -0.07f, 0.07f)), Rot(r, 20f), new Vector3(0.07f, h, 0.07f), 0);
                }
                mats = new[] { GrassMat() };
                break;
            }
            case PropType.Boulder:
            {
                b = new MeshBuilder(1);
                b.Add(ico, new Vector3(0f, 0.7f, 0f), Rot(r, 5f), new Vector3(2.4f * R(r, 0.9f, 1.05f), 1.8f * R(r, 0.9f, 1f), 2.2f * R(r, 0.9f, 1.05f)), 0);
                b.Add(ico, new Vector3(R(r, 0.9f, 1.3f), 0.35f, R(r, 0.2f, 0.7f)), Rot(r, 8f), new Vector3(1.4f, 1f, 1.3f), 0);
                b.Add(ico, new Vector3(R(r, -1.2f, -0.8f), 0.2f, R(r, -0.8f, -0.4f)), Rot(r, 8f), new Vector3(0.9f, 0.6f, 0.8f), 0);
                mats = new[] { BoulderMat() };
                break;
            }
            case PropType.DeadTree:
            {
                b = new MeshBuilder(1);
                float h = R(r, 3.7f, 4.1f);
                b.Add(PrismShape(6, 0.45f), new Vector3(0f, -0.05f, 0f), Rot(r, 3f), new Vector3(0.32f, h + 0.05f, 0.32f), 0);
                float a0 = R(r, 0f, 360f);
                float[] attach = { 0.45f, 0.65f, 0.82f };
                for (int i = 0; i < 3; i++)   // 3 galezie: cienkie walce pod katem
                {
                    float a = (a0 + i * 120f + R(r, -25f, 25f)) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), R(r, 0.55f, 1.1f), Mathf.Sin(a)).normalized;
                    float len = R(r, 1.1f, 1.8f);
                    b.Add(PrismShape(5, 0.3f), new Vector3(0f, h * attach[i], 0f), Quaternion.FromToRotation(Vector3.up, dir), new Vector3(0.08f, len, 0.08f), 0);
                }
                mats = new[] { DeadWoodMat() };
                break;
            }
            case PropType.Rubble:
            {
                b = new MeshBuilder(1);
                int n = r.Next(4, 7);
                for (int i = 0; i < n; i++)
                {
                    float s = R(r, 0.2f, 0.48f);
                    b.Add(BoxShape(), new Vector3(R(r, -0.45f, 0.45f), s * 0.3f, R(r, -0.45f, 0.45f)), Quaternion.Euler(R(r, 0f, 360f), R(r, 0f, 360f), R(r, 0f, 360f)), new Vector3(s, s * R(r, 0.7f, 1.1f), s), 0);
                }
                mats = new[] { RubbleMat() };
                break;
            }
            case PropType.Stalagmite:
            case PropType.StalagmiteSmall:
            {
                b = new MeshBuilder(1);
                float h = type == PropType.Stalagmite ? 1.4f : 0.45f;
                b.Add(cone, new Vector3(0f, -0.05f, 0f), Rot(r, 3f), new Vector3(h * 0.45f, h + 0.05f, h * 0.45f), 0);
                int extra = r.Next(1, 3);
                for (int i = 0; i < extra; i++)
                {
                    float hh = h * R(r, 0.35f, 0.6f), a = R(r, 0f, Mathf.PI * 2f);
                    b.Add(cone, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * h * 0.22f + Vector3.down * 0.05f, Rot(r, 7f), new Vector3(hh * 0.5f, hh + 0.05f, hh * 0.5f), 0);
                }
                mats = new[] { RockMat() };
                break;
            }
            case PropType.CrystalBig:
            case PropType.CrystalSmall:
            case PropType.CrystalTiny:
            case PropType.LandmarkCrystals:
            {
                b = new MeshBuilder(1);
                float[] h = type == PropType.CrystalBig ? new[] { 3f, 2f, 1.6f } :
                            type == PropType.CrystalSmall ? new[] { 1.4f, 0.9f } :
                            type == PropType.CrystalTiny ? new[] { 0.45f, 0.3f } :
                            new[] { 4.4f, 3.2f, 2.5f, 1.4f };
                for (int i = 0; i < h.Length; i++)
                {
                    float hh = h[i] * (i == 0 ? 1f : R(r, 0.92f, 1.05f));
                    float a = R(r, 0f, Mathf.PI * 2f), off = i == 0 ? 0f : hh * 0.14f + 0.1f;
                    b.Add(crystal, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * off + Vector3.down * 0.05f, Rot(r, i == 0 ? 6f : 20f), new Vector3(hh * 0.27f, hh + 0.05f, hh * 0.27f), 0);
                }
                mats = new[] { BlueCrystalMat() };
                break;
            }
            case PropType.Mushroom:
            {
                b = new MeshBuilder(2);
                for (int i = 0; i < 3; i++)
                {
                    float h = R(r, 0.12f, 0.2f), cw = R(r, 0.14f, 0.22f);
                    var pos = i == 0 ? Vector3.zero : new Vector3(R(r, -0.18f, 0.18f), 0f, R(r, -0.18f, 0.18f));
                    b.Add(PrismShape(6, 0.8f), pos, Quaternion.identity, new Vector3(0.05f, h, 0.05f), 1);
                    b.Add(ico, pos + Vector3.up * h, Quaternion.Euler(0f, R(r, 0f, 360f), 0f), new Vector3(cw, cw * 0.55f, cw), 0);
                }
                mats = new[] { MushroomCapMat(), MushroomStemMat() };
                break;
            }
            case PropType.Fang:
            case PropType.LandmarkFang:
            case PropType.FangSmall:
            {
                b = new MeshBuilder(1);
                float h = type == PropType.Fang ? 4f : type == PropType.LandmarkFang ? 6f : 0.5f;
                float w = h * (type == PropType.FangSmall ? 0.44f : 0.17f);
                b.Add(cone, new Vector3(0f, -0.1f, 0f), Rot(r, 4f), new Vector3(w, h + 0.1f, w), 0);
                float[] sizes = { 0.5f, 0.33f };
                for (int i = 0; i < 2; i++)
                {
                    float hh = h * sizes[i] * R(r, 0.9f, 1.1f), a = R(r, 0f, Mathf.PI * 2f) + i * 3f;
                    b.Add(cone, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w * 0.9f + Vector3.down * 0.1f, Quaternion.Euler(R(r, -16f, 16f), R(r, 0f, 360f), R(r, -16f, 16f)), new Vector3(w * 0.6f, hh + 0.1f, w * 0.6f), 0);
                }
                mats = new[] { FangMat() };
                break;
            }
            case PropType.Cocoon:
            {
                b = new MeshBuilder(1);
                b.Add(ico, new Vector3(0f, 0.62f, 0f), Rot(r, 6f), new Vector3(0.6f * R(r, 0.9f, 1.1f), 1.3f * R(r, 0.9f, 1f), 0.6f * R(r, 0.9f, 1.1f)), 0);
                b.Add(ico, new Vector3(0.18f, 0.35f, 0.08f), Rot(r, 6f), new Vector3(0.4f, 0.7f, 0.4f), 0);
                mats = new[] { CocoonMat() };
                break;
            }
            case PropType.WebLean:
            {
                b = new MeshBuilder(1);
                b.Add(QuadShape(), new Vector3(0f, 0.62f, 0f), Quaternion.Euler(R(r, -35f, -25f), 0f, R(r, -8f, 8f)), new Vector3(1.5f, 1.5f, 1f), 0);
                mats = new[] { WebMat() };
                break;
            }
            default:   // Menhir
            {
                b = new MeshBuilder(1);
                var octa = OctaShape();
                b.Add(octa, new Vector3(0f, -0.1f, 0f), Quaternion.Euler(R(r, -3f, 3f), R(r, 0f, 360f), R(r, -3f, 3f)), new Vector3(1.3f, 5.1f, 1.1f), 0);
                b.Add(octa, new Vector3(1.1f, -0.1f, 0.4f), Quaternion.Euler(R(r, 8f, 14f), R(r, 0f, 360f), R(r, -6f, 6f)), new Vector3(0.8f, 2.7f, 0.7f), 0);
                b.Add(octa, new Vector3(-0.9f, -0.1f, -0.5f), Quaternion.Euler(R(r, -14f, -8f), R(r, 0f, 360f), R(r, -6f, 6f)), new Vector3(0.55f, 1.5f, 0.5f), 0);
                mats = new[] { BoulderMat() };
                break;
            }
        }
        return new PropMesh { Mesh = b.ToMesh("Decor " + type + " " + variant), Mats = mats };
    }

    /// <summary>Korona z 3 nakladajacych sie ikosfer (dwa odcienie zieleni: sloty slotA/slotB), srednice dMin..dMax, rozstaw skalowany przez `spread`.</summary>
    private static void Crowns(MeshBuilder b, System.Random r, float baseY, float dMin, float dMax, float spread, int slotA, int slotB)
    {
        var ico = IcoMesh();
        var centers = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(R(r, 0.6f, 1.1f), R(r, 0.3f, 0.7f), R(r, 0.4f, 0.9f)) * spread,
            new Vector3(R(r, -1.1f, -0.6f), R(r, 0.6f, 1.0f), R(r, -0.9f, -0.4f)) * spread,
        };
        for (int i = 0; i < 3; i++)
        {
            float d = R(r, dMin, dMax);
            b.Add(ico, new Vector3(0f, baseY, 0f) + centers[i], Rot(r, 12f), new Vector3(d, d * R(r, 0.82f, 0.95f), d), i == 1 ? slotB : slotA);
        }
    }
    private static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    /// <summary>Ikosaedr (20 plaskich scianek), promien 0,5 (srednica 1). Niskopoligonowa "kula" na korony, krzaki i kamienie.</summary>
    private static Mesh IcoMesh()
    {
        if (icoMesh != null) return icoMesh;
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f;
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        var verts = new System.Collections.Generic.List<Vector3>(f.Length);
        var tris = new System.Collections.Generic.List<int>(f.Length);
        for (int i = 0; i < f.Length; i++) { verts.Add(v[f[i]]); tris.Add(i); }
        icoMesh = new Mesh { name = "Decor Ico" };
        icoMesh.SetVertices(verts);
        icoMesh.SetTriangles(tris, 0);
        icoMesh.RecalculateNormals();
        icoMesh.RecalculateBounds();
        return icoMesh;
    }

    private static Material TrunkMat() => matTrunk != null ? matTrunk : matTrunk = MakeMaterial("Decor Trunk", new Color(0.26f, 0.16f, 0.09f), false, false, null, 0.05f);
    private static Material DeadWoodMat() => matDeadWood != null ? matDeadWood : matDeadWood = MakeMaterial("Decor Dead Wood", new Color(0.2f, 0.17f, 0.14f), false, false, null, 0.05f);
    private static Material LeafMatA() => matLeafA != null ? matLeafA : matLeafA = MakeMaterial("Decor Leaf A", new Color(0.12f, 0.38f, 0.1f), false, false, null, 0.08f);
    private static Material LeafMatB() => matLeafB != null ? matLeafB : matLeafB = MakeMaterial("Decor Leaf B", new Color(0.18f, 0.46f, 0.12f), false, false, null, 0.08f);
    private static Material BushMat() => matBush != null ? matBush : matBush = MakeMaterial("Decor Bush", new Color(0.06f, 0.24f, 0.07f), false, false, null, 0.05f);
    private static Material StoneMat() => matStone != null ? matStone : matStone = MakeMaterial("Decor Stone", new Color(0.4f, 0.4f, 0.42f), false, false, null, 0.1f);
    private static Material GrassMat() => matGrass != null ? matGrass : matGrass = MakeMaterial("Decor Grass", new Color(0.22f, 0.58f, 0.15f), false, false, null, 0.05f);
    private static Material BlueCrystalMat() => matBlueCrystal != null ? matBlueCrystal : matBlueCrystal = MakeMaterial("Decor Blue Crystal", new Color(0.4f, 0.95f, 1.5f), true, false, null);
    private static Material BoulderMat() => matBoulder != null ? matBoulder : matBoulder = MakeMaterial("Decor Boulder", new Color(0.3f, 0.3f, 0.33f), false, false, null, 0.1f);
    private static Material MushroomCapMat() => matMushroomCap != null ? matMushroomCap : matMushroomCap = MakeMaterial("Decor Mushroom Cap", new Color(0.2f, 1.2f, 0.9f), true, false, null);
    private static Material MushroomStemMat() => matMushroomStem != null ? matMushroomStem : matMushroomStem = MakeMaterial("Decor Mushroom Stem", new Color(0.75f, 0.78f, 0.7f), false, false, null, 0.1f);

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
