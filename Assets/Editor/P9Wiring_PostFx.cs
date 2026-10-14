using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Mgla wolumetryczna w rendererze PC (feature z Assets/Volumetric) + obiekt "Post Fx Pulse" w scenie Michal.
/// Idempotentne. Komponent Volume mgly ustawia LevelAmbience.Apply().
/// </summary>
public static class P9Wiring_PostFx
{
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string ObjectName = "Post Fx Pulse";

    [MenuItem("LD57/Wire P9 PostFx")]
    public static void Run()
    {
        try { EnsureFogFeature(); }
        catch (System.Exception e) { Debug.LogWarning("[P9PostFx] feature mgly: " + e.Message); }
        try { WireScene(); }
        catch (System.Exception e) { Debug.LogWarning("[P9PostFx] scena: " + e.Message); }
        AssetDatabase.SaveAssets();
        Debug.Log("[P9PostFx] done");
    }

    static void EnsureFogFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null) { Debug.LogWarning("[P9PostFx] brak " + RendererPath); return; }
        foreach (var f in data.rendererFeatures)
            if (f is VolumetricFogRendererFeature) { Debug.Log("[P9PostFx] feature mgly juz jest"); return; }

        var feature = ScriptableObject.CreateInstance<VolumetricFogRendererFeature>();
        feature.name = "VolumetricFogRendererFeature";
        AssetDatabase.AddObjectToAsset(feature, data);

        var so = new SerializedObject(data);
        var list = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[P9PostFx] dodano VolumetricFogRendererFeature do PC_Renderer");
    }

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var go = GameObject.Find(ObjectName);
        if (go == null) go = new GameObject(ObjectName);
        if (go.GetComponent<PostFxPulse>() == null) go.AddComponent<PostFxPulse>();

        var camGo = GameObject.Find("Main Camera");
        var cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (!data.renderPostProcessing) data.renderPostProcessing = true;
        }
        else Debug.LogWarning("[P9PostFx] brak Main Camera");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
