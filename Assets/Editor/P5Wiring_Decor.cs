using UnityEditor;
using UnityEngine;

/// <summary>Dodaje CaveDecor (z numerem poziomu) do Level 1..8.prefab. Idempotentne, nie rusza innych dzieci.</summary>
public static class P5Wiring_Decor
{
    const string LevelsFolder = "Assets/Prefabs/Levels";

    [MenuItem("LD57/Wire P5 Decor")]
    public static void Run()
    {
        for (int level = 1; level <= 8; level++) WireLevel(level);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P5Decor] done");
    }

    static void WireLevel(int level)
    {
        var path = $"{LevelsFolder}/Level {level}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[P5Decor] brak {path}"); return; }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var decor = root.GetComponent<CaveDecor>();
            if (decor == null) decor = root.AddComponent<CaveDecor>();
            var so = new SerializedObject(decor);
            var p = so.FindProperty("levelNumber");
            if (p.intValue != level) { p.intValue = level; so.ApplyModifiedPropertiesWithoutUndo(); }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
