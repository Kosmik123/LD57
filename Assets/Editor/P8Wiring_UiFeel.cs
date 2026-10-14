using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Podpięcie efektów ekranowych P8 (Docs/WIRING-p8-ui.md): Xp Bar, Hit Direction w `Game UI`,
/// menedżery Level Up Burst i Wave Cleared Toast w scenie. Menu: LD57 > Wire P8 UI Feel. Idempotentne.
/// </summary>
public static class P8Wiring_UiFeel
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";

    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;
    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    [MenuItem("LD57/Wire P8 UI Feel")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        var canvasGo = GameObject.Find("Game UI");
        if (player == null || canvasGo == null)
        {
            Debug.LogError("[P8Wiring_UiFeel] brak 'Player' lub 'Game UI'");
            return;
        }
        var cam = Camera.main;
        var experience = player.GetComponent<Experience>();

        // --- Xp Bar (dolna krawędź, 480x10)
        var xp = UiChild(canvasGo, "Xp Bar");
        var r = Rect(xp);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(480, 10);
        r.anchoredPosition = new Vector2(0, 6);
        var bgGo = UiChild(xp, "Background");
        var bg = GetOrAdd<Image>(bgGo);
        bg.color = new Color(0.02f, 0.03f, 0.05f, 0.75f);
        bg.raycastTarget = false;
        Stretch(bgGo);
        var fillGo = UiChild(xp, "Fill");
        var fill = GetOrAdd<Image>(fillGo);
        fill.sprite = UiSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.color = new Color(0.4f, 0.8f, 1f);
        fill.raycastTarget = false;
        Stretch(fillGo, 1);
        var xpv = GetOrAdd<XpBarView>(xp);
        SetRef(xpv, "experience", experience);
        SetRef(xpv, "fill", fill);
        SetRef(xpv, "font", Font);

        // --- stary pasek XP z SkillHud: wyłączony, SkillHud.xpFill wyzerowany (Lv N zostaje)
        var hud = Object.FindFirstObjectByType<SkillHud>(FindObjectsInactive.Include);
        if (hud != null)
        {
            SetRef(hud, "xpFill", null);
            var old = hud.transform.Find("XP Bar");
            if (old != null) old.gameObject.SetActive(false);
        }

        // --- Hit Direction (cały canvas, pod Xp Bar w hierarchii niepotrzebne)
        var hd = UiChild(canvasGo, "Hit Direction");
        Stretch(hd);
        var hdc = GetOrAdd<HitDirectionIndicator>(hd);
        SetRef(hdc, "playerHealth", player.GetComponent<Health>());

        // --- Menedżery świata
        var lub = GetOrAdd<LevelUpBurst>(FindOrCreate("Level Up Burst"));
        SetRef(lub, "font", Font);
        SetRef(lub, "viewCamera", cam);
        var wct = GetOrAdd<WaveClearedToast>(FindOrCreate("Wave Cleared Toast"));
        SetRef(wct, "font", Font);
        SetRef(wct, "viewCamera", cam);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[P8Wiring_UiFeel] scena OK");
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P8Wiring_UiFeel] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static RectTransform Rect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        return rt != null ? rt : go.AddComponent<RectTransform>();
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
}
