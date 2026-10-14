using DG.Tweening;
using UnityEngine;

/// <summary>Boss Matka Pająk: pozy per stan (OnStateChanged), nogi jak u pająka z większą amplitudą, oczy zależne od fazy.</summary>
[RequireComponent(typeof(VisualRig))]
public class BossAnimator : MonoBehaviour
{
    [SerializeField] private float ceilingSwayDegrees = 6f;
    [SerializeField] private float ceilingSwayHz = 0.5f;
    [SerializeField] private float ceilingFold = 30f;
    [SerializeField] private float dropStretch = 1.5f;
    [SerializeField] private float landingSquash = 0.55f;
    [SerializeField] private float jitterAmount = 0.05f;
    [SerializeField] private float jitterSeconds = 0.4f;
    [SerializeField] private float legAmplitude = 25f;
    [SerializeField] private float chargeLegBoost = 1.5f;
    [SerializeField] private float legFrequency = 6f;
    [SerializeField] private float referenceSpeed = 6f;
    [SerializeField] private float summonEyes = 1.3f;
    [SerializeField] private float phase3Eyes = 1.6f;

    private VisualRig rig;
    private SpiderMotherBoss boss;
    private string state = "";
    private bool swaying, charging;
    private float jitterUntil, clock, fold, legPhase, walkBlend;
    private int bossPhase = 1;
    private Tween foldTween;
    private System.Reflection.EventInfo phaseEvent;
    private System.Delegate phaseHandler;

    private void Awake()
    {
        rig = GetComponent<VisualRig>();
        boss = GetComponent<SpiderMotherBoss>();
        rig.EnsureInit();
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.OnStateChanged += StateChanged;
        boss.OnTelegraph += Telegraph;
        // Opcjonalne zdarzenie OnPhaseChanged(int): brak = pomijamy bez błędu.
        try
        {
            phaseEvent = typeof(SpiderMotherBoss).GetEvent("OnPhaseChanged");
            if (phaseEvent != null && phaseEvent.EventHandlerType == typeof(System.Action<int>))
            {
                phaseHandler = System.Delegate.CreateDelegate(phaseEvent.EventHandlerType, this, nameof(PhaseChanged));
                phaseEvent.AddEventHandler(boss, phaseHandler);
            }
            else phaseEvent = null;
        }
        catch (System.Exception) { phaseEvent = null; phaseHandler = null; }
    }

    private void OnDisable()
    {
        if (boss != null)
        {
            boss.OnStateChanged -= StateChanged;
            boss.OnTelegraph -= Telegraph;
            if (phaseEvent != null && phaseHandler != null)
            {
                try { phaseEvent.RemoveEventHandler(boss, phaseHandler); } catch (System.Exception) { }
            }
        }
        foldTween?.Kill();
        rig.ProcEuler = Vector3.zero; rig.ProcOffset = Vector3.zero;
    }

    public void PhaseChanged(int phase)
    {
        bossPhase = phase;
        rig.SetEyes(EyeTarget(), 0.3f);
    }

    private float EyeTarget() => bossPhase >= 3 ? phase3Eyes : (state == "Summon" ? summonEyes : 1f);

    private void Telegraph(Vector3 point)
    {
        // Celowo bez dodatkowej animacji: zostaje stretch ze stanu Drop.
    }

    private void StateChanged(string newState)
    {
        state = newState;
        swaying = newState == "OnCeiling";
        charging = newState == "Charge";
        switch (newState)
        {
            case "OnCeiling":
                rig.HoldPose(1f, 0.3f);
                TweenFold(ceilingFold, 0.3f);
                break;
            case "Drop":
                rig.HoldPose(dropStretch, 0.2f);
                TweenFold(-ceilingFold, 0.2f);
                break;
            case "Charge":
                rig.HoldPose(1f, 0.05f);
                TweenFold(0f, 0.2f);
                rig.Squash(landingSquash, 0f, 0.5f, Ease.OutElastic);
                jitterUntil = Time.time + jitterSeconds;
                break;
            case "Summon":
                rig.HoldPose(1f, 0.2f);
                TweenFold(0f, 0.2f);
                rig.Pulse(1.15f, 3, 0.5f);
                break;
            case "ReturnToCeiling":
                rig.HoldPose(1.3f, 0.3f, Ease.OutBack);
                TweenFold(ceilingFold, 0.3f);
                break;
        }
        rig.SetEyes(EyeTarget(), 0.25f);
    }

    private void TweenFold(float target, float seconds)
    {
        foldTween?.Kill();
        foldTween = DOTween.To(() => fold, v => fold = v, target, seconds).SetEase(Ease.OutQuad);
    }

    private void Update()
    {
        if (!rig.HasVisual) return;
        float dt = Time.deltaTime;
        clock += dt;

        rig.ProcEuler = swaying
            ? new Vector3(0f, 0f, ceilingSwayDegrees * Mathf.Sin(clock * Mathf.PI * 2f * ceilingSwayHz))
            : Vector3.zero;
        rig.ProcOffset = Time.time < jitterUntil
            ? new Vector3(Random.Range(-jitterAmount, jitterAmount), 0f, Random.Range(-jitterAmount, jitterAmount))
            : Vector3.zero;

        float k = Mathf.Clamp(rig.Speed / referenceSpeed, 0f, 2f);
        walkBlend = Mathf.MoveTowards(walkBlend, (charging && rig.Speed > 0.3f) ? 1f : 0f, dt * 6f);
        legPhase += Mathf.PI * 2f * legFrequency * k * dt;
        float amp = legAmplitude * chargeLegBoost * k * walkBlend;
        for (int i = 0; i < VisualRig.MaxLegs; i++)
            rig.SetLegAngle(i, fold + amp * Mathf.Sin(legPhase + ((i & 1) == 0 ? 0f : Mathf.PI)));
    }
}
