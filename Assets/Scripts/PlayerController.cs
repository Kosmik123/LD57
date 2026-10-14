using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    /// <summary>Dash rozpoczęty.</summary>
    public event System.Action OnDashed;
    /// <summary>Dash zakończony (po dashDistance / dashSpeed sekund).</summary>
    public event System.Action OnDashEnded;
    public event System.Action OnAttack;
    /// <summary>Gracz wylądował (był w powietrzu, jest na ziemi). Używane przez GroundSlam.</summary>
    public event System.Action OnLanded;

    [SerializeField, Tooltip("Skok jako wolny ruch jest wyłączony. Włącza go dopiero skill GroundSlam (SkillSlots).")]
    private bool jumpEnabled = false;
    public bool JumpEnabled
    {
        get => jumpEnabled;
        set => jumpEnabled = value;
    }

    [Foldout("Rotation"), SerializeField]
    private LookAtCursor lookAtCursor;
    [Foldout("Rotation"), SerializeField]
    private float defaultRotationSpeed = 500f;    

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
    public bool IsGrounded => isGrounded;   

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
    [Foldout("Dash"), SerializeField, Tooltip("Czas (s) po rozpoczęciu dasha, zanim można dashować ponownie.")]
    private float dashCooldown = 0.8f;
    private float dashCooldownRemaining;
    private float currentDashSpeed;
    public float DashCooldownRemaining => Mathf.Max(0f, dashCooldownRemaining);
    [ShowNonSerializedField]
    private bool isDashing;
    public bool IsDashing => isDashing;
    [ShowNonSerializedField]
    private Vector3 dashDirection;

    [Foldout("Attack"), SerializeField]
    private float attackDuration;
    [Foldout("Attack"), SerializeField, ReadOnly]
    private bool isAttacking;
    [Foldout("Attack"), SerializeField]
    private float rotationSpeedWhileAttacking;
    [Foldout("Attack"), SerializeField]
    private GameObject[] damagingObjects;

    [SerializeField]
    private float fallingDownGravityModifier = 3f;

    /// <summary>False dla klas dystansowych: Fire1 nadal wywołuje OnAttack i animację, ale nie włącza damagingObjects.</summary>
    public bool MeleeHitboxes = true;

    private Rigidbody rb;
    
    private float horizontal, vertical;
    private Vector3 localDirection;
    public Vector2 LocalDirection => new Vector2(localDirection.x, localDirection.z);

    private bool jumpRequested;
    private bool dashRequested;
    private bool externalJumpRequested;

    private float rootedUntil;

    /// <summary>Gracz unieruchomiony (sieć bossa): ruch i dash zablokowane, input nadal czytany, atak i skille działają.</summary>
    public bool IsRooted => Time.time < rootedUntil;

    /// <summary>Unieruchamia gracza na `seconds` (dłuższe unieruchomienie wygrywa). Czas liczony Time.time, nie Invoke.</summary>
    public void Root(float seconds)
    {
        if (seconds <= 0f) return;
        rootedUntil = Mathf.Max(rootedUntil, Time.time + seconds);
    }

    /// <summary>Wymusza skok (np. skill GroundSlam). Działa tylko gdy JumpEnabled.</summary>
    public void RequestJump()
    {
        if (jumpEnabled)
            externalJumpRequested = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        lookAtCursor.RotationSpeed = defaultRotationSpeed;
        ResetJump();
    }

    private void Update()
    {
        UpdateInput();
        CheckGrounded();
        
        UpdateMovement();
        UpdateJump();
        UpdateDash();

        UpdateAttack();
    }

    private void UpdateInput()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        // Skok tylko przez RequestJump() (skill GroundSlam z własnym cooldownem). Oś "Jump" nie jest czytana,
        // inaczej spacja skakałaby z pominięciem cooldownu skilla.
        jumpRequested = jumpEnabled && externalJumpRequested;
        externalJumpRequested = false;
        dashRequested = Input.GetButtonDown("Dash");
    }

    private void CheckGrounded()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundPoint.position, groundCheckRadius, groundLayers);
        if (isGrounded && !wasGrounded)
            OnLanded?.Invoke();
    }

    private void UpdateMovement()
    {
        if (isDashing)
            return;

        Vector3 forward = forwardProvider ? forwardProvider.forward : Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        var direction = forward * vertical + right * horizontal;
        if (direction.sqrMagnitude > 1)
            direction.Normalize();

        localDirection = transform.InverseTransformDirection(direction);
        localDirection.x *= sidewaysSpeedModified;
        if (localDirection.z < 0)
            localDirection.z *= backwardsSpeedModified;

        if (IsRooted)
        {
            localDirection = Vector3.zero; // animacje biegu nie grają w sieci
            targetVelocity = Vector3.zero;
            return;
        }

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

    private void UpdateDash()
    {
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

        if (!isDashing && isGrounded && dashRequested && dashCooldownRemaining <= 0f && !IsRooted)
        {
            StartDash();
        }
    }

    /// <summary>
    /// Wymuszony dash w zadanym kierunku (skill Charge). Nie zużywa cooldownu zwykłego dasha.
    /// Podnosi OnDashed, więc DashInvulnerability daje nietykalność także w szarży (decyzja: szarża przez korytarz jest nietykalna).
    /// Zwraca false, gdy gracz już dashuje albo jest w powietrzu.
    /// </summary>
    public bool ForceDash(Vector3 direction, float distance, float speed)
    {
        if (isDashing || IsRooted || !isGrounded || speed <= 0f || distance <= 0f)
            return false;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        dashDirection = direction.normalized;
        transform.forward = dashDirection;
        currentDashSpeed = speed;
        isDashing = true;
        lookAtCursor.enabled = false;
        CancelInvoke(nameof(EndDash));
        Invoke(nameof(EndDash), distance / speed);
        OnDashed?.Invoke();
        return true;
    }

    private void StartDash()
    {
        isDashing = true;
        dashCooldownRemaining = dashCooldown;
        currentDashSpeed = dashSpeed;
        lookAtCursor.enabled = false;
        float dashDuration = dashDistance / dashSpeed;
        var  velocity = rb.linearVelocity;
        velocity.y = 0;
        if (velocity.sqrMagnitude > 0.001f)
        {
            dashDirection = velocity.normalized;
            transform.forward = dashDirection;
        }
        else
        {
            dashDirection = transform.forward;
        }
        Invoke(nameof(EndDash), dashDuration);
        OnDashed?.Invoke();
    }

    private void EndDash()
    {
        isDashing = false;
        lookAtCursor.enabled = true;
        OnDashEnded?.Invoke();
    }

    private void UpdateAttack()
    {
        if (isDashing || isAttacking)
            return;

        if (Input.GetButtonDown("Fire1"))
        {
            if (MeleeHitboxes)
            {
                foreach (var damagingObject in damagingObjects)
                {
                    damagingObject.SetActive(true);
                }
            }
            isAttacking = true;
            lookAtCursor.RotationSpeed = rotationSpeedWhileAttacking;
            OnAttack?.Invoke();
            Invoke(nameof(StopAttack), attackDuration);
        }
    }

    private void StopAttack()
    {
        foreach (var damagingObject in damagingObjects)
        {
            damagingObject.SetActive(false);
        }
        lookAtCursor.RotationSpeed = defaultRotationSpeed;
        isAttacking = false;
    }

    public bool CanJump() => jumpEnabled && !isDashing && !hasJumped && (isGrounded || coyoteTimeCounter > 0);

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

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * currentDashSpeed;
        }
        else
        {
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
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
