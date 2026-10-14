using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Przepieka NavMesh każdego poziomu z faktycznej geometrii colliderów prefabu (skala 1, pozycja 0).
/// Powód: assety NavMesh Level N były pieczone w innym stanie sceny (inna skala/pozycja), przez co
/// NavMesh nie pokrywał się z podłogą i gracz/wrogowie spadali. Wynik: Nav Meshes/NavMesh Level N (auto).asset
/// podpięty do NavMeshSurface w prefabie. Uruchamiać przed P1Wiring_Levels.
///   bash Tools/unity-batch.sh navmesh
/// </summary>
public static class P1Wiring_NavMesh
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string NavFolder = "Assets/Prefabs/Levels/Nav Meshes";

    [MenuItem("LD57/Rebake NavMesh 1-8")]
    public static void Run()
    {
        var previous = SceneManager_ActivePath();
        for (int level = 1; level <= 8; level++)
            Rebake(level);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[P1NavMesh] done");
    }

    static string SceneManager_ActivePath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

    static void Rebake(int level)
    {
        var prefabPath = $"{LevelsFolder}/Level {level}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning($"[P1NavMesh] brak {prefabPath}"); return; }

        // Pusta scena tymczasowa z samą instancją poziomu w (0,0,0), skala 1.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        var surface = instance.GetComponentInChildren<NavMeshSurface>(true);
        if (surface == null)
        {
            Debug.LogWarning($"[P1NavMesh] Level {level}: brak NavMeshSurface");
            return;
        }
        var collider = instance.GetComponentInChildren<MeshCollider>(true);
        if (collider == null || collider.sharedMesh == null)
        {
            Debug.LogWarning($"[P1NavMesh] Level {level}: brak MeshCollider z siatką, pomijam");
            return;
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.navMeshData = null;
        surface.BuildNavMesh();
        var data = surface.navMeshData;
        if (data == null)
        {
            Debug.LogWarning($"[P1NavMesh] Level {level}: BuildNavMesh nie dał danych");
            return;
        }

        var assetPath = $"{NavFolder}/NavMesh Level {level} (auto).asset";
        var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(assetPath);
        if (existing != null) AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();
        var saved = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(assetPath);

        // Podpięcie do prefabu.
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var ps = root.GetComponentInChildren<NavMeshSurface>(true);
            var so = new SerializedObject(ps);
            so.FindProperty("m_NavMeshData").objectReferenceValue = saved;
            so.FindProperty("m_CollectObjects").intValue = (int)CollectObjects.All;
            so.FindProperty("m_UseGeometry").intValue = (int)UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
        Debug.Log($"[P1NavMesh] Level {level}: NavMesh przepieczony, {tri.vertices.Length} wierzchołków, bounds siatki {collider.sharedMesh.bounds.size}");
    }
}
