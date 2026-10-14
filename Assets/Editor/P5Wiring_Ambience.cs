using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Dodaje do sceny Michal obiekt Ambience (ProceduralAmbienceBank + AmbienceController). Idempotentne. Menu: LD57 > Wire P5 Ambience.</summary>
public static class P5WiringAmbience
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P5 Ambience")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var go = scene.GetRootGameObjects().FirstOrDefault(r => r != null && r.name == "Ambience") ?? new GameObject("Ambience");
        if (go.GetComponent<ProceduralAmbienceBank>() == null) go.AddComponent<ProceduralAmbienceBank>();
        if (go.GetComponent<AmbienceController>() == null) go.AddComponent<AmbienceController>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P5WiringAmbience] scena OK");
    }
}
