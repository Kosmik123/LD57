using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Automatyczne podpięcie backlogu P0 w edytorze (odpowiednik Docs/WIRING.md §1–4 plus poziom 1).
/// Uruchomienie: menu LD57 > Wire P0 albo
///   Unity.exe -batchmode -quit -projectPath . -executeMethod P0Wiring.Run -logFile wiring.log
/// Idempotentne: można uruchamiać wiele razy, istniejące komponenty i obiekty są ponownie używane.
/// </summary>
public static class P0Wiring
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string EnemyBasePrefabPath = "Assets/Prefabs/Enemies/_Enemy Base.prefab";
    const string SlimePrefabPath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string HeartPrefabPath = "Assets/Prefabs/Pickups/Heart.prefab";
    const string SkillsFolder = "Assets/Skills";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    [MenuItem("LD57/Wire P0")]
    public static void Run()
    {
        Debug.Log("[P0Wiring] start");
        var skills = CreateSkillAssets();
        WirePlayerPrefab(skills);
        WireEnemyBasePrefab();
        var heart = CreateHeartPrefab();
        WireScene(heart);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P0Wiring] done");
    }

    // ---------------------------------------------------------------- helpers

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[P0Wiring] brak pola '{field}' w {target.GetType().Name}");
            return;
        }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object target, string field, Object value) => SetProp(target, field, p => p.objectReferenceValue = value);
    static void SetInt(Object target, string field, int value) => SetProp(target, field, p => p.intValue = value);
    static void SetFloat(Object target, string field, float value) => SetProp(target, field, p => p.floatValue = value);
    static void SetBool(Object target, string field, bool value) => SetProp(target, field, p => p.boolValue = value);

    static void SetRefArray(Object target, string field, IList<Object> values) => SetProp(target, field, p =>
    {
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    });

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------------------------------------------------------------- skills

    static SkillTier Tier(string name, string desc, float cd, int dmg, float range, float dur, float kb = 0, float stun = 0)
        => new SkillTier { TierName = name, Description = desc, Cooldown = cd, Damage = dmg, Range = range, Duration = dur, Knockback = kb, Stun = stun };

    static SkillDefinition CreateSkill(string file, string display, SkillKind kind, bool space, SkillTier[] tiers)
    {
        var path = $"{SkillsFolder}/{file}.asset";
        var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        def.DisplayName = display;
        def.Kind = kind;
        def.UsesSpaceSlot = space;
        def.Tiers = tiers;
        EditorUtility.SetDirty(def);
        return def;
    }

    static List<SkillDefinition> CreateSkillAssets()
    {
        EnsureFolder(SkillsFolder);
        var list = new List<SkillDefinition>
        {
            CreateSkill("Spin", "Wirowanie", SkillKind.Spin, false, new[]
            {
                Tier("Baza", "Obrót z mieczem: 2 obrażeń co 0,25 s w promieniu 2,5 m przez 1 s.", 5, 2, 2.5f, 1),
                Tier("Ulepszenie I", "Promień 3 m, 3 obrażeń na tick.", 5, 3, 3f, 1),
                Tier("Ulepszenie II", "Promień 3,5 m, 4 obrażeń na tick.", 5, 4, 3.5f, 1),
                Tier("Ewolucja: Tornado", "Wiruje 2 s i przyciąga wrogów do środka.", 5, 4, 3.5f, 2),
            }),
            CreateSkill("Charge", "Szarża", SkillKind.Charge, false, new[]
            {
                Tier("Baza", "Rzut 6 m do przodu, 5 obrażeń, przebija wrogów. Nietykalny w trakcie.", 4, 5, 6, 0),
                Tier("Ulepszenie I", "Dystans 8 m.", 4, 5, 8, 0),
                Tier("Ulepszenie II", "7 obrażeń.", 4, 7, 8, 0),
                Tier("Ewolucja: Ognista szarża", "Zostawia ślad ognia na 3 s (2 obrażeń na sekundę).", 4, 7, 8, 3),
            }),
            CreateSkill("GroundSlam", "Uderzenie w ziemię", SkillKind.GroundSlam, true, new[]
            {
                Tier("Baza", "Skok (spacja) i uderzenie: 4 obrażeń w promieniu 3 m, odrzut 3 m.", 6, 4, 3, 0.5f, 3),
                Tier("Ulepszenie I", "Promień 4 m.", 6, 4, 4, 0.5f, 3),
                Tier("Ulepszenie II", "6 obrażeń.", 6, 6, 4, 0.5f, 3),
                Tier("Ewolucja: Pęknięcie", "Dodatkowo ogłusza wrogów na 1 s.", 6, 6, 4, 0.5f, 3, 1),
            }),
        };
        Debug.Log("[P0Wiring] skille: " + list.Count);
        return list;
    }

    // ---------------------------------------------------------------- player

    static void WirePlayerPrefab(List<SkillDefinition> skills)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var controller = root.GetComponent<PlayerController>();
            var health = root.GetComponent<Health>();
            SetBool(controller, "jumpEnabled", false);
            SetFloat(controller, "dashCooldown", 0.8f);

            foreach (var blade in root.GetComponentsInChildren<DamagingBlade>(true))
                SetInt(blade, "damage", 2);

            var dashInv = GetOrAdd<DashInvulnerability>(root);
            SetRef(dashInv, "playerController", controller);
            SetRef(dashInv, "health", health);

            GetOrAdd<Experience>(root);
            var slots = GetOrAdd<SkillSlots>(root);
            // Scal z istniejącą pulą (P1 dodaje AxeThrow i WarCry): nie kasuj cudzych wpisów.
            var merged = new List<Object>();
            var poolSo = new SerializedObject(slots);
            var poolProp = poolSo.FindProperty("pool");
            for (int i = 0; i < poolProp.arraySize; i++)
            {
                var existing = poolProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null && !merged.Contains(existing)) merged.Add(existing);
            }
            foreach (var skill in skills)
                if (!merged.Contains(skill)) merged.Add(skill);
            SetRefArray(slots, "pool", merged);

            var user = GetOrAdd<SkillUser>(root);
            SetRef(user, "slots", slots);
            SetRef(user, "player", controller);
            SetProp(user, "enemyLayers", p => p.intValue = ~0);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P0Wiring] Player.prefab OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- enemy

    static void WireEnemyBasePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyBasePrefabPath);
        try
        {
            var health = root.GetComponent<Health>();
            SetInt(health, "maxHealth", 6);
            SetInt(health, "currentHealth", 6);
            var enemyHealth = root.GetComponent<EnemyHealth>();
            SetInt(enemyHealth, "experienceReward", 10);
            SetBool(enemyHealth, "isBoss", false);

            var hitbox = GetOrCreateChild(root, "Attack Hitbox");
            hitbox.transform.localPosition = new Vector3(0, 0.5f, 0.9f);
            var sphere = GetOrAdd<SphereCollider>(hitbox);
            sphere.isTrigger = true;
            sphere.radius = 0.8f;
            var blade = GetOrAdd<DamagingBlade>(hitbox);
            SetProp(blade, "damagedTags", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).stringValue = "Player"; });
            SetInt(blade, "damage", 10);
            SetFloat(blade, "knockback", 0f);
            hitbox.SetActive(false);

            var attack = GetOrAdd<EnemyMeleeAttack>(root);
            SetFloat(attack, "windup", 0.6f);
            SetFloat(attack, "activeTime", 0.2f);
            SetFloat(attack, "cooldown", 1.2f);
            SetFloat(attack, "range", 1.5f);
            SetRef(attack, "damagingObject", hitbox);
            SetBool(attack, "autoAttack", true);

            var death = GetOrAdd<EnemyDeath>(root);
            SetFloat(death, "destroyDelay", 0.5f);

            // Zastępczy wygląd, gdy model wroga nie istnieje w repo (SlimePADefault slime'a nie był commitowany).
            GetOrAdd<PlaceholderVisual>(root);

            PrefabUtility.SaveAsPrefabAsset(root, EnemyBasePrefabPath);
            Debug.Log("[P0Wiring] _Enemy Base.prefab OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- heart

    static GameObject CreateHeartPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(HeartPrefabPath).Replace('\\', '/'));
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HeartPrefabPath);
        if (existing != null) return existing;

        var go = new GameObject("Heart");
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * 0.4f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.9f, 0.1f, 0.2f) };
        AssetDatabase.CreateAsset(mat, "Assets/Prefabs/Pickups/Heart.mat");
        visual.GetComponent<Renderer>().sharedMaterial = mat;

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.6f;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var pickup = go.AddComponent<HeartPickup>();
        SetInt(pickup, "heal", 15);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, HeartPrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log("[P0Wiring] Heart.prefab OK");
        return prefab;
    }

    // ---------------------------------------------------------------- scene

    static void WireScene(GameObject heartPrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        // Po DestroyImmediate tablica roots trzyma martwe referencje, więc Find czyta scenę na świeżo.
        GameObject Find(string name) => scene.GetRootGameObjects().FirstOrDefault(r => r != null && r.name == name);

        var player = Find("Player");
        var levels = Find("Levels");
        if (player == null || levels == null)
        {
            Debug.LogError("[P0Wiring] scena nie ma obiektów Player / Levels");
            return;
        }

        // Instancja poziomu 1 w scenie ma nadpisania (skala 1,5 w X/Z, NavMesh), których prefab nie ma.
        // Wpisz je do prefabu, żeby LevelManager ładował to samo, w co grał zespół.
        var sceneLevel = levels.transform.Find("Level 1");
        if (sceneLevel != null && PrefabUtility.IsPartOfPrefabInstance(sceneLevel.gameObject))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(sceneLevel.gameObject);
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
            {
                PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
                Debug.Log($"[P0Wiring] nadpisania instancji Level 1 wpisane do prefabu (skala {instanceRoot.transform.localScale})");
            }
        }

        // Camera Center leży pod Levels: wyciągnij na root, inaczej LevelManager mógłby go wyczyścić.
        var cameraCenter = levels.transform.Find("Camera Center");
        if (cameraCenter != null)
            cameraCenter.SetParent(null, true);

        // Ręcznie położone slime'y: ich pozycje stają się punktami spawnu stref poziomu 1, potem giną.
        var slimePositions = roots
            .Where(r => r.name.StartsWith("Slime Enemy"))
            .Select(r => r.transform.position)
            .ToList();
        foreach (var r in roots.Where(r => r.name.StartsWith("Slime Enemy")).ToList())
            Object.DestroyImmediate(r);

        var entryPos = player.transform.position;
        WireLevel1Prefab(entryPos, slimePositions, heartPrefab);
        WireOtherLevelPrefabs();

        // Game Manager
        var gm = Find("Game Manager") ?? new GameObject("Game Manager");
        var runState = GetOrAdd<RunState>(gm);
        var gameManager = GetOrAdd<GameManager>(gm);
        SetRef(gameManager, "playerHealth", player.GetComponent<Health>());
        SetRef(gameManager, "runState", runState);

        // Level Manager
        var lm = Find("Level Manager") ?? new GameObject("Level Manager");
        var levelManager = GetOrAdd<LevelManager>(lm);
        var levelPrefabs = new List<Object>();
        for (int i = 1; i <= 8; i++)
            levelPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelsFolder}/Level {i}.prefab"));
        SetRefArray(levelManager, "levelPrefabs", levelPrefabs);
        SetRef(levelManager, "levelsParent", levels.transform);
        SetRef(levelManager, "player", player.GetComponent<Rigidbody>());

        // UI
        WireUi(player, gameManager, runState);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P0Wiring] scena OK");
    }

    // ---------------------------------------------------------------- levels

    static void WireLevel1Prefab(Vector3 entryPos, List<Vector3> slimePositions, GameObject heartPrefab)
    {
        var path = $"{LevelsFolder}/Level 1.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var tracker = GetOrAdd<LevelEnemyTracker>(root);
            SetInt(tracker, "levelNumber", 1);
            RelinkLevelMeshes(root, 1);

            // Scena miała poziom w skali 1,5, ale NavMesh i rozstawienie slime'ów są ze skali 1 (poziom to pierścień,
            // przy 1,5 punkt wejścia wisi w pustce). Skala 1 jest jedyną spójną z upieczonym NavMeshem.
            if (root.transform.localScale != Vector3.one)
            {
                Debug.Log($"[P0Wiring] Level 1: skala {root.transform.localScale} -> 1 (zgodnie z NavMeshem)");
                root.transform.localScale = Vector3.one;
            }
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            if (slimePositions.Count == 0 && root.GetComponentInChildren<WaveZone>() != null)
            {
                // Scena już bez slime'ów, poziom już rozstawiony: nie ruszaj pozycji.
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[P0Wiring] Level 1.prefab: pozycje bez zmian (brak slime'ów w scenie)");
                return;
            }

            var entry = GetOrCreateChild(root, "Level Entry");
            GetOrAdd<LevelEntry>(entry);
            entry.transform.position = entryPos;

            // Wyjście: najdalszy z punktów slime'ów; strefy w 1/3 i 2/3 drogi; spawny = pozostałe punkty.
            var ordered = slimePositions.OrderByDescending(p => Vector3.Distance(p, entryPos)).ToList();
            var exitPos = ordered.Count > 0 ? ordered[0] : entryPos + new Vector3(0, 0, 15);
            var spawnPool = ordered.Skip(1).ToList();
            if (spawnPool.Count == 0) spawnPool.Add(entryPos + (exitPos - entryPos) * 0.5f);

            var slime = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefabPath);
            var zoneCounts = new[] { 2, 2 }; // poziom 1: 4 slime'y w 2 strefach (dokument §8.1)
            for (int z = 0; z < 2; z++)
            {
                var zone = GetOrCreateChild(root, $"Wave Zone {z + 1}");
                zone.transform.position = Vector3.Lerp(entryPos, exitPos, (z + 1) / 3f);
                var box = GetOrAdd<BoxCollider>(zone);
                box.isTrigger = true;
                box.size = new Vector3(6, 3, 6);
                box.center = new Vector3(0, 1.5f, 0);
                var wz = GetOrAdd<WaveZone>(zone);

                var spawnPoint = GetOrCreateChild(zone, "Spawn A");
                spawnPoint.transform.position = spawnPool[z % spawnPool.Count];

                SetProp(wz, "entries", p =>
                {
                    p.arraySize = 1;
                    var e = p.GetArrayElementAtIndex(0);
                    e.FindPropertyRelative("EnemyPrefab").objectReferenceValue = slime;
                    e.FindPropertyRelative("SpawnPoint").objectReferenceValue = spawnPoint.transform;
                    e.FindPropertyRelative("Count").intValue = zoneCounts[z];
                });
                SetRef(wz, "heartPrefab", heartPrefab);
                SetFloat(wz, "spreadRadius", 0.8f);
            }

            var gate = GetOrCreateChild(root, "Exit Gate");
            gate.transform.position = exitPos;
            var trigger = GetOrAdd<BoxCollider>(gate);
            trigger.isTrigger = true;
            trigger.size = new Vector3(3, 3, 3);
            trigger.center = new Vector3(0, 1.5f, 0);
            var bars = GetOrCreateChildPrimitive(gate, "Bars", PrimitiveType.Cube);
            bars.transform.localPosition = new Vector3(0, 1.5f, -1.8f);
            bars.transform.localScale = new Vector3(3, 3, 0.3f);
            var blocker = bars.GetComponent<BoxCollider>();
            var exitGate = GetOrAdd<ExitGate>(gate);
            SetRef(exitGate, "closedVisual", bars);
            SetRef(exitGate, "blocking", blocker);
            SetRef(exitGate, "trigger", trigger);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[P0Wiring] Level 1.prefab OK (entry {entryPos}, exit {exitPos}, spawny {spawnPool.Count})");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject GetOrCreateChildPrimitive(GameObject parent, string name, PrimitiveType type)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <summary>
    /// Cave.fbx został przeeksportowany i stare referencje do siatek w wariantach poziomów się urwały
    /// (MeshCollider.sharedMesh == null, gracz spada przez podłogę). Podpina siatki z Cave.fbx:
    /// najpierw po nazwie zawierającej numer poziomu, inaczej największą.
    /// </summary>
    static void RelinkLevelMeshes(GameObject root, int level)
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Cave.fbx").OfType<Mesh>().ToList();
        if (meshes.Count == 0) { Debug.LogWarning("[P0Wiring] Cave.fbx nie ma siatek"); return; }
        if (level == 1) Debug.Log("[P0Wiring] siatki Cave.fbx: " + string.Join(", ", meshes.Select(m => $"{m.name}({m.vertexCount})")));
        // Nazwy siatek: Level_1.002, Level_2.001, Level_5 ... dopasowanie po "Level_<N>" na początku nazwy.
        var rx = new System.Text.RegularExpressions.Regex(@"^Level_(\d+)(\.|$)");
        Mesh pick = meshes.FirstOrDefault(m => { var mm = rx.Match(m.name); return mm.Success && int.Parse(mm.Groups[1].Value) == level; });
        if (pick == null)
        {
            Debug.LogWarning($"[P0Wiring] Level {level}: brak siatki Level_{level} w Cave.fbx, zostawiam jak jest");
            return;
        }
        var model = root.transform.Find("Model");
        var collider = root.transform.Find("Collider");
        var mfModel = model ? model.GetComponent<MeshFilter>() : null;
        var mcCollider = collider ? collider.GetComponent<MeshCollider>() : null;
        // Tylko urwane referencje; istniejące (z kwietniowego Cave.fbx) zostają.
        if (mfModel != null && mfModel.sharedMesh == null) { mfModel.sharedMesh = pick; Debug.Log($"[P0Wiring] Level {level}: MeshFilter Model <- {pick.name}"); }
        if (mcCollider != null && mcCollider.sharedMesh == null) { mcCollider.sharedMesh = pick; Debug.Log($"[P0Wiring] Level {level}: MeshCollider Collider <- {pick.name}"); }
    }

    /// <summary>Poziomy 2–8: tylko tracker z numerem; strefy, wejście i kratę rozstawia level designer.</summary>
    static void WireOtherLevelPrefabs()
    {
        for (int i = 2; i <= 8; i++)
        {
            var path = $"{LevelsFolder}/Level {i}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tracker = GetOrAdd<LevelEnemyTracker>(root);
                SetInt(tracker, "levelNumber", i);
                RelinkLevelMeshes(root, i);
                if (root.GetComponentInChildren<LevelEntry>() == null)
                {
                    var entry = GetOrCreateChild(root, "Level Entry");
                    GetOrAdd<LevelEntry>(entry);
                    entry.transform.localPosition = new Vector3(100, 1, 0); // TODO(level design): ustaw ręcznie
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[P0Wiring] poziomy 2–8: tracker + Level Entry (pozycje do ustawienia ręcznie)");
    }

    // ---------------------------------------------------------------- UI

    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    static RectTransform Rect(GameObject go) => go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

    static GameObject UiChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void Stretch(GameObject go, float margin = 0)
    {
        var r = Rect(go);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin); r.offsetMax = new Vector2(-margin, -margin);
    }

    static void Anchor(GameObject go, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        var r = Rect(go);
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = size;
        r.anchoredPosition = offset;
    }

    static Image Panel(GameObject parent, string name, Color color, Sprite sprite = null)
    {
        var go = UiChild(parent, name);
        var img = GetOrAdd<Image>(go);
        img.color = color;
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = true;
        return img;
    }

    static TextMeshProUGUI Text(GameObject parent, string name, string text, float size, TextAlignmentOptions align, Color? color = null)
    {
        var go = UiChild(parent, name);
        var tmp = GetOrAdd<TextMeshProUGUI>(go);
        tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void WireUi(GameObject player, GameManager gameManager, RunState runState)
    {
        var canvasGo = GameObject.Find("Game UI") ?? new GameObject("Game UI", typeof(RectTransform));
        var canvas = GetOrAdd<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = GetOrAdd<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(canvasGo);

        var slots = player.GetComponent<SkillSlots>();
        var experience = player.GetComponent<Experience>();
        var playerHealth = player.GetComponent<Health>();

        // --- Death Screen
        var deathGo = UiChild(canvasGo, "Death Screen");
        Stretch(deathGo);
        var deathPanel = Panel(deathGo, "Panel", new Color(0, 0, 0, 0.75f));
        Stretch(deathPanel.gameObject);
        var deathTitle = Text(deathPanel.gameObject, "Title", "Zginąłeś", 72, TextAlignmentOptions.Center);
        Anchor(deathTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1200, 120), new Vector2(0, 140));
        var deathStats = Text(deathPanel.gameObject, "Stats", "", 36, TextAlignmentOptions.Center);
        Anchor(deathStats.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1200, 300), new Vector2(0, -80));
        var death = GetOrAdd<DeathScreen>(deathGo);
        SetRef(death, "root", deathPanel.gameObject);
        SetRef(death, "title", deathTitle);
        SetRef(death, "stats", deathStats);
        SetRef(death, "gameManager", gameManager);
        SetRef(death, "runState", runState);
        deathPanel.gameObject.SetActive(false);

        // --- Level Up Screen
        var lvlGo = UiChild(canvasGo, "Level Up Screen");
        Stretch(lvlGo);
        var lvlRoot = Panel(lvlGo, "Root", new Color(0, 0, 0, 0.6f));
        Stretch(lvlRoot.gameObject);
        var header = Text(lvlRoot.gameObject, "Header", "Level up! Wybierz kartę (1, 2, 3)", 44, TextAlignmentOptions.Center);
        Anchor(header.gameObject, new Vector2(0.5f, 1f), new Vector2(1400, 80), new Vector2(0, -120));
        var cards = UiChild(lvlRoot.gameObject, "Cards");
        Anchor(cards, new Vector2(0.5f, 0.5f), new Vector2(1400, 520), Vector2.zero);
        var layout = GetOrAdd<HorizontalLayoutGroup>(cards);
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

        var buttons = new List<Object>(); var titles = new List<Object>(); var descs = new List<Object>(); var icons = new List<Object>();
        for (int i = 0; i < 3; i++)
        {
            var cardImg = Panel(cards, $"Card {i + 1}", new Color(0.15f, 0.15f, 0.18f, 0.95f), UiSprite);
            Rect(cardImg.gameObject).sizeDelta = new Vector2(400, 500);
            var button = GetOrAdd<Button>(cardImg.gameObject);
            button.targetGraphic = cardImg;
            var colors = button.colors; colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f); colors.pressedColor = new Color(0.5f, 0.5f, 0.6f); button.colors = colors;

            var icon = Panel(cardImg.gameObject, "Icon", Color.white);
            Anchor(icon.gameObject, new Vector2(0.5f, 1f), new Vector2(128, 128), new Vector2(0, -40));
            icon.raycastTarget = false;
            var title = Text(cardImg.gameObject, "Title", $"Karta {i + 1}", 32, TextAlignmentOptions.Center);
            Anchor(title.gameObject, new Vector2(0.5f, 1f), new Vector2(360, 90), new Vector2(0, -200));
            var desc = Text(cardImg.gameObject, "Description", "", 22, TextAlignmentOptions.Top, new Color(0.85f, 0.85f, 0.85f));
            Anchor(desc.gameObject, new Vector2(0.5f, 0f), new Vector2(360, 190), new Vector2(0, 20));
            var hint = Text(cardImg.gameObject, "Key", (i + 1).ToString(), 26, TextAlignmentOptions.Center, new Color(1, 0.85f, 0.4f));
            Anchor(hint.gameObject, new Vector2(0f, 1f), new Vector2(50, 50), new Vector2(10, -10));

            buttons.Add(button); titles.Add(title); descs.Add(desc); icons.Add(icon);
        }
        var lvl = GetOrAdd<LevelUpScreen>(lvlGo);
        SetRef(lvl, "root", lvlRoot.gameObject);
        SetRefArray(lvl, "cardButtons", buttons);
        SetRefArray(lvl, "cardTitles", titles);
        SetRefArray(lvl, "cardDescriptions", descs);
        SetRefArray(lvl, "cardIcons", icons);
        SetRef(lvl, "slots", slots);
        SetRef(lvl, "playerHealth", playerHealth);
        lvlRoot.gameObject.SetActive(false);

        // --- Skill HUD
        var hudGo = UiChild(canvasGo, "Skill HUD");
        Anchor(hudGo, new Vector2(0.5f, 0f), new Vector2(600, 140), new Vector2(0, 20));
        var slotsRow = UiChild(hudGo, "Slots");
        Anchor(slotsRow, new Vector2(0.5f, 0f), new Vector2(400, 90), new Vector2(0, 40));
        var rowLayout = GetOrAdd<HorizontalLayoutGroup>(slotsRow);
        rowLayout.spacing = 16; rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = false; rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = false;

        var keys = new[] { "Q", "E", "R", "SPC" };
        var hud = GetOrAdd<SkillHud>(hudGo);
        var hudSo = new SerializedObject(hud);
        var views = hudSo.FindProperty("views");
        views.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var slotBg = Panel(slotsRow, $"Slot {keys[i]}", new Color(0.1f, 0.1f, 0.12f, 0.8f), UiSprite);
            Rect(slotBg.gameObject).sizeDelta = new Vector2(80, 80);
            var icon = Panel(slotBg.gameObject, "Icon", Color.white);
            Stretch(icon.gameObject, 8);
            var cd = Panel(slotBg.gameObject, "Cooldown", new Color(0, 0, 0, 0.65f), KnobSprite);
            Stretch(cd.gameObject, 4);
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360; cd.fillOrigin = 2; cd.fillClockwise = false; cd.fillAmount = 0;
            var key = Text(slotBg.gameObject, "Key", keys[i], 16, TextAlignmentOptions.TopLeft, new Color(1, 0.85f, 0.4f));
            Stretch(key.gameObject, 4);
            var tier = Text(slotBg.gameObject, "Tier", "", 14, TextAlignmentOptions.BottomRight);
            Stretch(tier.gameObject, 4);

            var v = views.GetArrayElementAtIndex(i);
            v.FindPropertyRelative("root").objectReferenceValue = slotBg.gameObject;
            v.FindPropertyRelative("icon").objectReferenceValue = icon;
            v.FindPropertyRelative("cooldownFill").objectReferenceValue = cd;
            v.FindPropertyRelative("key").objectReferenceValue = key;
            v.FindPropertyRelative("tier").objectReferenceValue = tier;
        }
        var xpBg = Panel(hudGo, "XP Bar", new Color(0, 0, 0, 0.6f), UiSprite);
        Anchor(xpBg.gameObject, new Vector2(0.5f, 0f), new Vector2(400, 14), new Vector2(0, 16));
        var xpFill = Panel(xpBg.gameObject, "XP Fill", new Color(0.4f, 0.8f, 1f), UiSprite);
        Stretch(xpFill.gameObject, 2);
        xpFill.type = Image.Type.Filled; xpFill.fillMethod = Image.FillMethod.Horizontal; xpFill.fillAmount = 0;
        var heroLevel = Text(hudGo, "Hero Level", "Lv 0", 20, TextAlignmentOptions.Left);
        Anchor(heroLevel.gameObject, new Vector2(0.5f, 0f), new Vector2(120, 30), new Vector2(-270, 16));

        hudSo.FindProperty("slots").objectReferenceValue = slots;
        hudSo.FindProperty("experience").objectReferenceValue = experience;
        hudSo.FindProperty("xpFill").objectReferenceValue = xpFill;
        hudSo.FindProperty("heroLevel").objectReferenceValue = heroLevel;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[P0Wiring] UI OK");
    }
}
