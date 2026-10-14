using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Nagrania ciosu mieczem (Assets/Audio/Sword/sword_*.wav, pocięte z pliku od hajtuszko) wpinane do
/// ProceduralSfxBank.attackVariants w Michal.unity. Idempotentne; bez plików tylko ostrzega.
/// </summary>
public static class P6Wiring_SwordAudio
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    static readonly string[] ClipPaths = { "Assets/Audio/Sword/sword_1.wav", "Assets/Audio/Sword/sword_2.wav", "Assets/Audio/Sword/sword_3.wav" };

    [MenuItem("LD57/Wire P6 Sword Audio")]
    public static void Run()
    {
        var clips = new List<Object>();
        foreach (var path in ClipPaths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) clips.Add(clip);
            else Debug.LogWarning($"[P6SwordAudio] brak klipu {path}");
        }
        if (clips.Count == 0) { Debug.LogWarning("[P6SwordAudio] brak nagrań miecza, pomijam"); return; }

        var previous = EditorSceneManager.GetActiveScene().path;
        var scene = previous == ScenePath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var bank = Object.FindFirstObjectByType<ProceduralSfxBank>(FindObjectsInactive.Include);
        if (bank == null) { Debug.LogWarning("[P6SwordAudio] brak ProceduralSfxBank w scenie (najpierw Wire P2 Audio)"); return; }
        var so = new SerializedObject(bank);
        var arr = so.FindProperty("attackVariants");
        arr.arraySize = clips.Count;
        for (int i = 0; i < clips.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[P6SwordAudio] attackVariants: {clips.Count} klipy");
    }
}
