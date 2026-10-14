using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Podpięcie bossa i feelu (P1): prefab Boss Spider Mother, DamageFlash na wrogach, knockback gracza,
/// obiekty feelu w scenie Michal. Uruchomienie: menu LD57 > Wire P1 Boss albo
///   Unity.exe -batchmode -quit -projectPath . -executeMethod P1WiringBoss.Run -logFile boss.log
/// Idempotentne.
/// </summary>
public static class P1WiringBoss
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string EnemyBasePrefabPath = "Assets/Prefabs/Enemies/_Enemy Base.prefab";
    const string SlimePrefabPath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string SpiderPrefabPath = "Assets/Prefabs/Enemies/Spider.prefab";
    const string BossPrefabPath = "Assets/Prefabs/Enemies/Boss Spider Mother.prefab";
    const string BossMaterialPath = "Assets/Prefabs/Enemies/Boss Spider Mother.mat";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P1 Boss")]
    public static void Run()
    {
        Debug.Log("[P1WiringBoss] start");
        WireEnemyBaseFlash();
        WirePlayerKnockback();
        CreateBossPrefab();
        WireScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P1WiringBoss] done");
    }

    // ---------------------------------------------------------------- helpers

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[P1WiringBoss] brak pola '{field}' w {target.GetType().Name}");
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

    // ---------------------------------------------------------------- prefaby

    static void WireEnemyBaseFlash()
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyBasePrefabPath);
        try
        {
            GetOrAdd<DamageFlash>(root);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyBasePrefabPath);
            Debug.Log("[P1WiringBoss] _Enemy Base.prefab: DamageFlash OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WirePlayerKnockback()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            foreach (var blade in root.GetComponentsInChildren<DamagingBlade>(true))
                SetFloat(blade, "knockback", 0.5f);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P1WiringBoss] Player.prefab: knockback 0.5 OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CreateBossPrefab()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBasePrefabPath);
        if (basePrefab == null) { Debug.LogError("[P1WiringBoss] brak _Enemy Base.prefab"); return; }

        var minion = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath)
                     ?? AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefabPath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(BossMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, BossMaterialPath);
        }
        var dark = new Color(0.35f, 0.02f, 0.04f);
        material.color = dark;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dark);
        EditorUtility.SetDirty(material);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            instance.name = "Boss Spider Mother";

            var graph = instance.GetComponent<Unity.Behavior.BehaviorGraphAgent>();
            if (graph != null) graph.enabled = false;

            var boss = GetOrAdd<SpiderMotherBoss>(instance);
            SetRef(boss, "minionPrefab", minion);

            var health = instance.GetComponent<Health>();
            SetInt(health, "maxHealth", 120);
            SetInt(health, "currentHealth", 120);
            var enemyHealth = instance.GetComponent<EnemyHealth>();
            SetBool(enemyHealth, "isBoss", true);
            SetInt(enemyHealth, "experienceReward", 0);

            var agent = instance.GetComponent<NavMeshAgent>();
            agent.speed = 4f;
            agent.radius = 1.2f;
            agent.height = 1.5f;

            // Atak: większe ciało, więc większy zasięg i hitbox przesunięty przed boss'a.
            var attack = instance.GetComponent<EnemyMeleeAttack>();
            SetFloat(attack, "windup", 1.0f);
            SetFloat(attack, "range", 2.5f);
            SetBool(attack, "autoAttack", true);

            var hitbox = instance.transform.Find("Attack Hitbox");
            if (hitbox != null)
            {
                hitbox.localPosition = new Vector3(0, 0.5f, 1.5f);
                var sphere = hitbox.GetComponent<SphereCollider>();
                if (sphere != null) sphere.radius = 1.4f;
                var blade = hitbox.GetComponent<DamagingBlade>();
                if (blade != null) SetInt(blade, "damage", 15);
            }
            else Debug.LogWarning("[P1WiringBoss] brak 'Attack Hitbox' w _Enemy Base");

            // Placeholder: ciemnoczerwona kula.
            var visual = instance.transform.Find("Visual");
            if (visual == null)
            {
                var sphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereGo.name = "Visual";
                sphereGo.transform.SetParent(instance.transform, false);
                visual = sphereGo.transform;
            }
            visual.localPosition = new Vector3(0, 0.75f, 0);
            visual.localScale = new Vector3(3f, 1.5f, 3f);
            visual.GetComponent<Renderer>().sharedMaterial = material;

            GetOrAdd<DamageFlash>(instance);

            PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
            Debug.Log($"[P1WiringBoss] {BossPrefabPath} OK (minion: {(minion != null ? minion.name : "brak")})");
        }
        finally { Object.DestroyImmediate(instance); }
    }

    // ---------------------------------------------------------------- scena

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject FindRoot(string name) => scene.GetRootGameObjects().FirstOrDefault(r => r != null && r.name == name);

        var player = FindRoot("Player");
        var playerHealth = player != null ? player.GetComponent<Health>() : null;

        // Hit Feedback
        var hf = FindRoot("Hit Feedback") ?? new GameObject("Hit Feedback");
        GetOrAdd<HitStop>(hf);
        var feedback = GetOrAdd<HitFeedback>(hf);
        SetRef(feedback, "playerHealth", playerHealth);

        // Camera Shake
        var cam = FindRoot("Main Camera") ?? GameObject.Find("Main Camera");
        if (cam != null && cam.GetComponent<CameraController>() != null)
            GetOrAdd<CameraShake>(cam);
        else
            Debug.LogWarning("[P1WiringBoss] brak Main Camera z CameraController, CameraShake nie dodany");

        // Screen Flash w Game UI
        var canvas = FindRoot("Game UI");
        if (canvas != null)
        {
            var t = canvas.transform.Find("Screen Flash");
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject("Screen Flash", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
            }
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = GetOrAdd<Image>(go);
            img.color = new Color(0.8f, 0.05f, 0.05f, 0f);
            img.raycastTarget = false;
            GetOrAdd<ScreenFlash>(go);
        }
        else Debug.LogWarning("[P1WiringBoss] brak 'Game UI' (uruchom najpierw LD57 > Wire P0)");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P1WiringBoss] scena OK");
    }
}
