using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Podpięcie menu startowego i pauzy (Docs/WIRING-p5-menu.md): `Start Menu` i `Pause Menu` w `Game UI`
/// tuż przed `Screen Flash`, autoStart = false na `Game Manager`. Menu: LD57 > Wire P5 Menu. Idempotentne.
/// </summary>
public static class P5Wiring_Menu
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    [MenuItem("LD57/Wire P5 Menu")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvasGo = GameObject.Find("Game UI");
        var gmGo = GameObject.Find("Game Manager");
        var player = GameObject.Find("Player");
        if (canvasGo == null || gmGo == null || player == null)
        {
            Debug.LogError("[P5Wiring_Menu] brak 'Game UI', 'Game Manager' lub 'Player' (uruchom najpierw LD57 > Wire P0)");
            return;
        }
        var gm = gmGo.GetComponent<GameManager>();
        SetBool(gm, "autoStart", false);
        var pc = player.GetComponent<PlayerController>();

        var hud = new System.Collections.Generic.List<Object>();
        foreach (var n in new[] { "Skill HUD", "Health Bar" })
        {
            var t = canvasGo.transform.Find(n);
            if (t != null) hud.Add(t.gameObject);
        }

        // --- Start Menu
        var sm = UiChild(canvasGo, "Start Menu");
        Stretch(sm);
        var smGroup = GetOrAdd<CanvasGroup>(sm);
        var smBg = Panel(sm, "Background", new Color(0.02f, 0.02f, 0.04f, 0.85f));
        Stretch(smBg.gameObject);
        smBg.raycastTarget = true;
        var title = Text(sm, "Title", GameTitle.Name, 96, TextAlignmentOptions.Center);
        Anchor(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1000, 130), new Vector2(0, 190));
        var tag = Text(sm, "Tagline", GameTitle.Tagline, 32, TextAlignmentOptions.Center);
        tag.color = new Color(0.75f, 0.75f, 0.8f);
        Anchor(tag.gameObject, new Vector2(0.5f, 0.5f), new Vector2(800, 50), new Vector2(0, 100));
        var play = Button(sm, "Play Button", "Graj (Enter)", new Vector2(0, -10));
        var controls = Button(sm, "Controls Button", "Sterowanie", new Vector2(0, -90));
        var quit = Button(sm, "Quit Button", "Wyjdź", new Vector2(0, -170));

        var cp = UiChild(sm, "Controls Panel");
        Anchor(cp, new Vector2(0.5f, 0.5f), new Vector2(760, 420), new Vector2(0, -20));
        var cpBg = Panel(cp, "Background", new Color(0.04f, 0.04f, 0.07f, 0.97f), UiSprite);
        Stretch(cpBg.gameObject);
        cpBg.raycastTarget = true;
        var cpText = Text(cp, "Text",
            "WSAD<indent=240>ruch\nLPM<indent=240>atak\nShift<indent=240>dash (nietykalność)\nQ  E  R<indent=240>skille\nSpacja<indent=240>uderzenie w ziemię\nEsc<indent=240>pauza",
            34, TextAlignmentOptions.Left);
        Stretch(cpText.gameObject, 40);
        cpText.textWrappingMode = TextWrappingModes.NoWrap;
        cpText.lineSpacing = 20;
        cp.SetActive(false);

        var smc = GetOrAdd<StartMenu>(sm);
        SetRef(smc, "gameManager", gm);
        SetRef(smc, "group", smGroup);
        SetRef(smc, "title", title);
        SetRef(smc, "tagline", tag);
        SetRef(smc, "playButton", play);
        SetRef(smc, "controlsButton", controls);
        SetRef(smc, "quitButton", quit);
        SetRef(smc, "controlsPanel", cp);
        SetRef(smc, "playerController", pc);
        SetRefArray(smc, "hudObjects", hud);

        // --- Pause Menu: komponent na stałym obiekcie, widoczny tylko Root
        var pm = UiChild(canvasGo, "Pause Menu");
        Stretch(pm);
        var pmRoot = UiChild(pm, "Root");
        Stretch(pmRoot);
        var pmGroup = GetOrAdd<CanvasGroup>(pmRoot);
        var pmBg = Panel(pmRoot, "Background", new Color(0.02f, 0.02f, 0.04f, 0.8f));
        Stretch(pmBg.gameObject);
        pmBg.raycastTarget = true;
        var pTitle = Text(pmRoot, "Title", "Pauza", 72, TextAlignmentOptions.Center);
        Anchor(pTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(800, 100), new Vector2(0, 150));
        var resume = Button(pmRoot, "Resume Button", "Wznów (Esc)", new Vector2(0, 30));
        var restart = Button(pmRoot, "Restart Button", "Od nowa", new Vector2(0, -50));
        var pQuit = Button(pmRoot, "Quit Button", "Wyjdź", new Vector2(0, -130));
        var pmc = GetOrAdd<PauseMenu>(pm);
        SetRef(pmc, "gameManager", gm);
        SetRef(pmc, "root", pmRoot);
        SetRef(pmc, "group", pmGroup);
        SetRef(pmc, "resumeButton", resume);
        SetRef(pmc, "restartButton", restart);
        SetRef(pmc, "quitButton", pQuit);
        SetRef(pmc, "playerController", pc);
        pmRoot.SetActive(false);

        // Kolejność: menu tuż przed Screen Flash (Screen Flash najwyżej)
        var flash = canvasGo.transform.Find("Screen Flash");
        int idx = flash != null ? flash.GetSiblingIndex() : canvasGo.transform.childCount;
        pm.transform.SetSiblingIndex(idx - (idx > pm.transform.GetSiblingIndex() ? 1 : 0));
        flash = canvasGo.transform.Find("Screen Flash");
        idx = flash != null ? flash.GetSiblingIndex() : canvasGo.transform.childCount;
        sm.transform.SetSiblingIndex(idx - (idx > sm.transform.GetSiblingIndex() ? 1 : 0));

        // Start Menu aktywne w edytorze (pokazuje się przy starcie); HUD włącza StartMenu, więc zostawiamy je aktywne w scenie.
        sm.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P5Wiring_Menu] scena OK");
    }

    // ---------------------------------------------------------------- helpers

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P5Wiring_Menu] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRefArray(Object target, string field, System.Collections.Generic.IList<Object> values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P5Wiring_Menu] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P5Wiring_Menu] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static RectTransform Rect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
    }

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
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(GameObject parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = UiChild(parent, name);
        var tmp = GetOrAdd<TextMeshProUGUI>(go);
        tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button Button(GameObject parent, string name, string label, Vector2 offset)
    {
        var go = UiChild(parent, name);
        Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(360, 64), offset);
        var img = GetOrAdd<Image>(go);
        img.sprite = UiSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.12f, 0.12f, 0.17f, 0.95f);
        var btn = GetOrAdd<Button>(go);
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
        colors.pressedColor = new Color(0.8f, 0.6f, 0.4f);
        btn.colors = colors;
        var text = Text(go, "Label", label, 34, TextAlignmentOptions.Center);
        Stretch(text.gameObject);
        return btn;
    }
}
