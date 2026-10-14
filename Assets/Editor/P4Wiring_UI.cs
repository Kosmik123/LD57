using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Podpięcie UI P4 w scenie (Docs/WIRING-p4-ui.md): Health Bar, Boss Health Bar, Enemy Health Bars, Alert Markers
/// w `Game UI`, wyłączenie starego `Status Bar`. Menu: LD57 > Wire P4 UI. Idempotentne.
/// </summary>
public static class P4Wiring_UI
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    [MenuItem("LD57/Wire P4 UI")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        var canvasGo = GameObject.Find("Game UI");
        if (player == null || canvasGo == null)
        {
            Debug.LogError("[P4Wiring_UI] brak 'Player' lub 'Game UI' (uruchom najpierw LD57 > Wire P0)");
            return;
        }
        var cam = Camera.main;

        // --- Health Bar (lewy górny róg, 320x22)
        var hb = UiChild(canvasGo, "Health Bar");
        Anchor(hb, new Vector2(0f, 1f), new Vector2(320, 22), new Vector2(24, -24));
        var bg = Panel(hb, "Background", new Color(0.05f, 0.05f, 0.07f, 0.85f));
        Stretch(bg.gameObject);
        var delayed = FilledPanel(hb, "Delayed Fill", Color.white);
        var fill = FilledPanel(hb, "Fill", new Color(0.85f, 0.1f, 0.12f));
        var hbv = GetOrAdd<HealthBarView>(hb);
        SetRef(hbv, "health", player.GetComponent<Health>());
        SetRef(hbv, "delayedFill", delayed);
        SetRef(hbv, "fill", fill);

        // --- Boss Health Bar (komponent na obiekcie zawsze aktywnym, widoczny tylko Panel)
        var bb = UiChild(canvasGo, "Boss Health Bar");
        Stretch(bb);
        bb.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        var panel = UiChild(bb, "Panel");
        Anchor(panel, new Vector2(0.5f, 1f), new Vector2(600, 18), new Vector2(0, -60));
        var bbBg = Panel(panel, "Background", new Color(0.05f, 0.05f, 0.07f, 0.85f));
        Stretch(bbBg.gameObject);
        var bossFill = FilledPanel(panel, "Fill", new Color(0.7f, 0.1f, 0.15f));
        foreach (var (name, pct) in new[] { ("Phase Marker 60", 0.6f), ("Phase Marker 30", 0.3f) })
        {
            var m = Panel(panel, name, Color.white);
            m.raycastTarget = false;
            var r = m.rectTransform;
            r.anchorMin = new Vector2(pct, 0f); r.anchorMax = new Vector2(pct, 1f);
            r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(3, 6); r.anchoredPosition = Vector2.zero;
        }
        var nameLabel = Text(panel, "Name", "Matka pająków", 24, TextAlignmentOptions.Center);
        Anchor(nameLabel.gameObject, new Vector2(0.5f, 0f), new Vector2(600, 30), new Vector2(0, -20));
        var bossBar = GetOrAdd<BossHealthBar>(bb);
        SetRef(bossBar, "panel", panel);
        SetRef(bossBar, "fill", bossFill);
        SetRef(bossBar, "nameLabel", nameLabel);
        panel.SetActive(false);

        // --- Menedżery world-space
        var ehb = FindOrCreate("Enemy Health Bars");
        var ehbc = GetOrAdd<EnemyHealthBars>(ehb);
        SetRef(ehbc, "viewCamera", cam);

        var am = FindOrCreate("Alert Markers");
        var amc = GetOrAdd<AlertMarkers>(am);
        SetRef(amc, "viewCamera", cam);
        SetRef(amc, "font", Font);

        // --- LevelUpScreen: nagłówek do pulsu
        var lvl = Object.FindFirstObjectByType<LevelUpScreen>(FindObjectsInactive.Include);
        if (lvl != null)
        {
            var header = lvl.transform.Find("Root/Header");
            if (header != null) SetRef(lvl, "header", header);
        }

        // --- Stary pasek HP
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Status Bar" && t.gameObject.activeSelf)
                t.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P4Wiring_UI] scena OK");
    }

    // ---------------------------------------------------------------- helpers

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P4Wiring_UI] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
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

    /// <summary>Image typu Filled (poziomo od lewej) rozciągnięty z marginesem 2 px. Sprite z UISprite: Filled działa na sprite'ach nie-sliced.</summary>
    static Image FilledPanel(GameObject parent, string name, Color color)
    {
        var img = Panel(parent, name, color, null);
        // Filled wymaga sprite'a, inaczej fillAmount nie jest rysowany; używamy wbudowanego UISprite.
        img.sprite = UiSprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        Stretch(img.gameObject, 2);
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
}
