using Unity.Behavior;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Podpięcie AI wrogów tury "dopracowanie": EnemyAlert na _Enemy Base, SlimeController (bez grafu) w Slime Enemy,
/// wartości domyślne nowych pól. Menu: LD57 > Wire P3 AI. Idempotentne.
/// </summary>
public static class P3WiringAI
{
    const string Folder = "Assets/Prefabs/Enemies";
    const string BasePath = Folder + "/_Enemy Base.prefab";
    const string SlimePath = Folder + "/Slime Enemy.prefab";
    const string ShooterPath = Folder + "/Shooter.prefab";

    [MenuItem("LD57/Wire P3 AI")]
    public static void Run()
    {
        Debug.Log("[P3Wiring.AI] start");
        Edit(BasePath, root =>
        {
            if (root.GetComponent<EnemyAlert>() == null)
                root.AddComponent<EnemyAlert>();
        });

        Edit(SlimePath, root =>
        {
            var graph = root.GetComponent<BehaviorGraphAgent>();
            if (graph != null)
                graph.enabled = false;
            if (root.GetComponent<EnemyAlert>() == null)
                root.AddComponent<EnemyAlert>();
            if (root.GetComponent<SlimeController>() == null)
                root.AddComponent<SlimeController>();
            var melee = root.GetComponent<EnemyMeleeAttack>();
            if (melee != null)
                SetFloat(melee, "retreatAfterAttack", 1.2f);
        });

        Edit(ShooterPath, root =>
        {
            var shooter = root.GetComponent<ShooterController>();
            if (shooter != null)
            {
                SetInt(shooter, "burstCount", 1);
                SetFloat(shooter, "burstInterval", 0.15f);
            }
        });

        // Spider i Boss: nowe pola mają wartości domyślne ze skryptów; sprawdzamy tylko, że prefaby je mają.
        foreach (var path in new[] { Folder + "/Spider.prefab", Folder + "/Boss Spider Mother.prefab" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                Debug.LogWarning("[P3Wiring.AI] brak " + path);
            else if (path.Contains("Spider.prefab") && asset.GetComponent<SpiderController>() == null)
                Debug.LogWarning("[P3Wiring.AI] Spider.prefab bez SpiderController");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P3Wiring.AI] done");
    }

    static void Edit(string path, System.Action<GameObject> edit)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("[P3Wiring.AI] brak " + path);
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P3Wiring.AI] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P3Wiring.AI] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
