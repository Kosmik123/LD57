using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Dodaje do sceny Michal obiekt Sfx (ProceduralSfxBank + SfxHooks). Idempotentne. Menu: LD57 > Wire P2 Audio.</summary>
public static class P2WiringAudio
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P2 Audio")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject FindRoot(string n) => scene.GetRootGameObjects().FirstOrDefault(r => r != null && r.name == n);

        var player = FindRoot("Player");
        var go = FindRoot("Sfx") ?? new GameObject("Sfx");
        if (go.GetComponent<ProceduralSfxBank>() == null) go.AddComponent<ProceduralSfxBank>();
        var hooks = go.GetComponent<SfxHooks>();
        if (hooks == null) hooks = go.AddComponent<SfxHooks>();

        var so = new SerializedObject(hooks);
        so.FindProperty("player").objectReferenceValue = player != null ? player.GetComponent<PlayerController>() : null;
        so.FindProperty("playerHealth").objectReferenceValue = player != null ? player.GetComponent<Health>() : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (player == null) Debug.LogWarning("[P2WiringAudio] brak obiektu Player w scenie");

        var cam = FindRoot("Main Camera") ?? GameObject.Find("Main Camera");
        if (cam != null)
        {
            if (cam.GetComponent<AudioListener>() == null) cam.AddComponent<AudioListener>();
        }
        else Debug.LogWarning("[P2WiringAudio] brak Main Camera");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P2WiringAudio] scena OK");
    }
}
