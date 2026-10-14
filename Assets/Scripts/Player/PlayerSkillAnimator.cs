using DG.Tweening;
using UnityEngine;

/// <summary>Animacje proceduralne modelu gracza (Body) przy użyciu skilli. Nigdy nie rusza roota.</summary>
public class PlayerSkillAnimator : MonoBehaviour
{
    [SerializeField] private SkillUser skillUser;
    [SerializeField] private PlayerController player;
    [SerializeField, Tooltip("Dziecko roota gracza z modelem.")]
    private Transform body;

    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Sequence current;
    private bool slamPending;
    private PlayerFeel feel;

    /// <summary>True, gdy czeka squash po GroundSlam (PlayerFeel wtedy nie robi własnego).</summary>
    public bool SlamPending => slamPending;

    private void Awake()
    {
        feel = GetComponent<PlayerFeel>();
        if (skillUser == null) skillUser = GetComponent<SkillUser>();
        if (player == null) player = GetComponent<PlayerController>();
        if (body == null) body = transform.Find("Body");
        if (body == null) { enabled = false; return; }
        baseScale = body.localScale;
        baseRotation = body.localRotation;
    }

    private void OnEnable()
    {
        if (skillUser != null) skillUser.OnSkillExecuted += SkillUser_OnSkillExecuted;
        if (player != null) player.OnLanded += Player_OnLanded;
    }

    private void OnDisable()
    {
        if (skillUser != null) skillUser.OnSkillExecuted -= SkillUser_OnSkillExecuted;
        if (player != null) player.OnLanded -= Player_OnLanded;
        ResetBody();
    }

    private void ResetBody()
    {
        if (current != null && current.IsActive()) current.Kill();
        current = null;
        if (body == null) return;
        body.localScale = baseScale;
        body.localRotation = baseRotation;
    }

    private void SkillUser_OnSkillExecuted(SkillKind kind, SkillTier tier)
    {
        if (body == null) return;
        ResetBody();
        current = DOTween.Sequence().SetTarget(body);
        switch (kind)
        {
            case SkillKind.Spin:
                var from = baseRotation.eulerAngles;
                current.Append(body.DOLocalRotate(from + new Vector3(0f, 360f, 0f), Mathf.Max(0.1f, tier.Duration), RotateMode.FastBeyond360).SetEase(Ease.Linear));
                break;
            case SkillKind.Charge:
                if (feel != null) feel.Streaks(transform.forward, 3);
                current.Append(body.DOLocalRotateQuaternion(baseRotation * Quaternion.Euler(25f, 0f, 0f), 0.08f));
                current.Append(body.DOLocalRotateQuaternion(baseRotation, 0.3f).SetEase(Ease.OutQuad));
                break;
            case SkillKind.GroundSlam:
                slamPending = true;
                current.Kill();
                current = null;
                return;
            case SkillKind.WarCry:
                body.localScale = baseScale * 1.2f;
                current.Append(body.DOScale(baseScale, 0.35f).SetEase(Ease.OutBack));
                break;
            case SkillKind.AxeThrow:
                current.Append(body.DOLocalRotateQuaternion(baseRotation * Quaternion.Euler(0f, 20f, 0f), 0.1f));
                current.Append(body.DOLocalRotateQuaternion(baseRotation, 0.1f));
                break;
            default:
                current.Kill();
                current = null;
                return;
        }
        current.OnComplete(() => { body.localScale = baseScale; body.localRotation = baseRotation; });
    }

    private void Player_OnLanded()
    {
        if (!slamPending || body == null) return;
        slamPending = false;
        ResetBody();
        current = DOTween.Sequence().SetTarget(body);
        current.Append(body.DOScale(new Vector3(baseScale.x * 1.2f, baseScale.y * 0.8f, baseScale.z * 1.2f), 0.08f));
        current.Append(body.DOScale(baseScale, 0.3f).SetEase(Ease.OutElastic));
        current.OnComplete(() => body.localScale = baseScale);
    }
}
