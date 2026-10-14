using System.Collections;
using UnityEngine;

/// <summary>
/// Krótkie zamrożenie gry przy trafieniu. Komponent w scenie, dostęp przez HitStop.Instance / HitStop.Freeze().
/// Zamrożenie to timeScale = 0.0001 (a nie 0): dzięki temu da się odróżnić własne zamrożenie od pauzy
/// (GameManager i LevelUpScreen ustawiają dokładnie 0). Pauza ma pierwszeństwo i nigdy nie jest cofnięta.
/// </summary>
public class HitStop : MonoBehaviour
{
    private const float FrozenScale = 0.0001f;

    public static HitStop Instance { get; private set; }

    private Coroutine running;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        // Wyłączenie w trakcie zamrożenia nie może zostawić zatrzymanego czasu.
        if (running != null && Mathf.Approximately(Time.timeScale, FrozenScale))
            Time.timeScale = 1f;
        running = null;
    }

    /// <summary>Zamraża czas na podaną liczbę sekund czasu rzeczywistego. Ignorowane podczas pauzy i trwającego zamrożenia.</summary>
    public static void Freeze(float seconds = 0.05f)
    {
        if (Instance != null)
            Instance.FreezeInternal(seconds);
    }

    private void FreezeInternal(float seconds)
    {
        if (running != null || Time.timeScale <= 0f || !isActiveAndEnabled)
            return;
        running = StartCoroutine(FreezeRoutine(seconds));
    }

    private IEnumerator FreezeRoutine(float seconds)
    {
        float previous = Time.timeScale;
        Time.timeScale = FrozenScale;
        yield return new WaitForSecondsRealtime(seconds);
        // Jeśli w międzyczasie ktoś ustawił timeScale (pauza), nie nadpisujemy.
        if (Mathf.Approximately(Time.timeScale, FrozenScale))
            Time.timeScale = previous;
        running = null;
    }
}
