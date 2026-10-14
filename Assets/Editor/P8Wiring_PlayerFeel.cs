using UnityEditor;
using UnityEngine;

/// <summary>Podpięcie P8: PlayerFeel na Player.prefab. Menu LD57 > Wire P8 Player Feel. Idempotentne.</summary>
public static class P8Wiring_PlayerFeel
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("LD57/Wire P8 Player Feel")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var feel = root.GetComponent<PlayerFeel>();
            if (feel == null) feel = root.AddComponent<PlayerFeel>();
            var body = root.transform.Find("Body");
            if (body == null) Debug.LogWarning("[P8WiringPlayerFeel] brak dziecka 'Body'");
            SetRef(feel, "player", root.GetComponent<PlayerController>());
            SetRef(feel, "body3d", root.GetComponent<Rigidbody>());
            SetRef(feel, "health", root.GetComponent<Health>());
            SetRef(feel, "body", body);
            SetRef(feel, "skillAnimator", root.GetComponent<PlayerSkillAnimator>());
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P8WiringPlayerFeel] Player.prefab OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P8WiringPlayerFeel] brak pola '{field}'"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
