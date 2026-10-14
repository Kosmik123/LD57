using System;

/// <summary>
/// Statyczny hub zdarzeń runu. Jedyne miejsce, przez które moduły (walka, fale, poziomy, progresja, UI)
/// rozmawiają ze sobą bez bezpośrednich referencji. Subskrybuj w OnEnable, odpinaj w OnDisable.
/// Wszystkie zdarzenia są czyszczone przy restarcie runu przez GameManager.ResetEvents().
/// </summary>
public static class GameEvents
{
    /// <summary>Gracz zdobył XP (ilość). Podnosi: Experience po EnemyHealth.OnEnemyDied.</summary>
    public static event Action<int> OnExperienceGained;
    /// <summary>Gracz awansował na nowy level bohatera (numer levelu, od 1). Podnosi: Experience.</summary>
    public static event Action<int> OnLevelUp;
    /// <summary>Wszyscy wrogowie jednej fali są martwi. Podnosi: WaveZone.</summary>
    public static event Action<WaveZone> OnWaveCleared;
    /// <summary>Poziom jaskini rozpoczęty (numer poziomu 1–8). Podnosi: LevelManager.</summary>
    public static event Action<int> OnLevelStarted;
    /// <summary>Wszystkie fale poziomu wybite, krata otwarta (numer poziomu 1–8). Podnosi: LevelEnemyTracker.</summary>
    public static event Action<int> OnLevelCleared;
    /// <summary>Gracz zginął. Podnosi: GameManager po Health.OnDied gracza.</summary>
    public static event Action OnPlayerDied;
    /// <summary>Boss zabity, run wygrany. Podnosi: GameManager.</summary>
    public static event Action OnRunWon;

    public static void RaiseExperienceGained(int amount) => OnExperienceGained?.Invoke(amount);
    public static void RaiseLevelUp(int heroLevel) => OnLevelUp?.Invoke(heroLevel);
    public static void RaiseWaveCleared(WaveZone zone) => OnWaveCleared?.Invoke(zone);
    public static void RaiseLevelStarted(int caveLevel) => OnLevelStarted?.Invoke(caveLevel);
    public static void RaiseLevelCleared(int caveLevel) => OnLevelCleared?.Invoke(caveLevel);
    public static void RaisePlayerDied() => OnPlayerDied?.Invoke();
    public static void RaiseRunWon() => OnRunWon?.Invoke();

    /// <summary>Odpina wszystkich subskrybentów. Wołane przez GameManager przed przeładowaniem sceny.</summary>
    public static void ResetEvents()
    {
        OnExperienceGained = null;
        OnLevelUp = null;
        OnWaveCleared = null;
        OnLevelStarted = null;
        OnLevelCleared = null;
        OnPlayerDied = null;
        OnRunWon = null;
    }
}
