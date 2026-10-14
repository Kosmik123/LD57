using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Slime: cykl skoku od prędkości agenta, oddech w bezruchu, nadęcie i pociemnienie w windupie, wypad po ciosie.</summary>
[RequireComponent(typeof(VisualRig))]
public class SlimeAnimator : MonoBehaviour
{
    [SerializeField] private float moveThreshold = 0.3f;
    [SerializeField] private float hopPeriod = 0.5f;
    [SerializeField] private float hopHeight = 0.25f;
    [SerializeField] private float breathAmount = 0.03f;
    [SerializeField] private float breathPeriod = 1.2f;
    [SerializeField] private float windupInflate = 1.3f;
    [SerializeField] private float windupSeconds = 0.5f;
    [SerializeField] private float windupDarken = 0.6f;
    [SerializeField] private float lungeDistance = 0.5f;
    [Header("Skok (EnemyMeleeAttack.leap)")]
    [SerializeField, Tooltip("Wysokość Y rozpłaszczenia w windupie (objętość zachowana, więc szerzej).")] private float flattenY = 0.4f;
    [SerializeField] private float leapArc = 1.3f;
    [SerializeField] private float leapStretch = 0.35f;

    private static readonly float[] HopKeys = { 1f, 0.8f, 1.25f, 0.85f, 1f };

    private VisualRig rig;
    private NavMeshAgent agent;
    private EnemyMeleeAttack melee;
    private Health health;
    private RendererTint tint;
    private Tween tintTween;
    private float tintValue = 1f, phase, clock;

    private void Awake()
    {
        rig = GetComponent<VisualRig>();
        agent = GetComponent<NavMeshAgent>();
        melee = GetComponent<EnemyMeleeAttack>();
        health = GetComponent<Health>();
        rig.EnsureInit();
        tint = new RendererTint(rig.Visual, "_BaseColor");
    }

    private void OnEnable()
    {
        if (melee != null) { melee.OnWindupStarted += Windup; melee.OnAttackFinished += Finished; }
        if (health != null) health.OnHealthChanged += HealthChanged;
    }

    private void OnDisable()
    {
        if (melee != null) { melee.OnWindupStarted -= Windup; melee.OnAttackFinished -= Finished; }
        if (health != null) health.OnHealthChanged -= HealthChanged;
        tintTween?.Kill();
        rig.ProcY = 1f; rig.ProcOffset = Vector3.zero;
    }

    private void Update()
    {
        if (!rig.HasVisual) return;
        if (melee != null && melee.LeapProgress >= 0f)
        {
            // lot po łuku: Body w górę i rozciągnięcie w osi Y
            float p = melee.LeapProgress;
            rig.ProcY = 1f + leapStretch * Mathf.Sin(p * Mathf.PI);
            rig.ProcOffset = new Vector3(0f, leapArc * Mathf.Sin(p * Mathf.PI), 0f);
            return;
        }
        float dt = Time.deltaTime;
        float speed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : rig.Speed;
        bool moving = speed > moveThreshold;
        clock += dt;

        if (moving || phase > 0f)
        {
            phase += dt / hopPeriod;
            if (phase >= 1f) phase = moving ? phase - 1f : 0f;
        }
        if (phase > 0f)
        {
            float f = phase * 4f;
            int seg = Mathf.Min(3, (int)f);
            float t = Mathf.SmoothStep(0f, 1f, f - seg);
            rig.ProcY = Mathf.Lerp(HopKeys[seg], HopKeys[seg + 1], t);
            rig.ProcOffset = new Vector3(0f, hopHeight * Mathf.Sin(phase * Mathf.PI), 0f);
        }
        else
        {
            rig.ProcY = 1f + breathAmount * Mathf.Sin(clock * Mathf.PI * 2f / breathPeriod);
            rig.ProcOffset = Vector3.zero;
        }
    }

    private void Windup()
    {
        if (melee != null && melee.IsLeap)
        {
            rig.Crouch(flattenY, windupSeconds); // rozpłaszczenie = zapowiedź skoku
            TweenTint(windupDarken, windupSeconds);
            return;
        }
        rig.Inflate(windupInflate, windupSeconds, Ease.OutQuad);
        TweenTint(windupDarken, windupSeconds);
    }

    private void Finished()
    {
        if (melee != null && melee.IsLeap)
        {
            rig.Squash(0.5f, 0.05f, 0.35f, Ease.OutElastic); // splat lądowania, potem powrót
            TweenTint(1f, 0.25f);
            return;
        }
        rig.Inflate(1f, 0.25f, Ease.OutBack);
        rig.Kick(new Vector3(0f, 0f, lungeDistance), 0.08f, 0.25f, Ease.OutBack);
        TweenTint(1f, 0.25f);
    }

    private void TweenTint(float target, float seconds)
    {
        if (!tint.Any) return;
        tintTween?.Kill();
        tintTween = DOTween.To(() => tintValue, v => { tintValue = v; tint.Set(v); }, target, seconds).SetEase(Ease.OutQuad);
    }

    private void HealthChanged(int change)
    {
        // DamageFlash czyści MaterialPropertyBlock po błysku: przywróć pociemnienie.
        if (change < 0 && tintValue < 0.999f && isActiveAndEnabled)
            DOVirtual.DelayedCall(0.12f, () => { if (this != null && tint != null) tint.Set(tintValue); });
    }
}
