using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    private Transform center;
    [SerializeField]
    private Transform observedTarget;
    [SerializeField, Range(-90, 89.99f)]
    private float pitchAngle = 60;
    [SerializeField]
    private float distanceFromTarget;
    [SerializeField]
    private bool shouldRotateAroundCenter = true;

    [SerializeField, Tooltip("Wyprzedzenie kamery w kierunku ruchu gracza (m).")]
    private float lookAhead = 1.5f;

    private Rigidbody targetBody;
    private Vector3 lookAheadOffset;
    private Vector3 lookAheadVelocity;
    private float zoomCurrent = 1f;
    private float zoomVelocity;
    private float punchMultiplier = 1f;
    private float punchTimeLeft;

    /// <summary>Stały mnożnik dystansu (1 = baza), wygładzany 0,5 s.</summary>
    public float ZoomMultiplier = 1f;

    /// <summary>Stały zoom (np. 0,9 w walce z bossem).</summary>
    public void SetZoom(float multiplier) => ZoomMultiplier = multiplier;

    /// <summary>Chwilowe przybliżenie (np. 0,85 na 0,4 s), potem powrót do ZoomMultiplier.</summary>
    public void PunchZoom(float mult, float seconds)
    {
        punchMultiplier = mult;
        punchTimeLeft = seconds;
    }

    /// <summary>Offset wstrząsu (CameraShake), dodawany do pozycji kamery.</summary>
    public Vector3 ShakeOffset { get; set; }

    private void LateUpdate()
    {
        if (Application.isPlaying)
            UpdateSmoothing();
        ApplyPosition();
    }

    private void UpdateSmoothing()
    {
        float dt = Time.unscaledDeltaTime;
        if (observedTarget != null && targetBody == null)
            targetBody = observedTarget.GetComponentInParent<Rigidbody>();

        Vector3 wanted = Vector3.zero;
        if (targetBody != null)
        {
            var v = targetBody.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.01f)
                wanted = v.normalized * lookAhead;
        }
        lookAheadOffset = Vector3.SmoothDamp(lookAheadOffset, wanted, ref lookAheadVelocity, 0.25f, Mathf.Infinity, dt);

        float target = ZoomMultiplier;
        float smooth = 0.5f;
        if (punchTimeLeft > 0f)
        {
            punchTimeLeft -= dt;
            target *= punchMultiplier;
            smooth = 0.1f;
        }
        zoomCurrent = Mathf.SmoothDamp(zoomCurrent, target, ref zoomVelocity, smooth, Mathf.Infinity, dt);
    }

    private void ApplyPosition()
    {
        if (shouldRotateAroundCenter)
        {
            Vector3 targetPosition = observedTarget.position + lookAheadOffset;
            var direction = targetPosition - center.position;
            direction.y = 0;
            var localRight = Vector3.Cross(Vector3.up, direction);
            var lookDirection = (Quaternion.AngleAxis(pitchAngle, localRight) * direction).normalized;

            transform.position = targetPosition - distanceFromTarget * zoomCurrent * lookDirection;
            transform.forward = lookDirection;
            transform.position += ShakeOffset;
        }
        else
        {

        }
    }

    private void OnValidate()
    {
        ApplyPosition();
    }
}
