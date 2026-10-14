using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekran wyboru reliktu po otwarciu skrzyni: pauza (timeScale 0), 3 losowe relikty jako karty (mniejszy ekran niż karty
/// level upu), klawisze 1-3 albo klik, karencja 0,5 s na wybór. Własny ekran, nie miesza się z LevelUpScreen: otwiera się
/// dopiero, gdy czas płynie (timeScale &gt; 0,5), więc czeka na zamknięcie kart level upu, pauzy i hit stopu.
/// UI budowane w kodzie w Awake pod RectTransformem tego obiektu (tworzy go P12Wiring_Mechanics w Game UI).
/// Wywołanie: RelicScreen.Request() (skrzynia). Bez ekranu w scenie relikt jest przyznawany losowo.
/// </summary>
public class RelicScreen : MonoBehaviour
{
    private const int Cards = 3;

    public static RelicScreen Instance { get; private set; }

    [SerializeField] private RelicHolder holder;
    [SerializeField, Tooltip("Po otwarciu karty nie przyjmują wyboru przez tyle sekund (klik z walki nie wybiera karty).")]
    private float inputGrace = 0.5f;

    private class Card
    {
        public Button button;
        public CanvasGroup group;
        public Image icon;
        public TextMeshProUGUI letter, title, description;
    }

    private GameObject panelRoot;
    private RectTransform header;
    private readonly Card[] cards = new Card[Cards];
    private readonly List<RelicDefinition> offer = new List<RelicDefinition>();
    private readonly Sequence[] cardTweens = new Sequence[Cards];
    private Tween headerTween;
    private PlayerController disabledController;
    private float acceptAfter;
    private int pending;
    private bool isOpen, busy, built;

    public bool IsOpen => isOpen;

    /// <summary>Prosi o ekran wyboru reliktu (kolejkowane, jeśli akurat trwa inny ekran).</summary>
    public static void Request()
    {
        if (Instance != null) { Instance.pending++; return; }

        // awaryjnie bez ekranu w scenie: losowy relikt
        var h = RelicHolder.Instance;
        if (h == null) return;
        var offered = h.GetOffer(1);
        if (offered.Count > 0) h.Add(offered[0]);
    }

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        if (isOpen) Close();
    }

    // ------------------------------------------------------------ budowa UI

    private void Build()
    {
        if (built) return;
        built = true;
        var self = (RectTransform)transform;
        UiSprites.Stretch(self);

        var rootRt = UiSprites.Child(transform, "Root");
        UiSprites.Stretch(rootRt);
        panelRoot = rootRt.gameObject;
        var dim = UiSprites.AddImage(rootRt, "Dim", new Color(0.02f, 0.01f, 0.03f, 0.7f));
        UiSprites.Stretch(dim.rectTransform);
        dim.raycastTarget = true; // blokuje klikanie w grę pod spodem

        var frame = UiSprites.AddImage(rootRt, "Frame", new Color(0.85f, 0.65f, 0.2f, 1f));
        UiSprites.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(806f, 356f), Vector2.zero);
        var panel = UiSprites.AddImage(rootRt, "Panel", new Color(0.09f, 0.07f, 0.1f, 1f));
        UiSprites.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 350f), Vector2.zero);

        var title = UiSprites.AddText(panel.transform, "Header", "WYBIERZ RELIKT", 30f, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
        title.fontStyle = FontStyles.Bold;
        UiSprites.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(760f, 44f), new Vector2(0f, -10f));
        header = title.rectTransform;

        float cardW = 232f, cardH = 250f, gap = 18f;
        float startX = -(cardW + gap);
        for (int i = 0; i < Cards; i++)
        {
            var card = new Card();
            var bg = UiSprites.AddImage(panel.transform, "Card " + (i + 1), new Color(0.16f, 0.13f, 0.18f, 1f));
            bg.raycastTarget = true;
            UiSprites.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(cardW, cardH), new Vector2(startX + i * (cardW + gap), 14f));
            card.button = bg.gameObject.AddComponent<Button>();
            card.button.targetGraphic = bg;
            var colors = card.button.colors;
            colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            card.button.colors = colors;
            card.group = bg.gameObject.AddComponent<CanvasGroup>();

            card.icon = UiSprites.AddImage(bg.transform, "Icon", Color.white);
            card.icon.sprite = RelicIcons.Hexagon;
            UiSprites.Place(card.icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(84f, 84f), new Vector2(0f, -14f));
            card.letter = UiSprites.AddText(card.icon.transform, "Letter", "R", 40f, TextAlignmentOptions.Center, new Color(0.08f, 0.06f, 0.08f));
            card.letter.fontStyle = FontStyles.Bold;
            UiSprites.Stretch(card.letter.rectTransform);
            card.title = UiSprites.AddText(bg.transform, "Title", "", 22f, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.6f));
            card.title.fontStyle = FontStyles.Bold;
            UiSprites.Place(card.title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(cardW - 20f, 34f), new Vector2(0f, -106f));
            card.description = UiSprites.AddText(bg.transform, "Description", "", 17f, TextAlignmentOptions.Top, new Color(0.9f, 0.88f, 0.85f));
            UiSprites.Place(card.description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(cardW - 24f, 100f), new Vector2(0f, -144f));
            var num = UiSprites.AddText(bg.transform, "Key", (i + 1).ToString(), 18f, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.5f));
            UiSprites.Stretch(num.rectTransform, 6f);

            int index = i;
            card.button.onClick.AddListener(() => Choose(index));
            cards[i] = card;
        }
        panelRoot.SetActive(false);
    }

    // ------------------------------------------------------------ pętla

    private void Update()
    {
        if (isOpen)
        {
            for (int i = 0; i < Cards; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    Choose(i);
            return;
        }
        if (pending <= 0) return;
        if (Time.timeScale <= 0.5f) return; // trwa pauza, karty level upu albo hit stop: poczekaj
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsRunOver) { pending = 0; return; }
        if (holder == null) holder = RelicHolder.Instance;
        if (holder == null) return;
        Open();
    }

    private void Open()
    {
        RefreshOffer();
        if (offer.Count == 0)
        {
            pending = 0; // wszystkie relikty zebrane
            return;
        }
        isOpen = true;
        busy = false;
        Time.timeScale = 0f;
        var pc = holder.GetComponent<PlayerController>();
        if (pc != null && pc.enabled) { pc.enabled = false; disabledController = pc; }
        panelRoot.SetActive(true);
        Sfx.Play(SfxKind.LevelUp);

        headerTween?.Kill();
        header.localScale = Vector3.one;
        headerTween = header.DOScale(1.04f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        Animate();
    }

    private void RefreshOffer()
    {
        offer.Clear();
        if (holder != null) offer.AddRange(holder.GetOffer(Cards));
        for (int i = 0; i < Cards; i++)
        {
            bool active = i < offer.Count;
            cards[i].button.gameObject.SetActive(active);
            if (!active) continue;
            var def = offer[i];
            cards[i].icon.color = def.Color;
            cards[i].letter.text = def.Letter;
            cards[i].title.text = def.DisplayName;
            cards[i].description.text = def.Description;
        }
        // karty wyśrodkowane, gdy ofert jest mniej niż 3
        float cardW = 232f, gap = 18f;
        float total = offer.Count * cardW + (offer.Count - 1) * gap;
        for (int i = 0; i < offer.Count; i++)
        {
            var rt = (RectTransform)cards[i].button.transform;
            var pos = rt.anchoredPosition;
            pos.x = -total * 0.5f + cardW * 0.5f + i * (cardW + gap);
            rt.anchoredPosition = pos;
        }
    }

    private void Animate()
    {
        int order = 0;
        acceptAfter = Time.unscaledTime + inputGrace;
        for (int i = 0; i < Cards; i++)
        {
            var c = cards[i];
            if (!c.button.gameObject.activeSelf) continue;
            c.button.interactable = false;
            cardTweens[i]?.Kill();
            c.button.targetGraphic.color = new Color(0.16f, 0.13f, 0.18f, 1f);
            c.button.transform.localScale = Vector3.one * 0.6f;
            c.group.alpha = 0f;
            cardTweens[i] = DOTween.Sequence().SetUpdate(true).SetDelay(0.08f * order)
                .Append(c.button.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => c.group.alpha, a => c.group.alpha = a, 1f, 0.2f));
            order++;
        }
        DOVirtual.DelayedCall(inputGrace, () =>
        {
            if (!isOpen || busy) return;
            foreach (var c in cards)
                if (c.button != null) c.button.interactable = true;
        }, true);
    }

    private void Choose(int index)
    {
        if (!isOpen || busy || index < 0 || index >= offer.Count) return;
        if (Time.unscaledTime < acceptAfter) return; // karencja: przypadkowy klik z walki
        if (Input.GetMouseButton(0) && Time.unscaledTime < acceptAfter + 0.1f) return;

        busy = true;
        var chosen = offer[index];
        holder.Add(chosen);

        var card = cards[index];
        cardTweens[index]?.Kill();
        cardTweens[index] = DOTween.Sequence().SetUpdate(true)
            .Append(card.button.transform.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad))
            .Join(card.button.targetGraphic.DOColor(new Color(1f, 0.95f, 0.6f), 0.15f))
            .OnComplete(() => Finish(chosen));
    }

    private void Finish(RelicDefinition chosen)
    {
        if (!isOpen) return;
        pending = Mathf.Max(0, pending - 1);
        Close();
        if (holder != null)
        {
            var pos = holder.transform.position;
            var c = chosen.Color;
            var hdr = new Color(c.r * 3f, c.g * 3f, c.b * 3f);
            VoxelFx.Burst(pos + Vector3.up, hdr, 22, 0.15f, 6f, 0.6f);
            VoxelFx.Pillar(pos, hdr, 3f, 0.6f);
            WorldPopup.Show(pos + Vector3.up * 2.5f, chosen.DisplayName, c, 7f, 1.6f);
        }
        Sfx.Play(SfxKind.CardPick);
    }

    private void Close()
    {
        isOpen = false;
        busy = false;
        headerTween?.Kill();
        for (int i = 0; i < Cards; i++) { cardTweens[i]?.Kill(); cardTweens[i] = null; }
        Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (disabledController != null) { disabledController.enabled = true; disabledController = null; }
    }
}

