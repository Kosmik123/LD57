using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pasek XP przy dolnej krawędzi ekranu: wypełnienie tweenowane, błysk i „+N” przy zdobyciu XP,
/// biały błysk i napis „Poziom N” przy awansie. Czas rzeczywisty (ekran kart ma timeScale = 0).
/// Zastępuje stary pasek XP z SkillHud (wiring wyłącza stary obiekt i zeruje SkillHud.xpFill).
/// </summary>
public class XpBarView : MonoBehaviour
{
    [SerializeField] private Experience experience;
    [SerializeField, Tooltip("Image typu Filled (poziomo).")] private Image fill;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color normalColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color gainColor = new Color(0.75f, 0.95f, 1f);
    [SerializeField] private float fillDuration = 0.25f;

    private const int PoolSize = 4;
    private readonly TMP_Text[] gainLabels = new TMP_Text[PoolSize];
    private int nextLabel;
    private TMP_Text levelLabel;
    private Tween fillTween;
    private bool dirty;
    private int shownLevel;

    private void Awake()
    {
        for (int i = 0; i < PoolSize; i++)
            gainLabels[i] = CreateLabel("Gain " + i, 22, TextAlignmentOptions.Left, new Vector2(1f, 0.5f), new Vector2(60, 28));
        levelLabel = CreateLabel("Level Label", 30, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(300, 40));
        levelLabel.fontStyle = FontStyles.Bold;
    }

    private TMP_Text CreateLabel(string label, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x < 0.9f ? 0.5f : 0f, 0f);
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.alpha = 0f;
        return t;
    }

    private void OnEnable()
    {
        GameEvents.OnExperienceGained += GameEvents_OnExperienceGained;
        GameEvents.OnLevelUp += GameEvents_OnLevelUp;
        if (experience != null && fill != null)
        {
            shownLevel = experience.HeroLevel;
            fill.fillAmount = experience.Progress01;
            fill.color = normalColor;
        }
    }

    private void OnDisable()
    {
        GameEvents.OnExperienceGained -= GameEvents_OnExperienceGained;
        GameEvents.OnLevelUp -= GameEvents_OnLevelUp;
        fillTween?.Kill();
    }

    private void GameEvents_OnExperienceGained(int amount)
    {
        dirty = true;
        if (fill != null)
        {
            fill.DOKill();
            fill.color = gainColor;
            fill.DOColor(normalColor, 0.1f).SetUpdate(true).SetTarget(fill);
        }
        var l = gainLabels[nextLabel];
        nextLabel = (nextLabel + 1) % PoolSize;
        l.DOKill();
        l.text = "+" + amount;
        l.color = new Color(0.75f, 0.95f, 1f, 1f);
        var rt = l.rectTransform;
        rt.anchoredPosition = new Vector2(8f, 6f);
        DOTween.Sequence().SetTarget(l).SetUpdate(true)
            .Join(rt.DOAnchorPosY(34f, 0.6f).SetEase(Ease.OutCubic))
            .Join(l.DOFade(0f, 0.3f).SetDelay(0.3f));
    }

    private void GameEvents_OnLevelUp(int heroLevel)
    {
        dirty = true;
        if (fill != null)
        {
            fill.DOKill();
            fill.color = Color.white;
            fill.DOColor(normalColor, 0.35f).SetUpdate(true).SetTarget(fill);
        }
        levelLabel.DOKill();
        levelLabel.text = "Poziom " + heroLevel;
        levelLabel.color = new Color(1f, 0.9f, 0.4f, 1f);
        levelLabel.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        DOTween.Sequence().SetTarget(levelLabel).SetUpdate(true)
            .Join(levelLabel.rectTransform.DOAnchorPosY(30f, 1f).SetEase(Ease.OutCubic))
            .Join(levelLabel.DOFade(0f, 0.3f).SetDelay(0.7f));
    }

    private void LateUpdate()
    {
        if (experience == null || fill == null) return;
        if (experience.HeroLevel < shownLevel) // restart runu
        {
            shownLevel = experience.HeroLevel;
            fillTween?.Kill();
            fill.fillAmount = experience.Progress01;
            dirty = false;
            return;
        }
        if (!dirty) return;
        dirty = false;
        fillTween?.Kill();
        if (experience.HeroLevel > shownLevel)
        {
            shownLevel = experience.HeroLevel;
            // dopełnij do końca, wyzeruj i dojedź do nowego postępu
            fillTween = DOTween.Sequence().SetUpdate(true)
                .Append(fill.DOFillAmount(1f, fillDuration).SetEase(Ease.OutCubic))
                .AppendCallback(() => fill.fillAmount = 0f)
                .Append(fill.DOFillAmount(experience.Progress01, fillDuration).SetEase(Ease.OutCubic));
        }
        else
        {
            fillTween = fill.DOFillAmount(experience.Progress01, fillDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
