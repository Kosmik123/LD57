using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Biały sprite 4×4 generowany w kodzie (Image typu Filled wymaga sprite'a; UI robione w kodzie nie zależy od zasobów edytora).</summary>
public static class UiSprites
{
    private static Sprite white;

    public static Sprite White
    {
        get
        {
            if (white != null) return white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "UI White (runtime)", filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            white.name = "UI White";
            return white;
        }
    }

    public static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image AddImage(Transform parent, string name, Color color)
    {
        var rt = Child(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = White;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
    {
        var rt = Child(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static void Stretch(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}

/// <summary>
/// Pasek Furii w HUD (dół ekranu, nad kołem skilli): wypełnia się zabójstwami (8 w oknie 4 s), opada po oknie, w Furii
/// pokazuje pozostały czas i pulsuje, po Furii szarzeje na czas przerwy. UI budowane w kodzie w Awake pod własnym
/// RectTransformem (obiekt „Fury Meter” w Game UI tworzy P12Wiring_Mechanics); pozycję zmienia się na obiekcie.
/// </summary>
public class FuryMeter : MonoBehaviour
{
    [SerializeField] private FurySystem fury;
    [SerializeField, Tooltip("Rozmiar paska (px).")] private Vector2 size = new Vector2(320f, 16f);
    [SerializeField, Tooltip("Odsunięcie środka od dołu ekranu (px).")] private float bottomOffset = 160f;

    private static readonly Color Back = new Color(0.05f, 0.04f, 0.06f, 0.85f);
    private static readonly Color Building = new Color(1f, 0.55f, 0.12f);
    private static readonly Color Hot = new Color(1f, 0.82f, 0.25f);
    private static readonly Color Cool = new Color(0.45f, 0.45f, 0.5f);

    private RectTransform root;
    private Image fill, windowLine, glow;
    private TextMeshProUGUI label;
    private CanvasGroup group;
    private float shown;
    private bool built;

    private void Awake() => Build();

    private void Build()
    {
        if (built) return;
        built = true;
        root = (RectTransform)transform;
        UiSprites.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), size, new Vector2(0f, bottomOffset));
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        glow = UiSprites.AddImage(root, "Glow", new Color(1f, 0.55f, 0.12f, 0f));
        UiSprites.Stretch(glow.rectTransform, -6f);
        var bg = UiSprites.AddImage(root, "Background", Back);
        UiSprites.Stretch(bg.rectTransform);
        fill = UiSprites.AddImage(root, "Fill", Building);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        UiSprites.Stretch(fill.rectTransform, 2f);
        windowLine = UiSprites.AddImage(root, "Window", new Color(1f, 1f, 1f, 0.8f));
        windowLine.type = Image.Type.Filled;
        windowLine.fillMethod = Image.FillMethod.Horizontal;
        windowLine.fillAmount = 0f;
        UiSprites.Place(windowLine.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(size.x - 4f, 2f), new Vector2(2f, -1f));
        label = UiSprites.AddText(root, "Label", "FURIA", 20f, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.4f));
        label.fontStyle = FontStyles.Bold;
        UiSprites.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(size.x, 28f), new Vector2(0f, 2f));
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        Build();
        Hook(true);
    }

    private void OnDisable() => Hook(false);

    private FurySystem hooked;

    private void Hook(bool on)
    {
        if (hooked != null)
        {
            hooked.OnActivated -= Fury_OnActivated;
            hooked = null;
        }
        if (!on) return;
        if (fury == null) fury = FurySystem.Instance;
        if (fury == null) return;
        hooked = fury;
        hooked.OnActivated += Fury_OnActivated;
    }

    private void Fury_OnActivated()
    {
        root.DOKill(true);
        root.localScale = Vector3.one;
        root.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f).SetUpdate(true).SetTarget(root);
    }

    private void Update()
    {
        if (fury == null || hooked != fury)
        {
            if (FurySystem.Instance == null) { group.alpha = 0f; return; }
            fury = FurySystem.Instance;
            Hook(true);
        }

        float dt = Time.unscaledDeltaTime;
        float target;
        Color color;
        string text;
        if (fury.IsActive)
        {
            target = fury.ActiveRemaining01;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
            color = Color.Lerp(Building, Hot, pulse);
            text = "FURIA";
        }
        else if (fury.OnCooldown)
        {
            target = fury.CooldownProgress01;
            color = Cool;
            text = "FURIA";
        }
        else
        {
            target = (float)fury.Kills / Mathf.Max(1, fury.KillsRequired);
            color = Color.Lerp(Building, Hot, target);
            text = fury.Kills > 0 ? fury.Kills + "/" + fury.KillsRequired : "FURIA";
        }

        // wypełnienie: rośnie szybko, opada płynnie
        float speed = target > shown ? 18f : 5f;
        shown = Mathf.MoveTowards(shown, target, speed * dt * (target > shown ? 0.1f : 0.25f) + 0.0001f);
        fill.fillAmount = shown;
        fill.color = color;
        label.text = text;
        windowLine.fillAmount = fury.IsActive || fury.OnCooldown ? 0f : fury.WindowRemaining01;

        var g = glow.color;
        g.a = fury.IsActive ? 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 14f) : 0f;
        glow.color = g;

        float wantAlpha = fury.IsActive || fury.Kills > 0 ? 1f : (fury.OnCooldown ? 0.55f : 0.4f);
        group.alpha = Mathf.MoveTowards(group.alpha, wantAlpha, dt * 4f);
    }
}
