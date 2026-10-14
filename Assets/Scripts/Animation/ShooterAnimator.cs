using DG.Tweening;
using UnityEngine;

/// <summary>Strzelec: kołysanie Body, rozbudowa rdzenia i emisji w windupie, odrzut po strzale.</summary>
[RequireComponent(typeof(VisualRig))]
public class ShooterAnimator : MonoBehaviour
{
    [SerializeField] private float swayDegrees = 4f;
    [SerializeField] private float swayHz = 1.5f;
    [SerializeField] private float coreScale = 1.8f;
    [SerializeField] private float emissionMultiplier = 3f;
    [SerializeField, Tooltip("Czas narastania rdzenia (zbliżony do windupu strzelca).")] private float windupSeconds = 0.6f;
    [SerializeField] private float recoil = 0.2f;

    private VisualRig rig;
    private ShooterController shooter;
    private Transform core;
    private Vector3 coreBase = Vector3.one;
    private RendererTint emission;
    private float coreFactor = 1f, emissionFactor = 1f;
    private Tween coreTween, emissionTween;
    private float clock;

    private void Awake()
    {
        rig = GetComponent<VisualRig>();
        shooter = GetComponent<ShooterController>();
        rig.EnsureInit();
        if (rig.Visual != null)
        {
            core = rig.Visual.Find("Core");
            if (core != null)
            {
                coreBase = core.localScale;
                emission = new RendererTint(core, "_EmissionColor");
            }
        }
    }

    private void OnEnable()
    {
        if (shooter != null) { shooter.OnWindupStarted += Windup; shooter.OnShot += Shot; }
    }

    private void OnDisable()
    {
        if (shooter != null) { shooter.OnWindupStarted -= Windup; shooter.OnShot -= Shot; }
        coreTween?.Kill(); emissionTween?.Kill();
        rig.ProcEuler = Vector3.zero;
    }

    private void Update()
    {
        if (!rig.HasVisual) return;
        clock += Time.deltaTime;
        rig.ProcEuler = new Vector3(0f, 0f, swayDegrees * Mathf.Sin(clock * Mathf.PI * 2f * swayHz));
    }

    private void LateUpdate()
    {
        if (core != null) core.localScale = coreBase * coreFactor;
    }

    private void Windup() => TweenCore(coreScale, emissionMultiplier, windupSeconds, Ease.InOutSine);

    private void Shot()
    {
        rig.Kick(new Vector3(0f, 0f, -recoil), 0.05f, 0.3f, Ease.OutBack);
        TweenCore(1f, 1f, 0.2f, Ease.OutBack);
    }

    private void TweenCore(float scale, float emissionMul, float seconds, Ease ease)
    {
        coreTween?.Kill(); emissionTween?.Kill();
        coreTween = DOTween.To(() => coreFactor, v => coreFactor = v, scale, seconds).SetEase(ease);
        if (emission != null && emission.Any)
            emissionTween = DOTween.To(() => emissionFactor, v => { emissionFactor = v; emission.Set(v); }, emissionMul, seconds).SetEase(Ease.InOutSine);
    }
}
