using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    private Transform center;
    [SerializeField]
    private Transform observedTarget;
    [SerializeField, Range(-90, 90)]
    private float pitchAngle = 60;
    [SerializeField]
    private float distanceFromTarget;

    private void LateUpdate()
    {
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        Vector3 targetPosition = observedTarget.position;
        var direction = targetPosition - center.position;
        direction.y = 0;
        var localRight = Vector3.Cross(Vector3.up, direction);
        var lookDirection = (Quaternion.AngleAxis(pitchAngle, localRight) * direction).normalized;

        transform.position = targetPosition - distanceFromTarget * lookDirection;
        transform.forward = lookDirection;
    }

    private void OnValidate()
    {
        ApplyPosition();
    }
}
