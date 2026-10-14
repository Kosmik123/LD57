using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Podpięcie walki z bossem (P12): nogi (BossLeg + collidery + Health 12 + kinematyczny Rigidbody + tag Enemy), Body Hurtbox,
/// pola SpiderMotherBoss, Health 96, prefaby Spider Egg i Web. Menu: LD57 > Wire P12 Boss. Uruchamiać PO P3 Visuals
/// (P3 buduje Visual/Legs od nowa i kasuje komponenty na nogach).
/// Idempotentne: prefaby jajka i sieci nadpisuje tylko wariant NADPISZ; reszta to GetOrAdd + ustawienie pól.
/// Pasek bossa (BossHealthBar) buduje rząd nóg w kodzie, więc scena nie wymaga zmian.
/// </summary>
public static class P12WiringBoss
{
    const string EnemyFolder = "Assets/Prefabs/Enemies";
    const string MatFolder = EnemyFolder + "/Materials";
    const string MeshFolder = EnemyFolder + "/Meshes";
    const string BossPath = EnemyFolder + "/Boss Spider Mother.prefab";
    const string SpiderPath = EnemyFolder + "/Spider.prefab";
    const string EggPath = EnemyFolder + "/Spider Egg.prefab";
    const string WebPath = EnemyFolder + "/Web.prefab";
    const int LegHealth = 12;
    const int LegCount = 8;

    [MenuItem("LD57/Wire P12 Boss")]
    public static void Run() => RunInternal(false);

    [MenuItem("LD57/Wire P12 Boss (NADPISZ prefaby jajka i sieci)")]
    public static void RunOverwrite() => RunInternal(true);

