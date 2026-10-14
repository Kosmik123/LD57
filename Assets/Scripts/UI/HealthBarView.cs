using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pasek HP gracza: czerwony pasek spada natychmiast, biały („opóźniony ubytek”) dogania po 0,4 s.
/// Leczenie podnosi oba. Poniżej 30% HP czerwony miga 2 Hz. Animacje na czasie rzeczywistym.
/// </summary>
public class HealthBarView : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField, Tooltip("Image typu Filled (biały, opóźniony ubytek).")] private Image delayedFill;
    [SerializeField, Tooltip("Image typu Filled (czerwony, właściwy).")] private Image fill;
    [SerializeField] private float catchUpDelay = 0.4f;
    [SerializeField] private float catchUpDuration = 0.3f;
    [SerializeField] private float lowThreshold = 0.3f;
    [SerializeField] private Color normalColor = new Color(0.85f, 0.1f, 0.12f);
    [SerializeField] private Color blinkColor = new Color(1f, 0.55f, 0.55f);

    private Tween catchUp;
    private float ratio = 1f;

    private void OnEnable()
    {
        if (health == null) return;
        health.OnHealthChanged += Health_OnHealthChanged;
        ratio = Ratio();
        Apply(ratio, true);
    }

    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= Health_OnHealthChanged;
        catchUp?.Kill();
    }

    private float Ratio() => health.MaxHealth > 0 ? Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth) : 0f;

    private void Health_OnHealthChanged(int change)
    {
        ratio = Ratio();
        Apply(ratio, change >= 0);
    }

    private void Apply(float target, bool together)
    {
        catchUp?.Kill();
        if (fill != null) fill.fillAmount = target;
        if (delayedFill == null) return;
        if (together || target >= delayedFill.fillAmount)
        {
            delayedFill.fillAmount = target;
            return;
        }
        catchUp = delayedFill.DOFillAmount(target, catchUpDuration)
            .SetDelay(catchUpDelay).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void Update()
    {
        if (fill == null) return;
        bool low = ratio > 0f && ratio < lowThreshold;
        // 2 Hz: pełny cykl co 0,5 s.
        fill.color = low
            ? Color.Lerp(normalColor, blinkColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f))
            : normalColor;
    }
}
