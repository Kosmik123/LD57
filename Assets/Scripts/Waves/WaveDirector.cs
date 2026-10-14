using DG.Tweening;
using UnityEngine;

/// <summary>
/// Oddech po wybiciu fali: krótkie spowolnienie czasu (0,3 s, timeScale 0,6) i powrót na czasie rzeczywistym.
/// Pomijane, gdy gra jest zapauzowana (timeScale == 0). Dźwięk końca fali jest w SfxHooks.
/// </summary>
public class WaveDirector : MonoBehaviour
{
    [SerializeField] private bool slowMoOnWaveCleared = true;
    [SerializeField] private float slowScale = 0.6f;
    [SerializeField] private float slowSeconds = 0.3f;

    private Tween tween;
    private float lastSet;

    private void OnEnable() => GameEvents.OnWaveCleared += GameEvents_OnWaveCleared;

    private void OnDisable()
    {
        GameEvents.OnWaveCleared -= GameEvents_OnWaveCleared;
        tween?.Kill();
        tween = null;
        if (Time.timeScale > 0f && Time.timeScale < 1f)
            Time.timeScale = 1f;
    }

    private void SetScale(float v)
    {
        // ktoś inny (pauza na ekran kart) zmienił timeScale w trakcie powrotu: oddaj mu kontrolę
        if (!Mathf.Approximately(Time.timeScale, lastSet))
        {
            tween?.Kill();
            return;
        }
        Time.timeScale = v;
        lastSet = v;
    }

    private void GameEvents_OnWaveCleared(WaveZone zone)
    {
        if (!slowMoOnWaveCleared || Time.timeScale <= 0f)
            return;

        tween?.Kill();
        Time.timeScale = slowScale;
        lastSet = slowScale;
        tween = DOTween.To(() => Time.timeScale, SetScale, 1f, slowSeconds)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);
    }
}
