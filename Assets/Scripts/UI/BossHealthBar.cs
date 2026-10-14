using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pasek bossa u góry ekranu. Komponent leży na zawsze aktywnym obiekcie, a widoczny jest tylko potomek `panel`.
/// Co 0,5 s szuka żywego EnemyHealth z IsBoss. Błysk przy zmianie fazy (SpiderMotherBoss.OnPhaseChanged).
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private string bossName = "Matka pająków";
    [SerializeField] private float pollInterval = 0.5f;
    [SerializeField] private Color normalColor = new Color(0.7f, 0.1f, 0.15f);

    [Header("Nogi bossa (P12)")]
    [SerializeField, Tooltip("Rząd ikon nóg. Null = budowany w kodzie pod panelem przy pierwszym bossie z nogami.")] private RectTransform legRow;
    [SerializeField] private Image[] legIcons;
    [SerializeField] private TMP_Text legLabel;
    [SerializeField] private Color legAliveColor = new Color(0.9f, 0.85f, 0.8f);
    [SerializeField] private Color legDeadColor = new Color(0.25f, 0.25f, 0.27f, 0.7f);
    [SerializeField] private Vector2 legIconSize = new Vector2(18f, 10f);
    [SerializeField] private float legIconGap = 4f;

    private EnemyHealth boss;
    private bool bound;
    private SpiderMotherBoss phases;
    private float nextPoll;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (nameLabel != null) nameLabel.text = bossName;
    }

    private void OnEnable() => EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
        Unbind();
    }

    private void Update()
    {
        if (boss == null && Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + pollInterval;
            foreach (var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                if (e.IsBoss && !e.Health.IsDead) { Bind(e); break; }
        }
        // Boss zniszczony albo martwy bez zdarzenia (np. test, przeładowanie poziomu): schowaj pasek.
        if (bound && (boss == null || boss.Health == null || boss.Health.IsDead || !boss.gameObject.activeInHierarchy))
            Unbind(); // boss zniszczony (fake null) albo martwy bez zdarzenia
        if (boss != null && fill != null)
            fill.fillAmount = boss.Health.MaxHealth > 0 ? Mathf.Clamp01((float)boss.Health.CurrentHealth / boss.Health.MaxHealth) : 0f;
        if (boss != null)
            UpdateLegs();
    }

    // ------------------------------------------------------------ nogi

    private void EnsureLegRow(int count)
    {
        if (legIcons != null && legIcons.Length >= count && legIcons[0] != null) return;
        if (panel == null || count <= 0) return;

        var rowGo = new GameObject("Leg Row", typeof(RectTransform));
        rowGo.transform.SetParent(panel.transform, false);
        legRow = rowGo.GetComponent<RectTransform>();
        legRow.anchorMin = new Vector2(0.5f, 0f);
        legRow.anchorMax = new Vector2(0.5f, 0f);
        legRow.pivot = new Vector2(0.5f, 1f);
        float width = count * legIconSize.x + (count - 1) * legIconGap;
        legRow.sizeDelta = new Vector2(width, legIconSize.y);
        legRow.anchoredPosition = new Vector2(0f, -6f);

        legIcons = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Leg " + i, typeof(RectTransform));
            go.transform.SetParent(legRow, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = legIconSize;
            rt.anchoredPosition = new Vector2(i * (legIconSize.x + legIconGap), 0f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = legAliveColor;
            legIcons[i] = img;
        }

        if (legLabel == null)
        {
            var labelGo = new GameObject("Leg Label", typeof(RectTransform));
            labelGo.transform.SetParent(legRow, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(260f, 20f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.9f, 0.85f, 0.8f);
            tmp.raycastTarget = false;
            legLabel = tmp;
        }
    }

    private static string LegsLabel(int toDestroy)
    {
        if (toDestroy <= 0) return "Boss pada!";
        string noun = toDestroy == 1 ? "nogę" : (toDestroy % 10 >= 2 && toDestroy % 10 <= 4 && (toDestroy < 10 || toDestroy > 20)) ? "nogi" : "nóg";
        return "Zniszcz " + toDestroy + " " + noun;
    }

    private void UpdateLegs()
    {
        if (phases == null || phases.LegCount == 0) return; // boss bez nóg (stary prefab): bez wskaźnika
        EnsureLegRow(phases.LegCount);
        if (legIcons == null) return;
        for (int i = 0; i < legIcons.Length && i < phases.LegCount; i++)
        {
            if (legIcons[i] == null) continue;
            var leg = phases.Legs[i];
            bool alive = leg != null && leg.IsAlive;
            float ratio = alive && leg.MaxHealth > 0 ? (float)leg.CurrentHealth / leg.MaxHealth : 0f;
            legIcons[i].color = alive ? Color.Lerp(new Color(legAliveColor.r, 0.4f, 0.35f, 1f), legAliveColor, ratio) : legDeadColor;
        }
        if (legLabel != null)
            legLabel.text = LegsLabel(phases.LegsToDestroy);
    }

    private void Bind(EnemyHealth e)
    {
        boss = e;
        bound = true;
        phases = e.GetComponent<SpiderMotherBoss>();
        if (phases != null) phases.OnPhaseChanged += Boss_OnPhaseChanged;
        if (fill != null) fill.color = normalColor;
        if (panel != null) panel.SetActive(true);
    }

    private void Unbind()
    {
        if (phases != null) phases.OnPhaseChanged -= Boss_OnPhaseChanged;
        phases = null;
        boss = null;
        bound = false;
        if (fill != null) fill.DOKill();
        if (panel != null) panel.SetActive(false);
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth e)
    {
        if (e == boss) Unbind();
    }

    private void Boss_OnPhaseChanged(int phase)
    {
        if (fill == null) return;
        fill.DOKill();
        fill.color = Color.white;
        fill.DOColor(normalColor, 0.5f).SetUpdate(true);
    }
}
