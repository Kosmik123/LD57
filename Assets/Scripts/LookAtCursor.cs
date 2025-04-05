using NaughtyAttributes;
using UnityEngine;

public class LookAtCursor : MonoBehaviour
{
    private Camera viewCamera;

    [ShowNonSerializedField]
    private float angle;

    private void Awake()
    {
        viewCamera = Camera.main;
    }

    private void Update()
    {
        var screenPosition = viewCamera.WorldToScreenPoint(transform.position);
        var mousePosition = Input.mousePosition;
        Vector2 screenDirection = mousePosition - screenPosition;
        angle = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
    }
}
