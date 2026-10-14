using UnityEngine;

/// <summary>Reakcja na trafienie i śmierć (każdy wróg): squash + punch od strony gracza, zapadnięcie przy śmierci.</summary>
[RequireComponent(typeof(Health), typeof(VisualRig))]
public class HitReaction : MonoBehaviour
{
    [SerializeField] private float hitSquash = 0.85f;
    [SerializeField] private float hitSeconds = 0.06f;
    [SerializeField] private float recoverSeconds = 0.3f;
    [SerializeField] private float punchAmount = 0.15f;
    [SerializeField, Tooltip("Mniejsze od destroyDelay w EnemyDeath (0,5 s).")] private float collapseSeconds = 0.4f;

    private Health health;
    private VisualRig rig;

    private void Awake()
    {
        health = GetComponent<Health>();
        rig = GetComponent<VisualRig>();
    }

    private void OnEnable()
    {
        health.OnHealthChanged += OnHealthChanged;
        health.OnDied += OnDied;
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= OnHealthChanged;
        health.OnDied -= OnDied;
    }

    private void OnHealthChanged(int change)
    {
        if (change >= 0 || health.IsDead || !rig.HasVisual) return;
        rig.Squash(hitSquash, hitSeconds, recoverSeconds, DG.Tweening.Ease.OutElastic);
        rig.Punch(rig.DirectionAwayFromPlayerLocal(), punchAmount);
    }

    private void OnDied()
    {
        foreach (var a in GetComponents<SlimeAnimator>()) a.enabled = false;
        foreach (var a in GetComponents<SpiderAnimator>()) a.enabled = false;
        foreach (var a in GetComponents<ShooterAnimator>()) a.enabled = false;
        foreach (var a in GetComponents<BossAnimator>()) a.enabled = false;
        rig.Collapse(collapseSeconds);
    }
}