    static void RunInternal(bool overwrite)
    {
        Debug.Log("[P12Boss] start");
        EnsureFolder(MatFolder);
        EnsureFolder(MeshFolder);
        var egg = BuildEgg(overwrite);
        var web = BuildWeb(overwrite);
        WireBoss(egg, web);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P12Boss] done (BossHealthBar buduje rząd nóg w kodzie, scena bez zmian)");
    }

    // ---------------------------------------------------------------- helpers

    static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Set(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P12Boss] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object t, string f, Object v) => Set(t, f, p => p.objectReferenceValue = v);
    static void SetInt(Object t, string f, int v) => Set(t, f, p => p.intValue = v);
    static void SetFloat(Object t, string f, float v) => Set(t, f, p => p.floatValue = v);
    static void SetBool(Object t, string f, bool v) => Set(t, f, p => p.boolValue = v);

    static Material Mat(string name, Color color, Color? emission = null, float emissionIntensity = 2f, float smoothness = 0.35f)
    {
        var path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.SetColor("_EmissionColor", emission.Value * emissionIntensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Płasko cieniowana ikosfera (low poly): subdiv 0 = 20 ścian, 1 = 80. Zapisana jako asset siatki.</summary>
    static Mesh Icosphere(string name, int subdiv, float radius)
    {
        var path = $"{MeshFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        var tris = new List<int[]>
        {
            new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
            new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
            new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
            new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1},
        };
        for (int s = 0; s < subdiv; s++)
        {
            var next = new List<int[]>();
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                cache[key] = v.Count - 1;
                return v.Count - 1;
            }
            foreach (var tri in tris)
            {
                int ab = Mid(tri[0], tri[1]), bc = Mid(tri[1], tri[2]), ca = Mid(tri[2], tri[0]);
                next.Add(new[] { tri[0], ab, ca });
                next.Add(new[] { tri[1], bc, ab });
                next.Add(new[] { tri[2], ca, bc });
                next.Add(new[] { ab, bc, ca });
            }
            tris = next;
        }

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        foreach (var tri in tris)
        {
            Vector3 a = v[tri[0]] * radius, b = v[tri[1]] * radius, c = v[tri[2]] * radius;
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, a + b + c) < 0f) { var tmp = b; b = c; c = tmp; n = -n; } // front = zgodnie z konwencją Unity (na zewnątrz)
            n.Normalize();
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ---------------------------------------------------------------- jajko

    static GameObject BuildEgg(bool overwrite)
    {
        var spider = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EggPath);
        if (existing != null && !overwrite)
        {
            var contents = PrefabUtility.LoadPrefabContents(EggPath);
            try
            {
                var egg = contents.GetComponent<SpiderEgg>();
                if (egg != null) SetRef(egg, "spiderPrefab", spider);
                PrefabUtility.SaveAsPrefabAsset(contents, EggPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Debug.Log("[P12Boss] Spider Egg.prefab już jest: odświeżono tylko referencję pająka");
            return AssetDatabase.LoadAssetAtPath<GameObject>(EggPath);
        }

        var cream = Mat("Egg_Cream", new Color(0.95f, 0.9f, 0.74f), null, 0f, 0.5f);
        var purple = Mat("Egg_Spot", new Color(0.45f, 0.15f, 0.7f), new Color(0.45f, 0.15f, 0.7f), 0.6f, 0.4f);
        var big = Icosphere("Icosphere_1", 1, 0.5f);   // średnica 1, skalowane do 0,5 m
        var small = Icosphere("Icosphere_0", 0, 0.5f);

        var root = new GameObject("Spider Egg");
        try
        {
            root.tag = "Enemy";
            root.layer = spider != null ? spider.layer : 0;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.25f, 0f);

            var health = root.AddComponent<Health>();
            SetInt(health, "maxHealth", 1);
            SetInt(health, "currentHealth", 1);
            var enemy = root.AddComponent<EnemyHealth>();
            SetInt(enemy, "experienceReward", 2);
            SetBool(enemy, "isBoss", false);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            MeshObject(visual, "Shell", big, cream, new Vector3(0f, 0.21f, 0f), new Vector3(1f, 0.84f, 1f)); // 0,5 m, spłaszczone
            var dirs = new[] { new Vector3(0.7f, 0.4f, 0.4f), new Vector3(-0.6f, 0.2f, 0.6f), new Vector3(0.1f, 0.8f, -0.5f), new Vector3(-0.4f, 0.1f, -0.8f), new Vector3(0.5f, -0.1f, -0.7f) };
            for (int i = 0; i < dirs.Length; i++)
            {
                var d = dirs[i].normalized;
                var pos = new Vector3(0f, 0.21f, 0f) + Vector3.Scale(d, new Vector3(0.25f, 0.21f, 0.25f)) * 0.98f;
                var spot = MeshObject(visual, "Spot " + i, small, purple, pos, Vector3.one * (0.22f + 0.04f * (i % 2)));
                spot.transform.rotation = Quaternion.LookRotation(d);
            }

            var eggComp = root.AddComponent<SpiderEgg>();
            SetRef(eggComp, "spiderPrefab", spider);
            SetRef(eggComp, "visual", visual);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, EggPath);
            Debug.Log("[P12Boss] Spider Egg.prefab OK");
            return saved;
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---------------------------------------------------------------- sieć

    static GameObject BuildWeb(bool overwrite)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WebPath);
        if (existing != null && !overwrite)
        {
            Debug.Log("[P12Boss] Web.prefab już jest, bez zmian");
            return existing;
        }

        var white = Mat("Web_White", new Color(0.95f, 0.97f, 1f), new Color(1f, 1f, 1f), 2.5f, 0.2f);
        var root = new GameObject("Web");
        try
        {
            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(root.transform, false);
            for (int i = 0; i < 3; i++)
            {
                var strand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strand.name = "Strand " + i;
                Object.DestroyImmediate(strand.GetComponent<Collider>());
                strand.transform.SetParent(spinner, false);
                var radial = Quaternion.Euler(0f, 0f, i * 120f) * Vector3.right * 0.12f;
                strand.transform.localPosition = radial;
                strand.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f + 45f);
                strand.transform.localScale = new Vector3(0.05f, 0.05f, 0.8f);
                strand.GetComponent<Renderer>().sharedMaterial = white;
            }
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(spinner, false);
            core.transform.localScale = Vector3.one * 0.14f;
            core.GetComponent<Renderer>().sharedMaterial = white;

            var web = root.AddComponent<WebProjectile>();
            SetRef(web, "spinner", spinner);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, WebPath);
            Debug.Log("[P12Boss] Web.prefab OK");
            return saved;
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---------------------------------------------------------------- boss

    static void WireBoss(GameObject egg, GameObject web)
    {
        var contents = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            int layer = contents.layer;

            // Nogi: Health 12, kinematyczny Rigidbody, tag Enemy, triggerowe collidery na Upper i Lower.
            int wired = 0;
            for (int i = 0; i < LegCount; i++)
            {
                var leg = contents.transform.Find($"Visual/Legs/Leg_{i}");
                if (leg == null) { Debug.LogWarning($"[P12Boss] brak Visual/Legs/Leg_{i} (uruchom najpierw LD57 > Wire P3 Visuals)"); continue; }
                var go = leg.gameObject;
                go.tag = "Enemy";
                go.layer = layer;

                var rb = GetOrAdd<Rigidbody>(go);
                rb.isKinematic = true;
                rb.useGravity = false;

                var health = GetOrAdd<Health>(go);
                SetInt(health, "maxHealth", LegHealth);
                SetInt(health, "currentHealth", LegHealth);
                var bossLeg = GetOrAdd<BossLeg>(go);
                SetInt(bossLeg, "index", i);

                foreach (var part in new[] { "Upper", "Lower" })
                {
                    var limb = leg.Find(part);
                    if (limb == null) continue;
                    limb.gameObject.layer = layer;
                    var cap = GetOrAdd<CapsuleCollider>(limb.gameObject);
                    cap.isTrigger = true;
                    cap.direction = 1;   // oś Y: zgodna z siatką kapsuły prymitywu
                    cap.radius = 0.9f;   // 0,9 x grubość kończyny (0,28/0,22) = ok. 0,25 m, szerzej niż siatka dla łatwiejszych trafień
                    cap.height = 2f;
                    cap.center = Vector3.zero;
                }
                wired++;
            }

            // Ciało: stary collider korzenia -> dziecko Body Hurtbox (własny Rigidbody i Health, trafienia idą w nogi).
            float radius = 1.4f, height = 3.2f; var center = new Vector3(0f, 1.6f, 0f); int direction = 1;
            var rootCap = contents.GetComponent<CapsuleCollider>();
            if (rootCap != null)
            {
                radius = rootCap.radius; height = rootCap.height; center = rootCap.center; direction = rootCap.direction;
                Object.DestroyImmediate(rootCap);
            }
            var hbT = contents.transform.Find("Body Hurtbox");
            GameObject hb;
            if (hbT != null) hb = hbT.gameObject;
            else
            {
                hb = new GameObject("Body Hurtbox");
                hb.transform.SetParent(contents.transform, false);
            }
            hb.tag = "Enemy";
            hb.layer = layer;
            var hbRb = GetOrAdd<Rigidbody>(hb);
            hbRb.isKinematic = true;
            hbRb.useGravity = false;
            var hbCol = GetOrAdd<CapsuleCollider>(hb);
            hbCol.isTrigger = false;
            if (hbT == null) { hbCol.radius = radius; hbCol.height = height; hbCol.center = center; hbCol.direction = direction; }
            var hbHealth = GetOrAdd<Health>(hb);
            SetInt(hbHealth, "maxHealth", 9999);
            SetInt(hbHealth, "currentHealth", 9999);
            GetOrAdd<BossBodyHitbox>(hb);

            // Boss: pola, Health = suma nóg, animator.
            var boss = GetOrAdd<SpiderMotherBoss>(contents);
            SetRef(boss, "eggPrefab", egg);
            SetRef(boss, "webPrefab", web);
            SetRef(boss, "minionPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath));
            var rootHealth = contents.GetComponent<Health>();
            SetInt(rootHealth, "maxHealth", LegHealth * LegCount);
            SetInt(rootHealth, "currentHealth", LegHealth * LegCount);
            var agent = contents.GetComponent<NavMeshAgent>();
            if (agent != null) agent.speed = 3f;
            GetOrAdd<BossAnimator>(contents);

            PrefabUtility.SaveAsPrefabAsset(contents, BossPath);
            Debug.Log($"[P12Boss] Boss Spider Mother.prefab OK: nóg podpiętych {wired}/{LegCount}, Health {LegHealth * LegCount}");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
