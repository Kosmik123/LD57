using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleCharacterMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 4;
    [SerializeField]
    private float rotateSpeed = 200;
    [SerializeField]
    private Transform forwardProvider;

    [Header("Controls")]
    [SerializeField]
#if NAUGHTY_ATTRIBUTES
	[NaughtyAttributes.InputAxis]
#endif
    private string horizontalAxis = "Horizontal";
    [SerializeField]
#if NAUGHTY_ATTRIBUTES
	[NaughtyAttributes.InputAxis]
#endif
    private string verticalAxis = "Vertical";

    private CharacterController characterController;

    private void Reset()
    {
        forwardProvider = transform;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector3 forward = forwardProvider ? forwardProvider.forward : Vector3.forward;
        forward.y = 0;
        if (forward.sqrMagnitude != 1)
            forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float horizontal = Input.GetAxis(horizontalAxis);
        float vertical = Input.GetAxis(verticalAxis);

        Vector3 direction = horizontal * right + vertical * forward;
        if (direction.sqrMagnitude > 1)
            direction.Normalize();

        if (moveSpeed > 0)
            characterController.SimpleMove(moveSpeed * direction);

        if (rotateSpeed > 0)
        {
            float lookAngle = Vector3.SignedAngle(Vector3.forward, direction, Vector3.up);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.AngleAxis(lookAngle, Vector3.up), Time.deltaTime * rotateSpeed);
        }
    }
}