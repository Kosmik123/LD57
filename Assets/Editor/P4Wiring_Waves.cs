using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reżyser fal: spawnInterval i alertOnSpawn na każdej WaveZone w Level 1..8, obiekt Wave Director w Michal.unity.
/// Menu: LD57 > Wire P4 Waves. Idempotentne; nie rusza pozycji ani wpisów stref.
/// </summary>
public static class P4WiringWaves
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P4 Waves")]
    public static void Run()
    {
        Debug.Log("[P4Wiring.Waves] start");
        for (int level = 1; level <= 8; level++)
        {
            string path = $"{LevelsFolder}/Level {level}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("[P4Wiring.Waves] brak " + path);
                continue;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var zone in root.GetComponentsInChildren<WaveZone>(true))
                {
                    var so = new SerializedObject(zone);
                    var delay = so.FindProperty("spawnInterval");
                    var alert = so.FindProperty("alertOnSpawn");
                    if (delay == null || alert == null)
                    {
                        Debug.LogError("[P4Wiring.Waves] WaveZone bez nowych pól (kompilacja?)");
                        continue;
                    }
                    delay.floatValue = 0.9f;
                    alert.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool exists = scene.GetRootGameObjects().Any(r => r != null && r.GetComponentInChildren<WaveDirector>(true) != null);
        if (!exists)
        {
            var go = new GameObject("Wave Director");
            go.AddComponent<WaveDirector>();
            Debug.Log("[P4Wiring.Waves] dodano Wave Director");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P4Wiring.Waves] done");
    }
}
