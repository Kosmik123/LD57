using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Podpięcie klasy Mag (P9): assety klas (Assets/Classes), 5 skilli maga (Assets/Skills/Mage), PlayerClassApplier i
/// RangedAttack (wyłączony) na Player.prefab, przyciski wyboru klasy w Start Menu w Michal.unity.
/// Menu: LD57 > Wire P9 Mage. Idempotentne: istniejące assety nie są nadpisywane (liczby zostają takie, jak je ustawiono).
/// </summary>
public static class P9Wiring_Mage
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string ClassesFolder = "Assets/Classes";
    const string MageSkillsFolder = "Assets/Skills/Mage";
    const string FemaleModelPath = "Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPolyart.prefab";

    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    [MenuItem("LD57/Wire P9 Mage")]
    public static void Run()
    {
        EnsureFolder(ClassesFolder);
        EnsureFolder(MageSkillsFolder);

        var fireball = CreateSkill("Fireball", "Kula ognia", SkillKind.Fireball, false, new[]
        {
            Tier("Baza", "Kula ognia leci prosto i wybucha: 6 obrażeń w promieniu 2 m.", 4, 6, 2, 0),
            Tier("Ulepszenie I", "Wybuch zadaje 8 obrażeń.", 4, 8, 2, 0),
            Tier("Ulepszenie II", "Wybuch zadaje 10 obrażeń.", 4, 10, 2, 0),
            Tier("Ewolucja: Pożoga", "Po wybuchu ogień płonie jeszcze 1,5 s i trzykrotnie rani wrogów w miejscu wybuchu.", 4, 10, 2, 1.5f),
        });
        var frost = CreateSkill("FrostNova", "Lodowa nova", SkillKind.FrostNova, false, new[]
        {
            Tier("Baza", "Lodowy pierścień w promieniu 3 m: 3 obrażenia i spowolnienie wrogów na 2 s.", 8, 3, 3, 2),
            Tier("Ulepszenie I", "Promień 3,5 m, 4 obrażenia.", 8, 4, 3.5f, 2),
            Tier("Ulepszenie II", "Promień 4 m, 5 obrażeń.", 8, 5, 4, 2),
            Tier("Ewolucja: Zamróz", "Wrogowie w pierścieniu są dodatkowo unieruchomieni na 1,5 s.", 8, 5, 4, 2, 0, 1.5f),
        });
        var orbs = CreateSkill("ArcaneOrbs", "Magiczne kule", SkillKind.ArcaneOrbs, false, new[]
        {
            Tier("Baza", "3 kule krążą wokół ciebie przez 6 s i ranią wrogów (2 obrażenia).", 12, 2, 1.8f, 6),
            Tier("Ulepszenie I", "4 kule, 3 obrażenia.", 12, 3, 1.8f, 6),
            Tier("Ulepszenie II", "5 kul, 4 obrażenia.", 12, 4, 1.8f, 6),
            Tier("Ewolucja: Strażnicy", "Co sekundę jedna z kul strzela w najbliższego wroga.", 12, 4, 1.8f, 6),
        });
        var blink = CreateSkill("Blink", "Teleport", SkillKind.Blink, true, new[]
        {
            Tier("Baza", "Spacja: teleport o 5 m w kierunku ruchu, 0,3 s nietykalności, wybuch w celu (3 obrażenia).", 4, 3, 5, 0.3f),
            Tier("Ulepszenie I", "Teleport o 6 m.", 4, 3, 6, 0.3f),
            Tier("Ulepszenie II", "Teleport o 7 m.", 4, 3, 7, 0.3f),
            Tier("Ewolucja: Rozdarcie", "Wybuch także w miejscu startu.", 4, 3, 7, 0.3f),
        });
        var chain = CreateSkill("ChainLightning", "Łańcuch błyskawic", SkillKind.ChainLightning, false, new[]
        {
            Tier("Baza", "Błyskawica trafia najbliższego wroga (8 m) i skacze na 3 kolejnych: 4 obrażenia, każdy skok słabszy o 1.", 5, 4, 8, 0.15f),
            Tier("Ulepszenie I", "4 skoki, 5 obrażeń.", 5, 5, 8, 0.15f),
            Tier("Ulepszenie II", "5 skoków, 6 obrażeń.", 5, 6, 8, 0.15f),
            Tier("Ewolucja: Burza", "Każde trafienie ogłusza wroga na 0,5 s.", 5, 6, 8, 0.15f, 0, 0.5f),
        });

        var warriorPool = new[]
        {
            Load<SkillDefinition>("Assets/Skills/Spin.asset"),
            Load<SkillDefinition>("Assets/Skills/Charge.asset"),
            Load<SkillDefinition>("Assets/Skills/GroundSlam.asset"),
            Load<SkillDefinition>("Assets/Skills/AxeThrow.asset"),
            Load<SkillDefinition>("Assets/Skills/WarCry.asset"),
        };
        var warrior = CreateClass("Warrior", c =>
        {
            c.DisplayName = "Wojownik";
            c.Description = "Walka wręcz mieczem, 50 HP. Skille: wir, szarża, uderzenie w ziemię, rzut toporem, krzyk bojowy.";
            c.Ranged = false;
            c.AttackDamage = 1;
            c.AttackCooldown = 0.3f;
            c.MoveSpeedMultiplier = 1f;
            c.MaxHealth = 50;
            c.SkillPool = warriorPool;
            c.ModelPrefab = null;
            c.Tint = Color.white;
            c.HideSword = false;
        });
        var mage = CreateClass("Mage", c =>
        {
            c.DisplayName = "Mag";
            c.Description = "Atak z dystansu pociskami, 40 HP. Skille: kula ognia, lodowa nova, magiczne kule, teleport (spacja), łańcuch błyskawic.";
            c.Ranged = true;
            c.AttackDamage = 2;
            c.AttackCooldown = 0.35f;
            c.MoveSpeedMultiplier = 1.05f;
            c.MaxHealth = 40;
            c.SkillPool = new[] { fireball, frost, orbs, blink, chain };
            c.ModelPrefab = Load<GameObject>(FemaleModelPath);
            c.Tint = new Color(0.55f, 0.55f, 1f, 1f);
            c.HideSword = true;
        });
        AssetDatabase.SaveAssets();

        WirePlayer(warrior, mage);
        WireScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[P9Wiring_Mage] OK");
    }

    // ---------------------------------------------------------------- gracz

    static void WirePlayer(PlayerClass warrior, PlayerClass mage)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var applier = GetOrAdd<PlayerClassApplier>(root);
            SetRef(applier, "warrior", warrior);
            SetRef(applier, "mage", mage);
            SetRef(applier, "defaultClass", warrior);

            var ranged = GetOrAdd<RangedAttack>(root);
            SetRef(ranged, "player", root.GetComponent<PlayerController>());
            SetRef(ranged, "body", root.transform.Find("Body"));
            ranged.enabled = false;
            EditorUtility.SetDirty(ranged);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P9Wiring_Mage] Player.prefab OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- menu

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var smGo = FindInScene("Start Menu");
        if (smGo == null)
        {
            Debug.LogError("[P9Wiring_Mage] brak 'Start Menu' w Michal.unity (uruchom najpierw LD57 > Wire P5 Menu)");
            return;
        }
        var sm = smGo.GetComponent<StartMenu>();

        // Układ: klasy y=60, opis y=0, Graj -80, Sterowanie -155, Wyjdź -230.
        var warriorBtn = ClassButton(smGo, "Warrior Button", "Wojownik", new Vector2(-135, 60));
        var mageBtn = ClassButton(smGo, "Mage Button", "Mag", new Vector2(135, 60));
        var desc = UiChild(smGo, "Class Description");
        Anchor(desc, new Vector2(0.5f, 0.5f), new Vector2(760, 70), new Vector2(0, -5));
        var descText = GetOrAdd<TextMeshProUGUI>(desc);
        descText.font = Font;
        descText.fontSize = 24;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.8f, 0.8f, 0.88f);
        descText.raycastTarget = false;
        if (string.IsNullOrEmpty(descText.text)) descText.text = "";

        Move(smGo, "Play Button", new Vector2(0, -80));
        Move(smGo, "Controls Button", new Vector2(0, -155));
        Move(smGo, "Quit Button", new Vector2(0, -230));

        SetRef(sm, "warriorButton", warriorBtn);
        SetRef(sm, "mageButton", mageBtn);
        SetRef(sm, "classDescription", descText);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P9Wiring_Mage] scena OK");
    }

    static GameObject FindInScene(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(t.gameObject))
                return t.gameObject;
        return null;
    }

    static void Move(GameObject parent, string child, Vector2 pos)
    {
        var t = parent.transform.Find(child);
        if (t != null) ((RectTransform)t).anchoredPosition = pos;
    }

    static Button ClassButton(GameObject parent, string name, string label, Vector2 offset)
    {
        var go = UiChild(parent, name);
        Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(240, 56), offset);
        var img = GetOrAdd<Image>(go);
        img.sprite = UiSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.12f, 0.12f, 0.17f, 0.95f);
        var btn = GetOrAdd<Button>(go);
        btn.targetGraphic = img;
        // Kolor zaznaczenia ustawia StartMenu bezpośrednio na grafice; przejście koloru wyłączone, by go nie nadpisywało.
        btn.transition = Selectable.Transition.None;
        var text = UiChild(go, "Label");
        Stretch(text);
        var tmp = GetOrAdd<TextMeshProUGUI>(text);
        tmp.font = Font;
        tmp.text = label;
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }

    // ---------------------------------------------------------------- assety

    static SkillTier Tier(string name, string desc, float cd, int dmg, float range, float dur, float kb = 0, float stun = 0)
        => new SkillTier { TierName = name, Description = desc, Cooldown = cd, Damage = dmg, Range = range, Duration = dur, Knockback = kb, Stun = stun };

    static SkillDefinition CreateSkill(string file, string display, SkillKind kind, bool space, SkillTier[] tiers)
    {
        var path = $"{MageSkillsFolder}/{file}.asset";
        var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (def != null) return def; // nie nadpisujemy ręcznego balansu
        def = ScriptableObject.CreateInstance<SkillDefinition>();
        def.DisplayName = display;
        def.Kind = kind;
        def.UsesSpaceSlot = space;
        def.Tiers = tiers;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    static PlayerClass CreateClass(string file, System.Action<PlayerClass> init)
    {
        var path = $"{ClassesFolder}/{file}.asset";
        var cls = AssetDatabase.LoadAssetAtPath<PlayerClass>(path);
        if (cls != null) return cls;
        cls = ScriptableObject.CreateInstance<PlayerClass>();
        init(cls);
        AssetDatabase.CreateAsset(cls, path);
        return cls;
    }

    static T Load<T>(string path) where T : Object
    {
        var o = AssetDatabase.LoadAssetAtPath<T>(path);
        if (o == null) Debug.LogWarning("[P9Wiring_Mage] brak assetu " + path);
        return o;
    }

    // ---------------------------------------------------------------- helpery

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P9Wiring_Mage] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject UiChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static void Anchor(GameObject go, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = size;
        r.anchoredPosition = offset;
    }
}
