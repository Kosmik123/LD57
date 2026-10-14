using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Grafika (tura „dopracowanie”): sylwetki wrogów z prymitywów (Visual/Body/Eyes/Legs), światła z LightFlicker,
/// włączenie post-processingu na kamerze i obiekt Telegraphs w scenie. Menu: LD57 > Wire P3 Visuals.
/// Idempotentne: Visual jest usuwany i budowany od nowa, światła i komponenty są sprawdzane po nazwie/typie.
/// </summary>
public static class P3WiringVisuals
{
    const string EnemyFolder = "Assets/Prefabs/Enemies";
    const string MatFolder = EnemyFolder + "/Materials";
    const string SlimePath = EnemyFolder + "/Slime Enemy.prefab";
    const string SpiderPath = EnemyFolder + "/Spider.prefab";
    const string ShooterPath = EnemyFolder + "/Shooter.prefab";
    const string BossPath = EnemyFolder + "/Boss Spider Mother.prefab";
    const string ProjectilePath = EnemyFolder + "/Projectile.prefab";
    const string ProjectileMatPath = EnemyFolder + "/Projectile.mat";
    const string HeartPath = "Assets/Prefabs/Pickups/Heart.prefab";
    const string HeartMatPath = "Assets/Prefabs/Pickups/Heart.mat";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P3 Visuals")]
    public static void Run()
    {
        Debug.Log("[P3Visuals] start");
        Directory.CreateDirectory(MatFolder);
        AssetDatabase.Refresh();
        BuildSlime();
        BuildSpider();
        BuildShooter();
        BuildBoss();
        WirePickupsAndProjectile();
        AssetDatabase.SaveAssets();
        WireScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P3Visuals] done");
    }

    // ---------------------------------------------------------------- materiały

    static Material Mat(string name, Color color, Color? emission = null, float emissionIntensity = 3f, float alpha = 1f, float smoothness = 0.3f)
    {
        var path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        color.a = alpha;
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        if (alpha < 1f)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;
        }
        SetEmission(mat, emission, emissionIntensity);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SetEmission(Material mat, Color? emission, float intensity)
    {
        if (emission.HasValue)
        {
            mat.SetColor("_EmissionColor", emission.Value * intensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
    }

    // ---------------------------------------------------------------- helpers

    static Transform Empty(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        return go.transform;
    }

    static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // kapsuła między dwoma punktami (kapsuła domyślna: wysokość 2, promień 0,5 wzdłuż Y)
    static void Limb(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
    {
        var d = b - a;
        Prim(parent, name, PrimitiveType.Capsule, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), mat,
            Quaternion.FromToRotation(Vector3.up, d.normalized));
    }

    static void Legs(Transform visual, int count, float bodyY, float hipR, Vector3 kneeOffset, float footExtra, float upperThick, float lowerThick, Material mat)
    {
        var legs = Empty(visual, "Legs", Vector3.zero);
        for (int i = 0; i < count; i++)
        {
            var pivot = Empty(legs, $"Leg_{i}", new Vector3(0f, bodyY, 0f));
            pivot.localRotation = Quaternion.Euler(0f, i * 360f / count, 0f);
            var hip = new Vector3(0f, 0f, hipR);                  // lokalnie: +Z = na zewnątrz, Y względem wysokości ciała
            var knee = hip + kneeOffset;
            var foot = new Vector3(0f, -bodyY, knee.z + footExtra);
            Limb(pivot, "Upper", hip, knee, upperThick, mat);
            Limb(pivot, "Lower", knee, foot, lowerThick, mat);
        }
    }

    static Transform ResetVisual(GameObject root)
    {
        // usuń stare Visual i brakujące zagnieżdżone prefaby (np. SlimePADefault, którego źródła nie ma w repo)
        foreach (var child in root.transform.Cast<Transform>().ToList())
        {
            if (child.name == "Visual" || child.name == "SlimePADefault" || PrefabUtility.IsPrefabAssetMissing(child.gameObject))
                Object.DestroyImmediate(child.gameObject);
        }
        var v = new GameObject("Visual").transform;
        v.SetParent(root.transform, false);
        return v;
    }

    static void RemoveChildColliders(GameObject root)
    {
        var hitbox = root.transform.Find("Attack Hitbox");
        foreach (var c in root.GetComponentsInChildren<Collider>(true).ToList())
        {
            if (c == null || c.gameObject == root) continue;
            if (hitbox != null && c.transform.IsChildOf(hitbox)) continue;
            Object.DestroyImmediate(c);
        }
    }

    static T RootCollider<T>(GameObject root) where T : Collider
    {
        foreach (var c in root.GetComponents<Collider>())
            if (!(c is T)) Object.DestroyImmediate(c);
        var col = root.GetComponent<T>(); if (col == null) col = root.AddComponent<T>();
        col.isTrigger = false;
        return col;
    }

    static void Eyes(Transform visual, Vector3 centerLeft, float size, Material mat, Color lightColor = default, float lightRange = 0f, float lightIntensity = 0f)
    {
        var eyes = Empty(visual, "Eyes", Vector3.zero);
        var l = Prim(eyes, "EyeL", PrimitiveType.Sphere, centerLeft, Vector3.one * size, mat);
        var r = Prim(eyes, "EyeR", PrimitiveType.Sphere, new Vector3(-centerLeft.x, centerLeft.y, centerLeft.z), Vector3.one * size, mat);
        if (lightRange > 0f)
        {
            AddLight(l.transform, "Eye Light", lightColor, lightRange, lightIntensity);
            AddLight(r.transform, "Eye Light", lightColor, lightRange, lightIntensity);
        }
    }

    static Light AddLight(Transform parent, string name, Color color, float range, float intensity)
    {
        var t = parent.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        var light = go.GetComponent<Light>(); if (light == null) light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        if (go.GetComponent<LightFlicker>() == null) go.AddComponent<LightFlicker>();
        if (go.GetComponent<VolumetricAdditionalLight>() == null) go.AddComponent<VolumetricAdditionalLight>();
        // LightFlicker zapamiętuje intensywność w Awake, więc zapis z wartością bazową wystarczy
        return light;
    }

    delegate void Builder(GameObject root);

    static void Edit(string path, Builder build)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[P3Visuals] {path} OK");
        }
        catch (System.Exception e) { Debug.LogError($"[P3Visuals] {path}: {e}"); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- wrogowie

    static void BuildSlime()
    {
        Edit(SlimePath, root =>
        {
            RemoveChildColliders(root);
            var visual = ResetVisual(root);
            var body = Mat("Slime_Body", new Color(0.2f, 0.85f, 0.25f), new Color(0.1f, 0.5f, 0.1f), 0.5f, 0.75f, 0.8f);
            var eye = Mat("Slime_Eye", new Color(0.9f, 1f, 0.4f), new Color(0.9f, 1f, 0.4f), 3f);
            Prim(visual, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 1f), body);
            Eyes(visual, new Vector3(0.18f, 0.42f, 0.42f), 0.16f, eye);
            var col = RootCollider<SphereCollider>(root);
            col.radius = 0.5f; col.center = new Vector3(0f, 0.35f, 0f);
        });
    }

    static void BuildSpider()
    {
        Edit(SpiderPath, root =>
        {
            RemoveChildColliders(root);
            var visual = ResetVisual(root);
            var body = Mat("Spider_Body", new Color(0.45f, 0.12f, 0.8f), null, 0f, 1f, 0.45f);
            var leg = Mat("Spider_Leg", new Color(0.32f, 0.08f, 0.6f), null, 0f, 1f, 0.3f);
            var eye = Mat("Spider_Eye", new Color(1f, 0.55f, 1f), new Color(1f, 0.45f, 1f), 3f);
            float bodyY = 0.55f;
            Prim(visual, "Body", PrimitiveType.Sphere, new Vector3(0f, bodyY, 0f), new Vector3(0.9f, 0.5f, 1.1f), body);
            Eyes(visual, new Vector3(0.2f, bodyY + 0.13f, 0.38f), 0.16f, eye);
            Legs(visual, 8, bodyY, 0.35f, new Vector3(0f, 0.45f, 0.45f), 0.35f, 0.09f, 0.07f, leg);
            var col = RootCollider<SphereCollider>(root);
            col.radius = 0.55f; col.center = new Vector3(0f, bodyY, 0f);
        });
    }

    static void BuildShooter()
    {
        Edit(ShooterPath, root =>
        {
            RemoveChildColliders(root);
            var visual = ResetVisual(root);
            var body = Mat("Shooter_Body", new Color(0.7f, 0.9f, 0.1f), new Color(0.35f, 0.5f, 0.05f), 0.6f, 0.6f, 0.7f);
            var core = Mat("Shooter_Core", new Color(1f, 0.95f, 0.2f), new Color(1f, 0.95f, 0.2f), 4f);
            var barrel = Mat("Shooter_Barrel", new Color(0.25f, 0.3f, 0.08f), null, 0f, 1f, 0.5f);
            var eye = Mat("Shooter_Eye", new Color(1f, 1f, 0.7f), new Color(1f, 1f, 0.6f), 3f);
            Prim(visual, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.6f, body);
            var coreGo = Prim(visual, "Core", PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.3f, core);
            Prim(visual, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0.45f), new Vector3(0.12f, 0.25f, 0.12f), barrel,
                Quaternion.Euler(90f, 0f, 0f));
            Eyes(visual, new Vector3(0.1f, 0.85f, 0.26f), 0.11f, eye);
            AddLight(coreGo.transform, "Core Light", new Color(1f, 0.95f, 0.3f), 3f, 2.5f);
            var col = RootCollider<CapsuleCollider>(root);
            col.radius = 0.3f; col.height = 1.2f; col.center = new Vector3(0f, 0.6f, 0f); col.direction = 1;
        });
    }

    static void BuildBoss()
    {
        Edit(BossPath, root =>
        {
            RemoveChildColliders(root);
            var visual = ResetVisual(root);
            var body = Mat("Boss_Body", new Color(0.4f, 0.03f, 0.05f), null, 0f, 1f, 0.35f);
            var leg = Mat("Boss_Leg", new Color(0.28f, 0.02f, 0.04f), null, 0f, 1f, 0.3f);
            var eye = Mat("Boss_Eye", new Color(1f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.05f), 4f);
            var core = Mat("Boss_Core", new Color(1f, 0.25f, 0.05f), new Color(1f, 0.2f, 0.03f), 3f);
            float bodyY = 1.6f;
            Prim(visual, "Body", PrimitiveType.Sphere, new Vector3(0f, bodyY, 0f), new Vector3(3f, 1.5f, 3f), body);
            Prim(visual, "Core", PrimitiveType.Sphere, new Vector3(0f, bodyY + 0.62f, -0.5f), Vector3.one * 0.6f, core);
            Eyes(visual, new Vector3(0.55f, bodyY + 0.35f, 1.15f), 0.38f, eye, new Color(1f, 0.1f, 0.05f), 6f, 3f);
            Legs(visual, 8, bodyY, 1.3f, new Vector3(0f, 1.0f, 1.1f), 0.8f, 0.28f, 0.22f, leg);
            var col = RootCollider<CapsuleCollider>(root);
            col.radius = 1.4f; col.height = 3.2f; col.center = new Vector3(0f, bodyY, 0f); col.direction = 1;
        });
    }

    // ---------------------------------------------------------------- serce, pocisk

    static void WirePickupsAndProjectile()
    {
        var heartMat = AssetDatabase.LoadAssetAtPath<Material>(HeartMatPath);
        if (heartMat != null) { SetEmission(heartMat, new Color(1f, 0.08f, 0.15f), 2.5f); EditorUtility.SetDirty(heartMat); }
        var projMat = AssetDatabase.LoadAssetAtPath<Material>(ProjectileMatPath);
        if (projMat != null)
        {
            var yellow = new Color(1f, 0.9f, 0.2f);
            projMat.SetColor("_BaseColor", yellow); projMat.color = yellow;
            SetEmission(projMat, yellow, 3f);
            EditorUtility.SetDirty(projMat);
        }
        Edit(HeartPath, root => AddLight(root.transform, "Glow Light", new Color(1f, 0.1f, 0.15f), 4f, 2.5f).transform.localPosition = new Vector3(0f, 0.3f, 0f));
        Edit(ProjectilePath, root => AddLight(root.transform, "Glow Light", new Color(1f, 0.85f, 0.2f), 3f, 2f));
    }

    // ---------------------------------------------------------------- scena

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();

        var camGo = roots.FirstOrDefault(r => r.name == "Main Camera") ?? GameObject.Find("Main Camera");
        var cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
        if (cam != null)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
        }
        else Debug.LogWarning("[P3Visuals] brak Main Camera w scenie");

        var go = roots.FirstOrDefault(r => r.name == "Telegraphs") ?? new GameObject("Telegraphs");
        var tr = go.GetComponent<TelegraphRenderer>(); if (tr == null) tr = go.AddComponent<TelegraphRenderer>();
        var so = new SerializedObject(tr);
        var shader = so.FindProperty("unlitShader");
        if (shader != null)
        {
            shader.objectReferenceValue = Shader.Find("Universal Render Pipeline/Unlit");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P3Visuals] scena OK");
    }
}
