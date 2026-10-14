using UnityEngine;

/// <summary>Podpina zdarzenia gry pod efekty dźwiękowe. Komponent w scenie (obiekt Sfx).</summary>
public class SfxHooks : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Health playerHealth;

    private SkillSlots slots;

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnDashed += Player_OnDashed;
            player.OnAttack += Player_OnAttack;
            slots = player.GetComponentInChildren<SkillSlots>(true);
            if (slots == null) slots = player.GetComponentInParent<SkillSlots>();
        }
        if (slots == null) slots = FindFirstObjectByType<SkillSlots>();
        if (slots != null) slots.OnSkillChanged += Slots_OnSkillChanged;
        if (playerHealth != null) playerHealth.OnHealthChanged += PlayerHealth_OnHealthChanged;

        EnemyHealth.OnEnemyDamaged += EnemyHealth_OnEnemyDamaged;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
        GameEvents.OnLevelUp += GameEvents_OnLevelUp;
        GameEvents.OnWaveCleared += GameEvents_OnWaveCleared;
        GameEvents.OnPlayerDied += GameEvents_OnPlayerDied;
        GameEvents.OnRunWon += GameEvents_OnRunWon;
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnDashed -= Player_OnDashed;
            player.OnAttack -= Player_OnAttack;
        }
        if (slots != null) slots.OnSkillChanged -= Slots_OnSkillChanged;
        slots = null;
        if (playerHealth != null) playerHealth.OnHealthChanged -= PlayerHealth_OnHealthChanged;

        EnemyHealth.OnEnemyDamaged -= EnemyHealth_OnEnemyDamaged;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
        GameEvents.OnLevelUp -= GameEvents_OnLevelUp;
        GameEvents.OnWaveCleared -= GameEvents_OnWaveCleared;
        GameEvents.OnPlayerDied -= GameEvents_OnPlayerDied;
        GameEvents.OnRunWon -= GameEvents_OnRunWon;
    }

    private Vector3 PlayerPos => player != null ? player.transform.position : Vector3.zero;

    private void Player_OnDashed() => Sfx.Play(SfxKind.Dash, PlayerPos);
    private void Player_OnAttack() => Sfx.Play(player != null && !player.MeleeHitboxes ? SfxKind.Cast : SfxKind.Attack, PlayerPos); // mag: rzut, nie miecz
    private void Slots_OnSkillChanged(SkillInstance _) => Sfx.Play(SfxKind.CardPick);

    private void PlayerHealth_OnHealthChanged(int change)
    {
        if (change < 0) Sfx.Play(SfxKind.PlayerHurt, PlayerPos);
    }

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth enemy, int damage)
    {
        if (enemy != null) Sfx.Play(SfxKind.Hit, enemy.transform.position);
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (enemy != null) Sfx.Play(SfxKind.EnemyDie, enemy.transform.position);
    }

    private void GameEvents_OnLevelUp(int level) => Sfx.Play(SfxKind.LevelUp);
    private void GameEvents_OnWaveCleared(WaveZone zone) => Sfx.Play(SfxKind.WaveCleared);
    private void GameEvents_OnPlayerDied() => Sfx.Play(SfxKind.Death);
    private void GameEvents_OnRunWon() => Sfx.Play(SfxKind.Win);
}
