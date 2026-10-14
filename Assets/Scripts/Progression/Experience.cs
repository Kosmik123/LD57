using UnityEngine;

/// <summary>XP i levele bohatera. Słucha EnemyHealth.OnEnemyDied, podnosi GameEvents.OnExperienceGained / OnLevelUp.</summary>
public class Experience : MonoBehaviour
{
    public int HeroLevel { get; private set; }
    public int CurrentXp { get; private set; }
    public int XpToNextLevel => 40 + 13 * HeroLevel;
    public float Progress01 => Mathf.Clamp01((float)CurrentXp / XpToNextLevel);

    private void OnEnable() => EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    private void OnDisable() => EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        AddXp(enemy.ExperienceReward);
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        CurrentXp += amount;
        GameEvents.RaiseExperienceGained(amount);
        while (CurrentXp >= XpToNextLevel)
        {
            CurrentXp -= XpToNextLevel;
            HeroLevel++;
            GameEvents.RaiseLevelUp(HeroLevel);
        }
    }
}
