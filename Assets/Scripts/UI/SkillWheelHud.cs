using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Koło szybkiego wyboru skilla pod PPM: aktywny skill duży (72 px) z ikoną, cooldownem i podpisem „PPM”,
/// pozostałe skille koła mniejsze (48 px) po bokach. Rolka myszy / Tab przesuwa koło (0,15 s, OutBack)
/// i na 0,8 s pokazuje nazwę skilla. Gdy koło puste, ukryte. Obiekt leży w `Game UI` (pod `Skill HUD`),
/// jego pozycja = środek aktywnego slotu. Hierarchię buduje <see cref="BuildIfNeeded"/> (wiring w edytorze
/// albo awaryjnie w Awake), referencje do slotów i użytkownika skilli opcjonalne (szukane w scenie).
/// Czas rzeczywisty (SetUpdate(true)).
/// </summary>
public class SkillWheelHud : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public RectTransform root;
        public Image background;
        public Outline frame;
        public CanvasGroup group;
        public Image icon;
        [Tooltip("Radialny Image (Filled) pokazujący pozostały cooldown.")]
        public Image cooldownFill;
        public TMP_Text tier;

        [System.NonSerialized] public int offset;
        [System.NonSerialized] public bool shown;
        [System.NonSerialized] public bool wasOnCooldown;
    }

    private const int PoolSize = 4;
    private static readonly string[] Roman = { "", "I", "II", "III" };

    private static readonly Color NormalBg = new Color(0.1f, 0.1f, 0.12f, 0.8f);
    private static readonly Color ActiveBg = new Color(0.2f, 0.16f, 0.07f, 0.92f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.4f, 1f);

    [SerializeField] private SkillSlots slots;
    [SerializeField] private SkillUser user;
    [SerializeField] private RectTransform content;
    [SerializeField] private Item[] items;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField, Tooltip("Sprite tła slotów (9-slice). Puste = zaokrąglony kwadrat generowany w kodzie.")]
    private Sprite panelSprite;
    [SerializeField, Tooltip("Sprite nakładki cooldownu (koło). Puste = koło generowane w kodzie.")]
    private Sprite knobSprite;
    [SerializeField] private float activeSize = 72f;
    [SerializeField] private float smallSize = 48f;
    [SerializeField] private float gap = 10f;
    [SerializeField] private float slideTime = 0.15f;
    [SerializeField] private float nameTime = 0.8f;

    private int lastCount = -1;
    private int lastActive = -1;
    private Tween nameTween;

    // ------------------------------------------------------------------ cykl życia

    private void Awake()
    {
        if (slots == null) slots = FindFirstObjectByType<SkillSlots>();
        if (user == null) user = FindFirstObjectByType<SkillUser>();
        BuildIfNeeded();
        if (nameLabel != null) SetAlpha(nameLabel, 0f);
    }

    private void OnEnable()
    {
        if (user != null) user.OnSkillExecuted += User_OnSkillExecuted;
        lastCount = -1; // przeliczenie układu po ponownym włączeniu (np. po menu startowym)
    }

    private void OnDisable()
    {
        if (user != null) user.OnSkillExecuted -= User_OnSkillExecuted;
        KillTweens();
    }

    private void KillTweens()
    {
        nameTween?.Kill();
        nameTween = null;
        if (items == null) return;
        foreach (var it in items)
        {
            if (it == null || it.root == null) continue;
            it.root.DOKill();
            if (it.icon != null) it.icon.transform.DOKill(true);
        }
    }

    private void Update()
    {
        if (slots == null || items == null) return;
        int n = slots.Wheel.Count;
        int active = slots.ActiveWheelIndex;

        if (n != lastCount)
        {
            lastCount = n; lastActive = active;
            Layout(false);
        }
        else if (active != lastActive)
        {
            lastActive = active;
            Layout(true);
            if (n > 1) FlashName(slots.Wheel[active]);
        }

        if (n == 0) return;
        for (int i = 0; i < items.Length && i < n; i++)
            Refresh(items[i], slots.Wheel[i], i == active);

        if (keyLabel != null && n > 0)
        {
            string text = n > 1 ? "PPM <size=65%><color=#B0B0B8>rolka / Tab</color></size>" : "PPM";
            if (keyLabel.text != text) keyLabel.text = text;
        }
    }

    // ------------------------------------------------------------------ układ

    private static int OffsetOf(int index, int count, int active)
    {
        int raw = ((index - active) % count + count) % count;
        return raw <= count / 2 ? raw : raw - count;
    }

    private float XOf(int offset)
    {
        if (offset == 0) return 0f;
        float first = activeSize * 0.5f + gap + smallSize * 0.5f;
        float x = first + (Mathf.Abs(offset) - 1) * (smallSize + gap);
        return offset > 0 ? x : -x;
    }

    private void Layout(bool animate)
    {
        int n = slots.Wheel.Count;
        int active = slots.ActiveWheelIndex;
        if (content != null) content.gameObject.SetActive(n > 0);

        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it == null || it.root == null) continue;
            if (i >= n)
            {
                it.root.DOKill();
                it.root.gameObject.SetActive(false);
                it.shown = false;
                continue;
            }

            int off = OffsetOf(i, n, active);
            bool isActive = off == 0;
            var pos = new Vector2(XOf(off), 0f);
            float size = isActive ? activeSize : smallSize;
            float alpha = isActive ? 1f : Mathf.Abs(off) == 1 ? 0.8f : 0.55f;
            var sizeDelta = new Vector2(size, size);

            it.root.gameObject.SetActive(true);
            if (isActive) it.root.SetAsLastSibling();
            if (it.background != null) it.background.color = isActive ? ActiveBg : NormalBg;
            if (it.frame != null) it.frame.enabled = isActive;
            if (it.tier != null) it.tier.fontSize = isActive ? 16f : 12f;

            bool slide = animate && it.shown && Mathf.Abs(off - it.offset) <= 1;
            bool wrap = animate && it.shown && !slide;
            it.root.DOKill();
            if (slide)
            {
                it.root.DOAnchorPos(pos, slideTime).SetEase(Ease.OutBack).SetUpdate(true);
                it.root.DOSizeDelta(sizeDelta, slideTime).SetEase(Ease.OutBack).SetUpdate(true);
                var g = it.group;
                if (g != null)
                    DOTween.To(() => g.alpha, a => g.alpha = a, alpha, slideTime).SetTarget(it.root).SetUpdate(true);
            }
            else
            {
                it.root.anchoredPosition = pos;
                it.root.sizeDelta = sizeDelta;
                if (it.group != null)
                {
                    it.group.alpha = wrap ? 0f : alpha;
                    if (wrap)
                    {
                        var g = it.group;
                        DOTween.To(() => g.alpha, a => g.alpha = a, alpha, slideTime).SetTarget(it.root).SetUpdate(true);
                    }
                }
            }
            it.offset = off;
            it.shown = true;
        }

        if (nameLabel != null) nameLabel.rectTransform.anchoredPosition = new Vector2(0f, activeSize * 0.5f + 20f);
        if (keyLabel != null) keyLabel.rectTransform.anchoredPosition = new Vector2(0f, -(activeSize * 0.5f + 12f));
    }

    private void Refresh(Item it, SkillInstance skill, bool isActive)
    {
        if (it == null || skill == null) return;
        if (it.icon != null)
        {
            var sprite = SkillIcons.Get(skill.Definition);
            if (it.icon.sprite != sprite) it.icon.sprite = sprite;
            it.icon.enabled = sprite != null;
        }
        if (it.tier != null)
        {
            string t = Roman[Mathf.Clamp(skill.Tier, 0, 3)];
            if (it.tier.text != t) it.tier.text = t;
        }
        float cd = skill.Current.Cooldown;
        bool onCooldown = skill.CooldownRemaining > 0f;
        if (it.cooldownFill != null)
            it.cooldownFill.fillAmount = onCooldown && cd > 0f ? Mathf.Clamp01(skill.CooldownRemaining / cd) : 0f;

        if (it.wasOnCooldown && !onCooldown && it.icon != null)
        {
            var t = it.icon.transform;
            t.DOKill(true);
            t.localScale = Vector3.one;
            DOTween.Sequence().SetTarget(t).SetUpdate(true)
                .Append(t.DOScale(1.25f, 0.125f).SetEase(Ease.OutQuad))
                .Append(t.DOScale(1f, 0.125f).SetEase(Ease.InQuad));
        }
        it.wasOnCooldown = onCooldown;
    }

    private void User_OnSkillExecuted(SkillKind kind, SkillTier tier)
    {
        if (slots == null || items == null) return;
        for (int i = 0; i < items.Length && i < slots.Wheel.Count; i++)
        {
            if (slots.Wheel[i].Definition.Kind != kind || items[i] == null || items[i].icon == null) continue;
            var t = items[i].icon.transform;
            t.DOKill(true);
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * -0.22f, 0.2f, 1, 0f).SetUpdate(true);
        }
    }

    private void FlashName(SkillInstance skill)
    {
        if (nameLabel == null || skill == null) return;
        nameTween?.Kill();
        nameLabel.text = skill.Definition.DisplayName + " " + Roman[Mathf.Clamp(skill.Tier, 0, 3)];
        SetAlpha(nameLabel, 1f);
        var rt = nameLabel.rectTransform;
        rt.localScale = Vector3.one * 0.8f;
        nameTween = DOTween.Sequence().SetUpdate(true)
            .Append(rt.DOScale(1f, 0.15f).SetEase(Ease.OutBack))
            .AppendInterval(nameTime)
            .Append(DOTween.To(() => nameLabel.color.a, a => SetAlpha(nameLabel, a), 0f, 0.2f));
    }

    private static void SetAlpha(Graphic g, float a)
    {
        var c = g.color; c.a = a; g.color = c;
    }

    // ------------------------------------------------------------------ budowa hierarchii

    /// <summary>
    /// Tworzy dzieci (Content, 4 sloty, etykiety nazwy i „PPM”), jeśli ich jeszcze nie ma. Idempotentne.
    /// Edytor podaje wbudowane sprite'y UI (serializowalne w scenie); bez nich używane są sprite'y z kodu.
    /// </summary>
    public void BuildIfNeeded(Sprite panel = null, Sprite knob = null)
    {
        if (items != null && items.Length == PoolSize && items[0] != null && items[0].root != null && content != null) return;

        if (panel != null) panelSprite = panel;
        if (knob != null) knobSprite = knob;
        var panelSpr = panelSprite != null ? panelSprite : SkillIcons.RoundedSquare();
        var knobSpr = knobSprite != null ? knobSprite : SkillIcons.Disc();

        var self = (RectTransform)transform;
        content = Child(self, "Content");
        Stretch(content);

        items = new Item[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var root = Child(content, "Wheel Item " + (i + 1));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(smallSize, smallSize);
            var bg = Get<Image>(root.gameObject);
            bg.sprite = panelSpr; bg.type = Image.Type.Sliced; bg.color = NormalBg; bg.raycastTarget = false;
            var frame = Get<Outline>(root.gameObject);
            frame.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            frame.effectDistance = new Vector2(2f, -2f);
            frame.enabled = false;
            var group = Get<CanvasGroup>(root.gameObject);
            group.blocksRaycasts = false; group.interactable = false;

            var iconRt = Child(root, "Icon");
            Stretch(iconRt, 5f);
            var icon = Get<Image>(iconRt.gameObject);
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;

            var cdRt = Child(root, "Cooldown");
            Stretch(cdRt, 5f);
            var cd = Get<Image>(cdRt.gameObject);
            cd.sprite = knobSpr; cd.color = new Color(0f, 0f, 0f, 0.65f); cd.raycastTarget = false;
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = 2; cd.fillClockwise = false; cd.fillAmount = 0f;

            var tier = MakeText(root, "Tier", "", 12f, TextAlignmentOptions.BottomRight, Color.white);
            Stretch(tier.rectTransform, 4f);

            items[i] = new Item { root = root, background = bg, frame = frame, group = group, icon = icon, cooldownFill = cd, tier = tier };
        }

        nameLabel = MakeText(content, "Name", "", 20f, TextAlignmentOptions.Center, Color.white);
        nameLabel.rectTransform.sizeDelta = new Vector2(280f, 28f);
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.rectTransform.anchoredPosition = new Vector2(0f, activeSize * 0.5f + 20f);

        keyLabel = MakeText(content, "Key", "PPM", 16f, TextAlignmentOptions.Center, Gold);
        keyLabel.rectTransform.sizeDelta = new Vector2(220f, 22f);
        keyLabel.rectTransform.anchoredPosition = new Vector2(0f, -(activeSize * 0.5f + 12f));

        content.gameObject.SetActive(false);
    }

    private static RectTransform Child(RectTransform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return (RectTransform)t;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void Stretch(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
    }

    private static TMP_Text MakeText(RectTransform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
    {
        var rt = Child(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = Get<TextMeshProUGUI>(rt.gameObject);
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color;
        tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
