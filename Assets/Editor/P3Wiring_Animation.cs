using UnityEditor;
using UnityEngine;

/// <summary>
/// Podpięcie animacji proceduralnej wrogów (P3): VisualRig + animator + HitReaction na prefabach wrogów.
/// Menu LD57 > Wire P3 Animation albo Unity.exe -batchmode -quit -executeMethod P3WiringAnimation.Run. Idempotentne; nie rusza brył.
/// </summary>
public static class P3WiringAnimation
{
    const string Dir = "Assets/Prefabs/Enemies/";

    [MenuItem("LD57/Wire P3 Animation")]
    public static void Run()
    {
        Wire<SlimeAnimator>(Dir + "Slime Enemy.prefab");
        Wire<SpiderAnimator>(Dir + "Spider.prefab");
        Wire<ShooterAnimator>(Dir + "Shooter.prefab");
        Wire<BossAnimator>(Dir + "Boss Spider Mother.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P3WiringAnimation] done");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Wire<TAnimator>(string path) where TAnimator : Component
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning($"[P3WiringAnimation] brak prefabu {path}, pomijam");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GetOrAdd<VisualRig>(root);
            GetOrAdd<TAnimator>(root);
            GetOrAdd<HitReaction>(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[P3WiringAnimation] {path}: VisualRig + {typeof(TAnimator).Name} + HitReaction");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
