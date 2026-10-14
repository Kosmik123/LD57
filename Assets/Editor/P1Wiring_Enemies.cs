using System.IO;
using System.Linq;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Podpięcie wrogów P1 (pająk, strzelec, pocisk, pula pocisków w scenie). Menu: LD57 > Wire P1 Enemies.
/// Idempotentne: istniejące prefaby i obiekt sceny nie są nadpisywane.
/// </summary>
public static class P1WiringEnemies
{
    const string Folder = "Assets/Prefabs/Enemies";
    const string BasePath = Folder + "/_Enemy Base.prefab";
    const string SpiderPath = Folder + "/Spider.prefab";
    const string ShooterPath = Folder + "/Shooter.prefab";
    const string ProjectilePath = Folder + "/Projectile.prefab";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P1 Enemies")]
    public static void Run()
    {
        Debug.Log("[P1Wiring.Enemies] start");
        var projectile = CreateProjectile();
        CreateSpider();
        CreateShooter();
        WireScene(projectile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P1Wiring.Enemies] done");
    }

    // ---------------------------------------------------------------- helpers

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[P1Wiring.Enemies] brak pola '{field}' w {target.GetType().Name}");
            return;
        }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object target, string field, Object value) => SetProp(target, field, p => p.objectReferenceValue = value);
    static void SetInt(Object target, string field, int value) => SetProp(target, field, p => p.intValue = value);
    static void SetFloat(Object target, string field, float value) => SetProp(target, field, p => p.floatValue = value);
    static void SetBool(Object target, string field, bool value) => SetProp(target, field, p => p.boolValue = value);

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Material CreateMaterial(string name, Color color)
    {
        var path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static GameObject AddVisual(GameObject root, PrimitiveType type, Vector3 scale, float y, Material mat)
    {
        var visual = GameObject.CreatePrimitive(type);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0, y, 0);
        visual.transform.localScale = scale;
        visual.GetComponent<Renderer>().sharedMaterial = mat;
        // Collider zostaje (trafienie ostrzem gracza); to trigger-less collider na dziecku z rodzicem-Rigidbody.
        return visual;
    }

    static GameObject InstantiateBase()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        if (basePrefab == null)
        {
            Debug.LogError("[P1Wiring.Enemies] brak " + BasePath);
            return null;
        }
        return (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
    }

    static void CommonEnemySetup(GameObject inst, int xp, float speed)
    {
        var graph = inst.GetComponent<BehaviorGraphAgent>();
        if (graph != null) graph.enabled = false;

        var health = inst.GetComponent<Health>();
        SetInt(health, "maxHealth", 4);
        SetInt(health, "currentHealth", 4);
        SetInt(inst.GetComponent<EnemyHealth>(), "experienceReward", xp);
        inst.GetComponent<NavMeshAgent>().speed = speed;
    }

    // ---------------------------------------------------------------- enemies

    static void CreateSpider()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath) != null) { Debug.Log("[P1Wiring.Enemies] Spider.prefab istnieje, pomijam"); return; }
        var inst = InstantiateBase();
        if (inst == null) return;
        try
        {
            inst.name = "Spider";
            CommonEnemySetup(inst, 15, 6f);

            var attack = inst.GetComponent<EnemyMeleeAttack>();
            SetFloat(attack, "windup", 0.4f);
            SetBool(attack, "autoAttack", true);
            var hitbox = inst.transform.Find("Attack Hitbox");
            if (hitbox != null)
                foreach (var blade in hitbox.GetComponents<DamagingBlade>())
                    SetInt(blade, "damage", 8);

            GetOrAdd<SpiderController>(inst);
            AddVisual(inst, PrimitiveType.Sphere, new Vector3(0.8f, 0.4f, 0.8f), 0.3f, CreateMaterial("Spider", new Color(0.22f, 0.05f, 0.35f)));

            PrefabUtility.SaveAsPrefabAsset(inst, SpiderPath);
            Debug.Log("[P1Wiring.Enemies] Spider.prefab OK");
        }
        finally { Object.DestroyImmediate(inst); }
    }

    static void CreateShooter()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPath) != null) { Debug.Log("[P1Wiring.Enemies] Shooter.prefab istnieje, pomijam"); return; }
        var inst = InstantiateBase();
        if (inst == null) return;
        try
        {
            inst.name = "Shooter";
            CommonEnemySetup(inst, 20, 2f);
            SetBool(inst.GetComponent<EnemyMeleeAttack>(), "autoAttack", false);
            GetOrAdd<ShooterController>(inst);
            AddVisual(inst, PrimitiveType.Capsule, Vector3.one * 0.6f, 0.6f, CreateMaterial("Shooter", new Color(0.15f, 0.6f, 0.2f)));

            PrefabUtility.SaveAsPrefabAsset(inst, ShooterPath);
            Debug.Log("[P1Wiring.Enemies] Shooter.prefab OK");
        }
        finally { Object.DestroyImmediate(inst); }
    }

    static GameObject CreateProjectile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        if (existing != null) return existing;

        var go = new GameObject("Projectile");
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * 0.3f;
        visual.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Projectile", new Color(0.6f, 1f, 0.2f));

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.15f;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        go.AddComponent<Projectile>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePath);
        Object.DestroyImmediate(go);
        Debug.Log("[P1Wiring.Enemies] Projectile.prefab OK");
        return prefab;
    }

    // ---------------------------------------------------------------- scene

    static void WireScene(GameObject projectilePrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existing = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Projectile Pool");
        if (existing != null && existing.GetComponent<ProjectilePool>() != null)
        {
            Debug.Log("[P1Wiring.Enemies] Projectile Pool w scenie istnieje, pomijam");
            return;
        }
        var go = existing ?? new GameObject("Projectile Pool");
        var pool = GetOrAdd<ProjectilePool>(go);
        SetRef(pool, "prefab", projectilePrefab.GetComponent<Projectile>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P1Wiring.Enemies] scena OK");
    }
}
