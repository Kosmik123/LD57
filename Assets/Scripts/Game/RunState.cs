using UnityEngine;

/// <summary>
/// Statystyki bieżącego runu do ekranu wyniku. Czas liczony przez Time.deltaTime,
/// więc zatrzymuje się w pauzie (karty, śmierć).
/// </summary>
public class RunState : MonoBehaviour
{
    public float StartTime { get; private set; }
    public float ElapsedTime { get; private set; }
    public int DeepestLevel { get; private set; }
    public int Kills { get; private set; }
    public int HeroLevel { get; private set; }

    private void Awake()
    {
        StartTime = Time.time;
    }

    private void OnEnable()
    {
        GameEvents.OnLevelStarted += GameEvents_OnLevelStarted;
        GameEvents.OnLevelUp += GameEvents_OnLevelUp;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelStarted -= GameEvents_OnLevelStarted;
        GameEvents.OnLevelUp -= GameEvents_OnLevelUp;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
    }

    private void Update()
    {
        ElapsedTime += Time.deltaTime;
    }

    private void GameEvents_OnLevelStarted(int level) => DeepestLevel = Mathf.Max(DeepestLevel, level);
    private void GameEvents_OnLevelUp(int heroLevel) => HeroLevel = heroLevel;
    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy) => Kills++;
}
