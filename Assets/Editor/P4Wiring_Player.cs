using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Podpięcie P4 (feel gracza i kamera). Menu LD57 > Wire P4 Player. Idempotentne.</summary>
public static class P4Wiring_Player
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string MatFolder = "Assets/Materials/Player";

    [MenuItem("LD57/Wire P4 Player")]
    public static void Run()
    {
        Debug.Log("[P4WiringPlayer] start");
        var trailMat = EnsureMaterial("SwordTrail", new Color(1f, 0.7f, 0.3f, 1f));
        var ghostMat = EnsureMaterial("DashAfterimage", Color.white);
        WirePlayer(trailMat, ghostMat);
        WireScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[P4WiringPlayer] done");
    }

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P4WiringPlayer] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetRef(Object t, string f, Object v) => SetProp(t, f, p => p.objectReferenceValue = v);
    static void SetFloat(Object t, string f, float v) => SetProp(t, f, p => p.floatValue = v);

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Material EnsureMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Player");
        var path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = SwordTrail.CreateTransparentMaterial(name, color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static void WirePlayer(Material trailMat, Material ghostMat)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var controller = root.GetComponent<PlayerController>();
            var body = root.transform.Find("Body");
            var sword = FindDeep(root.transform, "SWORD");
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (body == null) Debug.LogWarning("[P4WiringPlayer] brak dziecka 'Body'");
            if (sword == null) Debug.LogWarning("[P4WiringPlayer] brak obiektu 'SWORD'");
            if (smr == null) Debug.LogWarning("[P4WiringPlayer] brak SkinnedMeshRenderer");

            var trail = GetOrAdd<SwordTrail>(root);
            SetRef(trail, "player", controller);
            SetRef(trail, "sword", sword);
            SetRef(trail, "blade", sword != null ? sword.GetComponentInChildren<DamagingBlade>(true) : null);
            SetRef(trail, "trailMaterial", trailMat);

            var after = GetOrAdd<DashAfterimage>(root);
            SetRef(after, "player", controller);
            SetRef(after, "skinnedRenderer", smr);
            SetRef(after, "ghostMaterial", ghostMat);

            var anim = GetOrAdd<PlayerSkillAnimator>(root);
            SetRef(anim, "skillUser", root.GetComponent<SkillUser>());
            SetRef(anim, "player", controller);
            SetRef(anim, "body", body);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P4WiringPlayer] Player.prefab OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            SetFloat(cam, "lookAhead", 1.5f);
            var so = new SerializedObject(cam);
            var zoom = so.FindProperty("ZoomMultiplier");
            if (zoom != null) { zoom.floatValue = 1f; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(cam);
        }
        else Debug.LogWarning("[P4WiringPlayer] brak CameraController w scenie");

        var canvas = GameObject.Find("Game UI");
        if (canvas != null)
        {
            var t = canvas.transform.Find("Low Health Overlay");
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject("Low Health Overlay", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
            }
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = GetOrAdd<Image>(go);
            img.raycastTarget = false;
            img.color = new Color(0.8f, 0.03f, 0.03f, 0f);
            GetOrAdd<LowHealthOverlay>(go);
            // Pod pozostałymi elementami HUD (winieta nie ma zasłaniać UI).
            go.transform.SetAsFirstSibling();
        }
        else Debug.LogWarning("[P4WiringPlayer] brak 'Game UI' (uruchom najpierw LD57 > Wire P0)");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P4WiringPlayer] scena OK");
    }
}
