using NaughtyAttributes;
using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Foldout("Movement")]
    [SerializeField]
    private Transform forwardProvider;
    [Foldout("Movement")]
    [SerializeField]
    private float moveSpeed = 5f;

    [Foldout("Movement")]
    [SerializeField, Range(0, 1)]
    private float sidewaysSpeedModified = 0.9f;
    [Foldout("Movement")]
    [SerializeField, Range(0, 1)]
    private float backwardsSpeedModified = 0.8f;
    [Foldout("Movement")]
    [SerializeField, Range(0, 1)]
    private float inAirSpeedModified = 0.5f;

    [Foldout("Movement")]
    [ShowNonSerializedField]
    private Vector3 targetVelocity;

    [Foldout("Grounded"), SerializeField]
    private Transform groundPoint;
    [Foldout("Grounded"), SerializeField]
    private float groundCheckRadius = 0.1f;
    [Foldout("Grounded"), SerializeField]
    private LayerMask groundLayers;
    [Foldout("Grounded")]
    [ShowNonSerializedField]
    private bool isGrounded;

    [Foldout("Jump"), SerializeField]
    private float jumpSpeed;
    [Foldout("Jump"), SerializeField]
    private float coyoteTime = 0.2f;
    private float coyoteTimeCounter;
    [Foldout("Jump"), SerializeField]
    private float inputBuffer = 0.2f;
    private float inputBufferCounter;
    private bool hasJumped;
    
    [Foldout("Dash"), SerializeField]
    private float dashSpeed;
    [Foldout("Dash"), SerializeField]
    private float dashDistance;

    [SerializeField]
    private float fallingDownGravityModifier = 3f;

    private Rigidbody rb;
    
    private float horizontal, vertical;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        ResetJump();
    }

    private void Update()
    {
        UpdateInput();
        CheckGrounded();
        
        UpdateMovement();
        UpdateJump();
    }

    private void UpdateInput()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        jumpRequested = Input.GetButtonDown("Jump");
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.CheckSphere(groundPoint.position, groundCheckRadius, groundLayers);
    }

    private void UpdateMovement()
    {
        Vector3 forward = forwardProvider ? forwardProvider.forward : Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        var direction = forward * vertical + right * horizontal;
        if (direction.sqrMagnitude > 1)
            direction.Normalize();

        var localDirection = transform.InverseTransformDirection(direction);
        localDirection.x *= sidewaysSpeedModified;
        if (localDirection.z < 0)
            localDirection.z *= backwardsSpeedModified;

        direction = transform.TransformDirection(localDirection);
        if (isGrounded == false)
            direction *= inAirSpeedModified;

        targetVelocity = direction * moveSpeed;
    }

    private void UpdateJump()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (jumpRequested)
        {
            inputBufferCounter = inputBuffer;
        }
        else
        {
            inputBufferCounter -= Time.deltaTime;
        }
    }

    public bool CanJump() => !hasJumped && (isGrounded || coyoteTimeCounter > 0);

    public bool WantsToJump() => jumpRequested || inputBufferCounter > 0;

    private void FixedUpdate()
    {
        if (CanJump() && WantsToJump())
        {
            hasJumped = true;
            rb.AddForce(Vector3.up * jumpSpeed, ForceMode.VelocityChange);
            jumpRequested = false;
            inputBufferCounter = 0;
            coyoteTimeCounter = 0;
            Invoke(nameof(ResetJump), 0.4f);
        }
        if (rb.linearVelocity.y < 0)
        {
            rb.AddForce((fallingDownGravityModifier - 1) * Physics.gravity.y * Vector3.up, ForceMode.Acceleration);
        }
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    private void ResetJump() => hasJumped = false;

    private void OnDrawGizmosSelected()
    {
        if (groundPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundPoint.position, groundCheckRadius);
        }
    }
}
