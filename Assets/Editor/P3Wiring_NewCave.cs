using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migracja poziomów na nowy eksport jaskini: przepina siatki Model (MeshFilter) i Collider (MeshCollider)
/// w prefabach Level 1–8 na siatki z podanego FBX po numerze poziomu (nazwy Level_N, Level_N.001 ...).
/// Po tym trzeba przepiec NavMesh (P1Wiring_NavMesh) i rozstawić poziomy (P1Wiring_Levels, NADPISZ).
///   bash Tools/unity-batch.sh newcave
/// </summary>
public static class P3Wiring_NewCave
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    public const string SourceFbx = "Assets/Models/Cave_new.fbx";

    [MenuItem("LD57/Przepnij poziomy na Cave_new.fbx")]
    public static void Run() => Run(SourceFbx);

    public static void Run(string fbxPath)
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().ToList();
        if (meshes.Count == 0) { Debug.LogError($"[P3NewCave] brak siatek w {fbxPath}"); return; }
        Debug.Log($"[P3NewCave] siatki {fbxPath}: " + string.Join(", ", meshes.Select(m => $"{m.name}({m.vertexCount})")));
        var rx = new Regex(@"^Level_(\d+)(\.|$)");

        for (int level = 1; level <= 8; level++)
        {
            var path = $"{LevelsFolder}/Level {level}.prefab";
            var pick = meshes.FirstOrDefault(m => { var mm = rx.Match(m.name); return mm.Success && int.Parse(mm.Groups[1].Value) == level; });
            if (pick == null) { Debug.LogWarning($"[P3NewCave] Level {level}: brak siatki Level_{level} w {fbxPath}, pomijam"); continue; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.transform.Find("Model");
                var collider = root.transform.Find("Collider");
                var mf = model ? model.GetComponent<MeshFilter>() : null;
                var mc = collider ? collider.GetComponent<MeshCollider>() : null;
                if (mf != null) mf.sharedMesh = pick;
                if (mc != null) mc.sharedMesh = pick;
                // Układ jak w FBX: root prefabu w zerze, Model i Collider dostają pozę węzła Level_N z modelu
                // (wcześniej zostawały transformy starych poziomów i siatki nakładały się na siebie).
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var node = FindNode(fbxPath, level);
                if (node != null)
                {
                    foreach (var t in new[] { model, collider })
                    {
                        if (t == null) continue;
                        t.localPosition = node.position;
                        t.localRotation = node.rotation;
                        t.localScale = node.lossyScale;
                    }
                    Debug.Log($"[P3NewCave] Level {level}: poza z FBX pos={node.position} rot={node.rotation.eulerAngles} scale={node.lossyScale}");
                }
                else
                {
                    Debug.LogWarning($"[P3NewCave] Level {level}: brak węzła Level_{level} w hierarchii {fbxPath}, transform bez zmian");
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[P3NewCave] Level {level}: Model i Collider <- {pick.name} ({pick.vertexCount} wierzchołków)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[P3NewCave] done");
    }

    /// <summary>Węzeł Level_N w hierarchii zaimportowanego modelu (transform względem korzenia modelu).</summary>
    static Transform FindNode(string fbxPath, int level)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) return null;
        var rx = new Regex(@"^Level_(\d+)(\.|$)");
        return model.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => { var m = rx.Match(t.name); return m.Success && int.Parse(m.Groups[1].Value) == level; });
    }

    /// <summary>Cały łańcuch migracji: siatki → NavMesh → rozstawienie wszystkich poziomów (NADPISZ) → reszta podpięć.</summary>
    [MenuItem("LD57/Migracja na Cave_new.fbx (siatki + NavMesh + poziomy)")]
    public static void RunFullMigration()
    {
        Run(SourceFbx);
        P1Wiring_NavMesh.Run();
        P1Wiring_Levels.Run(true, true);
        P2Wiring_Ambience.Run();
        AssetDatabase.SaveAssets();
        Debug.Log("[P3NewCave] migracja zakończona");
    }
}
