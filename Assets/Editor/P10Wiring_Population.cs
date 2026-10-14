using UnityEditor;
using UnityEngine;

/// <summary>
/// Populacja poziomów (P10): dodaje LevelPopulation do Level 1..8.prefab (obok LevelEnemyTracker) z liczbami i składem.
/// Idempotentne: ustawia pola, nie dubluje komponentu. Menu: LD57 > Wire P10 Population.
/// </summary>
public static class P10Wiring_Population
{
    const string SlimePath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string SpiderPath = "Assets/Prefabs/Enemies/Spider.prefab";
    const string ShooterPath = "Assets/Prefabs/Enemies/Shooter.prefab";

    static readonly int[] TargetAlive = { 6, 8, 9, 10, 11, 12, 13, 8 };
    // (slime, pająk, strzelec) wagi na poziom
    static readonly (int slime, int spider, int shooter)[] Mix =
    {
        (1, 0, 0), (1, 0, 0), (3, 1, 0), (3, 1, 0), (3, 1, 1), (3, 1, 1), (2, 2, 1), (2, 2, 1),
    };

    [MenuItem("LD57/Wire P10 Population")]
    public static void Run()
    {
        var slime = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePath);
        var spider = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);
        var shooter = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPath);
        for (int level = 1; level <= 8; level++)
        {
            string path = $"Assets/Prefabs/Levels/Level {level}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pop = root.GetComponent<LevelPopulation>();
                if (pop == null) pop = root.AddComponent<LevelPopulation>();
                var so = new SerializedObject(pop);
                so.FindProperty("levelNumber").intValue = level;
                so.FindProperty("targetAlive").intValue = TargetAlive[level - 1];
                so.FindProperty("respawnInterval").floatValue = 4f;
                so.FindProperty("spawnDistanceMin").floatValue = 10f;
                so.FindProperty("spawnDistanceMax").floatValue = 20f;
                so.FindProperty("seedMinDistance").floatValue = 8f;
                so.FindProperty("maxTotal").intValue = 0;

                var mix = Mix[level - 1];
                var list = so.FindProperty("entries");
                list.arraySize = 0;
                AddEntry(list, slime, mix.slime);
                AddEntry(list, spider, mix.spider);
                AddEntry(list, shooter, mix.shooter);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[P10Population] Level {level}: targetAlive {TargetAlive[level - 1]}, skład {mix.slime}:{mix.spider}:{mix.shooter}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[P10Population] done");
    }

    static void AddEntry(SerializedProperty list, GameObject prefab, int weight)
    {
        if (prefab == null || weight <= 0) return;
        int i = list.arraySize;
        list.arraySize = i + 1;
        var e = list.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("weight").floatValue = weight;
    }
}
