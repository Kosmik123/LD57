using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Ekran wyboru kart po level upie. Pauzuje grę (timeScale 0), kolejkuje wiele level upów.</summary>
public class LevelUpScreen : MonoBehaviour
{
    private const int HpCardAmount = 10;

    [SerializeField] private GameObject root;
    [SerializeField] private Button[] cardButtons = new Button[3];
    [SerializeField] private TMP_Text[] cardTitles = new TMP_Text[3];
    [SerializeField] private TMP_Text[] cardDescriptions = new TMP_Text[3];
    [SerializeField] private Image[] cardIcons = new Image[3];
    [SerializeField] private SkillSlots slots;
    [SerializeField] private Health playerHealth;
    [SerializeField, Tooltip("Nagłówek ekranu (lekki puls).")] private RectTransform header;
    [SerializeField, Tooltip("Po otwarciu/odświeżeniu oferty karty nie przyjmują wyboru przez tyle sekund (klik z walki nie wybiera karty).")]
    private float inputGrace = 0.5f;

    private float acceptAfter;

    private readonly string[] tierNames = { "Baza", "Ulepszenie I", "Ulepszenie II", "Ewolucja" };
    private readonly List<SkillDefinition> offer = new List<SkillDefinition>();
    private int pending;
    private bool isOpen;
    private bool hpCard;
    private bool busy;
    private Tween headerTween;
    private readonly Sequence[] cardTweens = new Sequence[3];

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        for (int i = 0; i < cardButtons.Length; i++)
        {
            int index = i;
            if (cardButtons[i] != null) cardButtons[i].onClick.AddListener(() => Choose(index));
        }
    }

    private void OnEnable() => GameEvents.OnLevelUp += GameEvents_OnLevelUp;

    private void OnDisable()
    {
        GameEvents.OnLevelUp -= GameEvents_OnLevelUp;
        if (isOpen) Close();
    }

    private void GameEvents_OnLevelUp(int heroLevel)
    {
        // Po śmierci lub wygranej karty nie mają sensu (XP z wrogów ginących razem z bossem).
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsRunOver) return;
        pending++;
        if (!isOpen) Open();
    }

    private void Update()
    {
        if (!isOpen) return;
        for (int i = 0; i < 3; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                Choose(i);
    }

    private void Open()
    {
        isOpen = true;
        busy = false;
        Time.timeScale = 0f;
        if (root != null) root.SetActive(true);
        RefreshOffer();
        PulseHeader();
    }

    private void Close()
    {
        isOpen = false;
        busy = false;
        KillTweens();
        Time.timeScale = 1f;
        if (root != null) root.SetActive(false);
    }

    private void RefreshOffer()
    {
        offer.Clear();
        var all = slots.GetOfferableSkills();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        for (int i = 0; i < all.Count && i < 3; i++)
            offer.Add(all[i]);

        hpCard = offer.Count == 0;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            bool active = hpCard ? i == 0 : i < offer.Count;
            if (cardButtons[i] != null) cardButtons[i].gameObject.SetActive(active);
            if (!active) continue;

            if (hpCard)
            {
                cardTitles[i].text = "+" + HpCardAmount + " HP";
                cardDescriptions[i].text = "Leczy " + HpCardAmount + " punktów zdrowia.";
                if (cardIcons[i] != null)
                {
                    cardIcons[i].sprite = SkillIcons.HeartIcon();
                    cardIcons[i].preserveAspect = true;
                    cardIcons[i].enabled = true;
                }
                continue;
            }

            var def = offer[i];
            int target = Mathf.Min(slots.TierOf(def) + 1, SkillDefinition.MaxTier);
            cardTitles[i].text = def.DisplayName + " · " + tierNames[target];
            cardDescriptions[i].text = TierText(def, target);
            if (cardIcons[i] != null)
            {
                var icon = SkillIcons.Get(def);
                cardIcons[i].sprite = icon;
                cardIcons[i].preserveAspect = true;
                cardIcons[i].enabled = icon != null;
            }
        }
        AnimateCards();
        acceptAfter = Time.unscaledTime + inputGrace;
        SetCardsInteractable(false);
        DOVirtual.DelayedCall(inputGrace, () => { if (isOpen && !busy) SetCardsInteractable(true); }, true);
    }

    /// <summary>Opis tieru + linia liczb: przy ulepszeniu „poprzedni → nowy” (odnowienie, obrażenia).</summary>
    private static string TierText(SkillDefinition def, int target)
    {
        var next = def.Tiers[target];
        string text = next.Description;
        bool upgrade = target > 0;
        var prev = upgrade ? def.Tiers[target - 1] : null;
        string cd = upgrade && !Mathf.Approximately(prev.Cooldown, next.Cooldown)
            ? prev.Cooldown.ToString("0.##") + " → " + next.Cooldown.ToString("0.##")
            : next.Cooldown.ToString("0.##");
        string stats = "Odnowienie " + cd + " s";
        if (next.Damage > 0 || (upgrade && prev.Damage > 0))
        {
            stats += "  ·  Obrażenia ";
            stats += upgrade && prev.Damage != next.Damage ? prev.Damage + " → " + next.Damage : next.Damage.ToString();
        }
        return text + "\n\n<size=80%><color=#FFD966>" + stats + "</color></size>";
    }

    private void SetCardsInteractable(bool on)
    {
        foreach (var b in cardButtons)
            if (b != null) b.interactable = on;
    }

    private void KillTweens()
    {
        headerTween?.Kill();
        for (int i = 0; i < cardTweens.Length; i++) { cardTweens[i]?.Kill(); cardTweens[i] = null; }
    }

    private void PulseHeader()
    {
        headerTween?.Kill();
        if (header == null) return;
        header.localScale = Vector3.one;
        headerTween = header.DOScale(1.04f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private static CanvasGroup GroupOf(Button b) => b.TryGetComponent<CanvasGroup>(out var g) ? g : b.gameObject.AddComponent<CanvasGroup>();

    private void AnimateCards()
    {
        int order = 0;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            var b = cardButtons[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            cardTweens[i]?.Kill();
            var group = GroupOf(b);
            var t = b.transform;
            if (b.targetGraphic != null) b.targetGraphic.color = Color.white;
            t.localScale = Vector3.one * 0.6f;
            group.alpha = 0f;
            cardTweens[i] = DOTween.Sequence().SetUpdate(true).SetDelay(0.08f * order)
                .Append(t.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => group.alpha, a => group.alpha = a, 1f, 0.2f));
            order++;
        }
    }

    private void Choose(int index)
    {
        if (!isOpen || busy) return;
        if (Time.unscaledTime < acceptAfter) return; // karencja: przypadkowy klik z walki
        if (Input.GetMouseButton(0) && Time.unscaledTime < acceptAfter + 0.1f) return;
        if (hpCard)
        {
            if (index != 0) return;
            if (playerHealth != null) playerHealth.ChangeHealth(HpCardAmount);
        }
        else
        {
            if (index >= offer.Count) return;
            slots.Grant(offer[index]);
        }

        pending--;
        busy = true;
        var card = cardButtons[Mathf.Clamp(index, 0, cardButtons.Length - 1)];
        cardTweens[index]?.Kill();
        if (card == null) { Continue(); return; }
        var graphic = card.targetGraphic;
        var seq = DOTween.Sequence().SetUpdate(true)
            .Append(card.transform.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad));
        if (graphic != null) seq.Join(graphic.DOColor(new Color(1f, 1f, 0.6f), 0.15f));
        seq.OnComplete(Continue);
        cardTweens[index] = seq;
    }

    private void Continue()
    {
        if (!isOpen) return;
        busy = false;
        if (pending > 0) RefreshOffer();
        else Close();
    }
}
