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

    [Header("Wspinaczka i bieg po ścianie")]
    [SerializeField, Tooltip("Ciało nad powierzchnią (m), nogi rozłożone szeroko.")] private float climbBodyLift = 0.15f;
    [SerializeField] private float climbSpread = 20f;
    [SerializeField] private float climbAmplitude = 40f;
    [SerializeField, Tooltip("Szybki cykl chodu po ścianie (Hz).")] private float climbFrequency = 10f;
    [SerializeField, Tooltip("Rozłożenie nóg przy lądowaniu po zeskoku (stopnie), potem powrót do 0.")] private float landingSpread = 30f;
    [SerializeField, Tooltip("Mnożnik częstotliwości nóg podczas biegu po ścianie (WallWalk).")] private float wallRunLegBoost = 1.35f;
    [SerializeField, Tooltip("Błysk oczu po wykryciu gracza: skala i czas (s).")] private float eyeFlashScale = 2.2f;
    [SerializeField] private float eyeFlashSeconds = 0.4f;

    private VisualRig rig;
    private SpiderController spider;
    private EnemyMeleeAttack melee;
    private SpiderController.State? last;
    private float fold, phase, walkBlend, crawlBlend;
    private Tween foldTween, eyeFlashTween;

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
        if (spider != null) spider.OnSpotted += FlashEyes;
    }

    private void OnDisable()
    {
        if (melee != null) { melee.OnWindupStarted -= Windup; melee.OnAttackFinished -= Finished; }
        if (spider != null) spider.OnSpotted -= FlashEyes;
        foldTween?.Kill();
        eyeFlashTween?.Kill();
    }

    /// <summary>Wykrycie gracza: oczy rozbłyskują i wracają (Visual/Eyes to 2 świecące bryły EyeL/EyeR).</summary>
    private void FlashEyes()
    {
        if (!rig.HasVisual) return;
        rig.SetEyes(eyeFlashScale, 0.08f);
        eyeFlashTween?.Kill();
        eyeFlashTween = DOVirtual.DelayedCall(eyeFlashSeconds, () => { if (rig != null) rig.SetEyes(1f, 0.25f); });
    }

    private void Update()
    {
        if (!rig.HasVisual) return;
        if (spider != null && last != spider.Current)
            EnterState(spider.Current);

        float dt = Time.deltaTime;
        float k = Mathf.Clamp(rig.Speed / referenceSpeed, 0f, 2f);
        bool crawling = spider != null && spider.OnSurface; // Climbing, WallWalk i Perched na ścianie: ciało 0,15 m nad powierzchnią
        bool wallRun = spider != null && spider.Current == SpiderController.State.WallWalk;
        bool walking = rig.Speed > 0.3f && (spider == null || spider.Current == SpiderController.State.Chasing || crawling);
        walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt * 6f);
        crawlBlend = Mathf.MoveTowards(crawlBlend, crawling ? 1f : 0f, dt * 5f);
        // ciało przyklejone do powierzchni (0,15 m nad nią), po wyjściu na półkę wraca płynnie do 0
        rig.ProcOffset = new Vector3(0f, climbBodyLift * crawlBlend, 0f);
        phase += Mathf.PI * 2f * Mathf.Lerp(legFrequency * k, climbFrequency * (wallRun ? wallRunLegBoost : 1f), crawlBlend) * dt;
        float amp = Mathf.Lerp(legAmplitude * k, climbAmplitude, crawlBlend) * walkBlend;
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
                if (spider != null && spider.OnSurface)
                {
                    // na ścianie: nogi rozłożone na powierzchni, bez przyczajenia sufitowego
                    rig.HoldPose(1f, 0.2f);
                    TweenFold(climbSpread, 0.25f);
                }
                else
                {
                    rig.HoldPose(perchedSquash, 0.3f);
                    TweenFold(perchedFold, 0.3f);
                }
                break;
            case SpiderController.State.Dropping:
                rig.HoldPose(dropStretch, 0.15f);
                TweenFold(dropFold, 0.15f);
                break;
            case SpiderController.State.Climbing:
            case SpiderController.State.WallWalk:
                rig.HoldPose(1f, 0.1f);
                TweenFold(climbSpread, 0.2f);
                break;
            case SpiderController.State.Leap:
                rig.HoldPose(1f, 0.05f);
                TweenFold(dropFold * 0.6f, 0.3f); // nogi podkulone przed i w trakcie skoku
                break;
            default:
                rig.HoldPose(1f, 0.05f);
                if (prev == SpiderController.State.Dropping)
                {
                    // lądowanie: nogi rozłożone, potem wracają do pozy; squash
                    foldTween?.Kill();
                    foldTween = DOTween.Sequence()
                        .Append(DOTween.To(() => fold, v => fold = v, landingSpread, 0.08f).SetEase(Ease.OutQuad))
                        .Append(DOTween.To(() => fold, v => fold = v, 0f, 0.35f).SetEase(Ease.OutQuad));
                    rig.Squash(landingSquash, 0f, 0.4f, Ease.OutElastic);
                }
                else
                    TweenFold(0f, prev == SpiderController.State.Climbing ? 0.5f : 0.2f);
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
