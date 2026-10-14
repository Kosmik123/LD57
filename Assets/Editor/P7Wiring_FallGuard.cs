using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>FallGuard na obiekcie Level Manager w Michal.unity (gracz spada z krawędzi = powrót + 5 HP). Idempotentne.</summary>
public static class P7Wiring_FallGuard
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P7 Fall Guard")]
    public static void Run()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        var scene = previous == ScenePath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var lm = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (lm == null || player == null) { Debug.LogWarning("[P7FallGuard] brak Level Manager albo gracza w scenie"); return; }
        var guard = lm.GetComponent<FallGuard>();
        if (guard == null) guard = lm.gameObject.AddComponent<FallGuard>();
        var so = new SerializedObject(guard);
        so.FindProperty("player").objectReferenceValue = player.GetComponent<Rigidbody>();
        so.FindProperty("controller").objectReferenceValue = player;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P7FallGuard] FallGuard na Level Manager OK");
    }
}
