using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Na roocie prefabu poziomu. Liczy wrogów ze wszystkich stref poziomu; gdy wszyscy zabici,
/// podnosi GameEvents.OnLevelCleared (raz).
/// </summary>
public class LevelEnemyTracker : MonoBehaviour
{
    [SerializeField, Range(1, 8)]
    private int levelNumber = 1;
    public int LevelNumber => levelNumber;

    public int TotalEnemies { get; private set; }
    public int KilledEnemies { get; private set; }
    public int RemainingEnemies => TotalEnemies - KilledEnemies;

    private readonly HashSet<EnemyHealth> enemies = new HashSet<EnemyHealth>();
    private WaveZone[] zones;
    private bool cleared;

    private void OnEnable()
    {
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
        SubscribeZones();
    }

    private void Start()
    {
        zones = GetComponentsInChildren<WaveZone>(true);
        TotalEnemies = 0;
        foreach (var zone in zones)
            TotalEnemies += zone.TotalEnemyCount;
        SubscribeZones();

        // poziom bez wrogów jest od razu wyczyszczony
        if (TotalEnemies == 0)
            Clear();
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
        UnsubscribeZones();
    }

    private void SubscribeZones()
    {
        if (zones == null)
            return;
        foreach (var zone in zones)
        {
            zone.OnEnemySpawned -= Zone_OnEnemySpawned;
            zone.OnEnemySpawned += Zone_OnEnemySpawned;
        }
    }

    private void UnsubscribeZones()
    {
        if (zones == null)
            return;
        foreach (var zone in zones)
            if (zone != null)
                zone.OnEnemySpawned -= Zone_OnEnemySpawned;
    }

    private void Zone_OnEnemySpawned(WaveZone zone, EnemyHealth enemy) => enemies.Add(enemy);

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (cleared || !enemies.Remove(enemy))
            return;

        KilledEnemies++;
        if (KilledEnemies >= TotalEnemies)
            Clear();
    }

    private void Clear()
    {
        if (cleared)
            return;
        cleared = true;
        GameEvents.RaiseLevelCleared(levelNumber);
    }
}
