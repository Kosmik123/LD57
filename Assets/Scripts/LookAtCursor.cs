using NaughtyAttributes;
using UnityEngine;

public class LookAtCursor : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 5f;

    private Camera viewCamera;

    [ShowNonSerializedField]
    private float targetAngle;
    [ShowNonSerializedField]
    private float currentAngle; 

    private void Awake()
    {
        viewCamera = Camera.main;
    }

    private void OnEnable()
    {
        currentAngle = transform.rotation.eulerAngles.y;
    }

    private void Update()
    {
        var screenPosition = viewCamera.WorldToScreenPoint(transform.position);
        var mousePosition = Input.mousePosition;
        Vector2 screenDirection = mousePosition - screenPosition;
        targetAngle = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg;
        
        currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Time.deltaTime * rotationSpeed);
        transform.rotation = Quaternion.AngleAxis(currentAngle, Vector3.up);
    }
}