/// <summary>
/// Rząd małych ikon zdobytych reliktów w HUD (sześciokąty z literą, kolory z RelicDefinition). Obiekt „Relic Bar” w Game UI
/// tworzy P12Wiring_Mechanics (lewy górny róg, pod paskiem HP); ikony budowane w kodzie przy każdej zmianie listy.
/// </summary>
public class RelicBar : MonoBehaviour
{
    [SerializeField] private RelicHolder holder;
    [SerializeField] private float iconSize = 30f;
    [SerializeField] private float spacing = 4f;
    [SerializeField, Tooltip("Pozycja lewego górnego rogu rzędu względem lewego górnego rogu ekranu (px).")]
    private Vector2 topLeftOffset = new Vector2(24f, -56f);

    private RectTransform root;
    private RelicHolder hooked;
    private int shownCount = -1;

    private void Awake()
    {
        root = (RectTransform)transform;
        UiSprites.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, iconSize), topLeftOffset);
    }

    private void OnDisable() => Unhook();

    private void Unhook()
    {
        if (hooked != null) hooked.OnChanged -= Rebuild;
        hooked = null;
    }

    private void Update()
    {
        if (holder == null) holder = RelicHolder.Instance;
        if (holder != hooked)
        {
            Unhook();
            hooked = holder;
            if (hooked != null) hooked.OnChanged += Rebuild;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
        int count = hooked != null ? hooked.Owned.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var def = hooked.Owned[i];
            var icon = UiSprites.AddImage(root, "Relic " + def.DisplayName, def.Color);
            icon.sprite = RelicIcons.Hexagon;
            UiSprites.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(iconSize, iconSize),
                new Vector2(i * (iconSize + spacing), 0f));
            var letter = UiSprites.AddText(icon.transform, "Letter", def.Letter, iconSize * 0.55f, TextAlignmentOptions.Center, new Color(0.08f, 0.06f, 0.08f));
            letter.fontStyle = FontStyles.Bold;
            UiSprites.Stretch(letter.rectTransform);
            if (i == count - 1 && count > shownCount && shownCount >= 0)
            {
                icon.transform.localScale = Vector3.zero;
                icon.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(icon.gameObject);
            }
        }
        shownCount = count;
    }
}
