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
