using DG.Tweening;
using UnityEngine;

/// <summary>Pająk: chód nóg od prędkości, przyczajenie (Perched), wydłużenie w locie (Dropping), squash lądowania, wypad po ciosie.</summary>
[RequireComponent(typeof(VisualRig))]
public class SpiderAnimator : MonoBehaviour
{
    [SerializeField] private float legAmplitude = 25f;
    [SerializeField] private float legFrequency = 6f;
    [SerializeField, Tooltip("Prędkość odniesienia, przy której amplituda i częstotliwość są nominalne.")] private float referenceSpeed = 6f;
    [SerializeField] private float perchedSquash = 0.7f;
    [SerializeField, Tooltip("Złożenie nóg na suficie (stopnie wokół lokalnej osi X).")] private float perchedFold = 35f;
    [SerializeField, Tooltip("Obrót nóg w dół podczas zeskoku (znak zależy od orientacji nóg w modelu).")] private float dropFold = -40f;
    [SerializeField] private float dropStretch = 1.4f;
    [SerializeField] private float landingSquash = 0.6f;
    [SerializeField] private float lungeDistance = 0.5f;
    [SerializeField] private float meleeCrouch = 0.75f;
    [SerializeField] private float meleeCrouchSeconds = 0.3f;

    private VisualRig rig;
    private SpiderController spider;
    private EnemyMeleeAttack melee;
    private SpiderController.State? last;
    private float fold, phase, walkBlend;
    private Tween foldTween;

    private void Awake()
    {
        rig = GetComponent<VisualRig>();
        spider = GetComponent<SpiderController>();
        melee = GetComponent<EnemyMeleeAttack>();
        rig.EnsureInit();
    }

    private void OnEnable()
    {
        if (melee != null) { melee.OnWindupStarted += Windup; melee.OnAttackFinished += Finished; }
    }

    private void OnDisable()
    {
        if (melee != null) { melee.OnWindupStarted -= Windup; melee.OnAttackFinished -= Finished; }
        foldTween?.Kill();
    }

    private void Update()
    {
        if (!rig.HasVisual) return;
        if (spider != null && last != spider.Current)
            EnterState(spider.Current);

        float dt = Time.deltaTime;
        float k = Mathf.Clamp(rig.Speed / referenceSpeed, 0f, 2f);
        bool walking = rig.Speed > 0.3f && (spider == null || spider.Current == SpiderController.State.Chasing);
        walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt * 6f);
        phase += Mathf.PI * 2f * legFrequency * k * dt;
        float amp = legAmplitude * k * walkBlend;
        for (int i = 0; i < VisualRig.MaxLegs; i++)
        {
            float parity = (i & 1) == 0 ? 0f : Mathf.PI;
            rig.SetLegAngle(i, fold + amp * Mathf.Sin(phase + parity));
        }
    }

    private void EnterState(SpiderController.State state)
    {
        var prev = last;
        last = state;
        switch (state)
        {
            case SpiderController.State.Perched:
                rig.HoldPose(perchedSquash, 0.3f);
                TweenFold(perchedFold, 0.3f);
                break;
            case SpiderController.State.Dropping:
                rig.HoldPose(dropStretch, 0.15f);
                TweenFold(dropFold, 0.15f);
                break;
            default:
                rig.HoldPose(1f, 0.05f);
                TweenFold(0f, 0.2f);
                if (prev == SpiderController.State.Dropping)
                    rig.Squash(landingSquash, 0f, 0.4f, Ease.OutElastic);
                break;
        }
    }

    private void TweenFold(float target, float seconds)
    {
        foldTween?.Kill();
        foldTween = DOTween.To(() => fold, v => fold = v, target, seconds).SetEase(Ease.OutQuad);
    }

    private void Windup() => rig.Crouch(meleeCrouch, meleeCrouchSeconds);

    private void Finished()
    {
        rig.Release(0.3f);
        rig.Kick(new Vector3(0f, 0f, lungeDistance), 0.08f, 0.25f, Ease.OutBack);
    }
}
