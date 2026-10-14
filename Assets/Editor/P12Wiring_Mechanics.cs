using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Podpięcie P12 (nowe mechaniki: elity, esencja, skrzynie z reliktami, Furia), patrz Docs/WIRING-p12-mechanics.md:
/// 10 assetów RelicDefinition w Assets/Relics (tylko brakujące, istniejących nie nadpisuje), PlayerStats + RelicHolder + FurySystem
/// na Player.prefab, w scenie: Elite Director, a w Game UI: Relic Screen, Fury Meter, Relic Bar. Idempotentne.
/// Menu: LD57 > Wire P12 Mechanics.
/// </summary>
public static class P12Wiring_Mechanics
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string PlayerPath = "Assets/Prefabs/Player.prefab";
    const string HeartPath = "Assets/Prefabs/Pickups/Heart.prefab";
    const string RelicDir = "Assets/Relics";

    struct RelicSpec
    {
        public string File, Name, Description, Letter;
        public RelicEffect Effect;
        public float Value;
        public Color Color;
        public RelicSpec(string file, string name, string desc, string letter, RelicEffect effect, float value, Color color)
        { File = file; Name = name; Description = desc; Letter = letter; Effect = effect; Value = value; Color = color; }
    }

    static readonly RelicSpec[] Specs =
    {
        new RelicSpec("Relic IronWill", "Żelazna wola", "+15% obrażeń.", "W", RelicEffect.IronWill, 15f, new Color(0.9f, 0.35f, 0.2f)),
        new RelicSpec("Relic LightFeet", "Lekkie stopy", "+12% prędkości ruchu.", "L", RelicEffect.LightFeet, 12f, new Color(0.45f, 0.85f, 0.4f)),
        new RelicSpec("Relic VampireFang", "Wampirzy kieł", "3% zadanych obrażeń wraca jako zdrowie.", "V", RelicEffect.VampireFang, 3f, new Color(0.8f, 0.1f, 0.25f)),
        new RelicSpec("Relic Thorns", "Kolce", "Napastnik w zwarciu dostaje 2 obrażeń, gdy cię trafi.", "K", RelicEffect.Thorns, 2f, new Color(0.65f, 0.55f, 0.35f)),
        new RelicSpec("Relic Luck", "Szczęście", "10% szans na cios krytyczny (podwójne obrażenia).", "S", RelicEffect.Luck, 10f, new Color(1f, 0.85f, 0.25f)),
        new RelicSpec("Relic SecondWind", "Drugi oddech", "Serce leczy o 10 HP więcej.", "O", RelicEffect.SecondWind, 10f, new Color(1f, 0.45f, 0.65f)),
        new RelicSpec("Relic Greed", "Zachłanność", "Esencja daje podwójnie (licznik i XP).", "Z", RelicEffect.Greed, 2f, new Color(0.3f, 0.85f, 0.95f)),
        new RelicSpec("Relic EliteHunter", "Łowca elit", "+40% obrażeń elitom.", "H", RelicEffect.EliteHunter, 40f, new Color(0.75f, 0.3f, 0.9f)),
        new RelicSpec("Relic ThickSkin", "Twarda skóra", "-15% otrzymywanych obrażeń.", "T", RelicEffect.ThickSkin, 15f, new Color(0.6f, 0.65f, 0.75f)),
        new RelicSpec("Relic Echo", "Echo", "10% szans, że skill nie wejdzie na cooldown.", "E", RelicEffect.Echo, 10f, new Color(0.55f, 0.5f, 1f)),
    };

    [MenuItem("LD57/Wire P12 Mechanics")]
    public static void Run()
    {
        Debug.Log("[P12Mechanics] start");
        EnsureRelics();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WirePlayer();
        WireScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[P12Mechanics] done");
    }

    // ---------------------------------------------------------------- assety reliktów

    static void EnsureRelics()
    {
        if (!AssetDatabase.IsValidFolder(RelicDir))
            AssetDatabase.CreateFolder("Assets", "Relics");
        foreach (var spec in Specs)
        {
            string path = $"{RelicDir}/{spec.File}.asset";
            var def = AssetDatabase.LoadAssetAtPath<RelicDefinition>(path);
            if (def != null) continue; // istniejący asset (ręczne zmiany) zostaje
            def = ScriptableObject.CreateInstance<RelicDefinition>();
            def.DisplayName = spec.Name;
            def.Description = spec.Description;
            def.Letter = spec.Letter;
            def.Color = spec.Color;
            def.Effect = spec.Effect;
            def.Value = spec.Value;
            AssetDatabase.CreateAsset(def, path);
            Debug.Log("[P12Mechanics] relikt: " + spec.Name);
        }
    }

    static List<RelicDefinition> AllRelics()
    {
        var list = new List<RelicDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:RelicDefinition", new[] { RelicDir }))
        {
            var def = AssetDatabase.LoadAssetAtPath<RelicDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) list.Add(def);
        }
        return list;
    }

    // ---------------------------------------------------------------- Player.prefab

    static void WirePlayer()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var stats = GetOrAdd<PlayerStats>(root);
            var holder = GetOrAdd<RelicHolder>(root);
            var fury = GetOrAdd<FurySystem>(root);

            SetRef(holder, "stats", stats);
            SetRef(holder, "skillUser", root.GetComponent<SkillUser>());
            SetRef(holder, "skillSlots", root.GetComponent<SkillSlots>());
            var relics = AllRelics();
            var so = new SerializedObject(holder);
            var pool = so.FindProperty("pool");
            pool.arraySize = relics.Count;
            for (int i = 0; i < relics.Count; i++)
                pool.GetArrayElementAtIndex(i).objectReferenceValue = relics[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(fury, "stats", stats);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log($"[P12Mechanics] Player.prefab: PlayerStats, RelicHolder ({relics.Count} reliktów), FurySystem");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- scena

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        var canvasGo = GameObject.Find("Game UI");
        if (player == null || canvasGo == null)
        {
            Debug.LogError("[P12Mechanics] brak 'Player' lub 'Game UI' w scenie (uruchom najpierw LD57 > Wire P0)");
            return;
        }
        var holder = player.GetComponent<RelicHolder>();
        var fury = player.GetComponent<FurySystem>();
        if (holder == null || fury == null)
            Debug.LogWarning("[P12Mechanics] Player w scenie nie ma RelicHolder/FurySystem (nadpisanie prefabu?); komponenty znajdą się w runtime przez Instance");

        // Elite Director
        var directorGo = GameObject.Find("Elite Director");
        if (directorGo == null) directorGo = new GameObject("Elite Director");
        var director = GetOrAdd<EliteDirector>(directorGo);
        SetRef(director, "heartPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(HeartPath));

        // HUD w Game UI
        var screenGo = UiChild(canvasGo, "Relic Screen");
        var screen = GetOrAdd<RelicScreen>(screenGo);
        if (holder != null) SetRef(screen, "holder", holder);
        screenGo.transform.SetAsLastSibling(); // na wierzchu

        var furyGo = UiChild(canvasGo, "Fury Meter");
        var meter = GetOrAdd<FuryMeter>(furyGo);
        if (fury != null) SetRef(meter, "fury", fury);

        var barGo = UiChild(canvasGo, "Relic Bar");
        var bar = GetOrAdd<RelicBar>(barGo);
        if (holder != null) SetRef(bar, "holder", holder);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P12Mechanics] scena OK (Elite Director, Relic Screen, Fury Meter, Relic Bar)");
    }

    // ---------------------------------------------------------------- helpery

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) return c;
        return go.AddComponent<T>();
    }

    static GameObject UiChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform)); // RectTransform od razu: TMP/Image nie podmieniają Transform
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SetRef(Object target, string field, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P12Mechanics] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
