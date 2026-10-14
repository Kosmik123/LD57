using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pająk: siedzi na ścianie (punkt spawnu poza NavMeshem), zeskakuje na gracza po paraboli, potem goni.
/// Atak robi EnemyMeleeAttack (autoAttack), włączany dopiero po lądowaniu.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class SpiderController : MonoBehaviour
{
    public enum State { Perched, Dropping, Chasing, Retreat, Circle, Leap }

    [SerializeField] private bool perchOnSpawn = true;
    [SerializeField, Tooltip("Zasięg wykrycia (czujność).")] private float detectRange = 12f;
    [SerializeField, Tooltip("Zeskok, gdy gracz bliżej niż tyle (albo pająk dostał obrażenia / alarm).")] private float dropTriggerRange = 6f;
    [SerializeField] private float dropTelegraph = 0.4f;
    [SerializeField] private float dropConeAngle = 40f;
    [SerializeField] private float dropDuration = 0.6f;
    [SerializeField] private float dropArcHeight = 2f;
    [SerializeField] private float maxDropDistance = 6f;
    [SerializeField] private float chaseSpeed = 6f;
    [SerializeField, Tooltip("Nietykalny w trakcie zeskoku.")] private bool invulnerableWhileDropping = true;

    [Header("Odwrót i krążenie")]
    [SerializeField] private float retreatDistance = 4f;
    [SerializeField] private float retreatTime = 1f;
    [SerializeField] private float circleTime = 1.5f;
    [SerializeField] private float circleRadius = 3f;
    [SerializeField] private float circleStep = 60f;
    [SerializeField] private float circleStepInterval = 0.5f;

    [Header("Skok-atak")]
    [SerializeField] private float leapMinDistance = 2.5f;
    [SerializeField] private float leapMaxDistance = 3.5f;
    [SerializeField] private float leapTelegraph = 0.4f;
    [SerializeField] private float leapConeAngle = 30f;
    [SerializeField] private float leapDuration = 0.25f;
    [SerializeField] private float leapArcHeight = 0.5f;

    public State Current { get; private set; } = State.Chasing;

    private NavMeshAgent agent;
    private Health health;
    private Rigidbody body;
    private EnemyMeleeAttack melee;
    private Transform player;
    private float nextPlayerSearch;
    private float nextRepath;
    private float dropTimer;
    private Vector3 dropStart, dropEnd;
    private EnemyAlert alert;
    private bool dropPending;
    private float dropPendingTimer;
    private float stateTimer;
    private float circleAngle;
    private float nextCircleStep;
    private float leapTimer;
    private bool leapWinding;
    private Vector3 leapStart, leapEnd;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody>();
        melee = GetComponent<EnemyMeleeAttack>();
        alert = GetComponent<EnemyAlert>();
    }

    private void OnEnable()
    {
        health.OnHealthChanged += Health_OnHealthChanged;
        if (alert != null)
            alert.OnAlerted += Alert_OnAlerted;
        if (melee != null)
            melee.OnAttackFinished += Melee_OnAttackFinished;
    }

    private void Health_OnHealthChanged(int delta)
    {
        if (delta < 0 && Current == State.Perched)
            RequestDrop();
    }

    private void Alert_OnAlerted()
    {
        if (Current == State.Perched)
            RequestDrop();
    }

    private void Melee_OnAttackFinished()
    {
        if (health.IsDead || Current != State.Chasing || !agent.enabled || !agent.isOnNavMesh)
            return;
        BeginRetreat();
    }

    private void RequestDrop()
    {
        if (dropPending || Current != State.Perched)
            return;
        if (!FindPlayer())
            return;
        dropPending = true;
        dropPendingTimer = dropTelegraph;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        Telegraph.Cone(transform.position, dir.normalized, dropConeAngle, FlatDistance(player.position), dropTelegraph);
        EnemyAlert.Raise(transform.position);
    }

    private void Start()
    {
        agent.speed = chaseSpeed;
        if (perchOnSpawn)
            Perch();
        else
            Current = State.Chasing;
    }

    private void Perch()
    {
        Current = State.Perched;
        if (agent.enabled)
            agent.enabled = false;
        if (body != null)
            body.isKinematic = true;
        if (melee != null)
            melee.enabled = false;
    }

    private void Update()
    {
        if (health.IsDead)
            return;

        switch (Current)
        {
            case State.Perched:
                if (dropPending)
                {
                    dropPendingTimer -= Time.deltaTime;
                    if (dropPendingTimer <= 0f)
                    {
                        dropPending = false;
                        BeginDrop();
                    }
                }
                else if (FindPlayer() && FlatDistance(player.position) <= Mathf.Min(dropTriggerRange, detectRange))
                    RequestDrop();
                break;
            case State.Dropping:
                UpdateDrop();
                break;
            case State.Chasing:
                UpdateChase();
                break;
            case State.Retreat:
                UpdateRetreat();
                break;
            case State.Circle:
                UpdateCircle();
                break;
            case State.Leap:
                UpdateLeap();
                break;
        }
    }

    private void BeginDrop()
    {
        dropStart = transform.position;
        dropEnd = ChooseLanding();
        dropTimer = 0f;
        Current = State.Dropping;
        if (invulnerableWhileDropping)
            health.IsInvulnerable = true;
    }

    private Vector3 ChooseLanding()
    {
        Vector3 playerPos = player.position;
        // Najbliżej gracza, ale najwyżej maxDropDistance od pająka w poziomie.
        Vector3 flat = playerPos - dropStart;
        flat.y = 0f;
        Vector3 target = playerPos;
        if (flat.magnitude > maxDropDistance)
            target = new Vector3(dropStart.x, playerPos.y, dropStart.z) + flat.normalized * maxDropDistance;

        if (NavMesh.SamplePosition(target, out var hit, 6f, NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(dropStart, out hit, 12f, NavMesh.AllAreas))
            return hit.position;
        return playerPos;
    }

    private void UpdateDrop()
    {
        dropTimer += Time.deltaTime;
        float t = Mathf.Clamp01(dropTimer / dropDuration);
        Vector3 pos = Vector3.Lerp(dropStart, dropEnd, t);
        pos.y += dropArcHeight * 4f * t * (1f - t);
        transform.position = pos;

        Vector3 flat = dropEnd - dropStart;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(flat);

        if (t >= 1f)
            Land();
    }

    private void Land()
    {
        health.IsInvulnerable = false;
        transform.position = dropEnd;
        agent.enabled = true;
        if (!agent.Warp(dropEnd))
            Debug.LogWarning("SpiderController: lądowanie poza NavMeshem.", this);
        agent.speed = chaseSpeed;
        if (melee != null)
            melee.enabled = true;
        Current = State.Chasing;
    }

    private void UpdateChase()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;
        if (!FindPlayer() || (melee != null && melee.IsAttacking))
            return;
        float dist = FlatDistance(player.position);
        if (melee != null && melee.enabled && melee.CanAttack && dist >= leapMinDistance && dist <= leapMaxDistance)
        {
            BeginLeap();
            return;
        }
        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + 0.2f;
        agent.SetDestination(player.position);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.IsInvulnerable = false;
            health.OnHealthChanged -= Health_OnHealthChanged;
        }
        if (alert != null)
            alert.OnAlerted -= Alert_OnAlerted;
        if (melee != null)
            melee.OnAttackFinished -= Melee_OnAttackFinished;
    }

    // ------------------------------------------------------------ odwrót i krążenie

    private Vector3 AwayFromPlayer()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        return away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
    }

    private void GoTo(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out var hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private void BeginRetreat()
    {
        if (!FindPlayer())
            return;
        Current = State.Retreat;
        stateTimer = retreatTime;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        GoTo(player.position + AwayFromPlayer() * retreatDistance);
    }

    private void UpdateRetreat()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
            return;
        Vector3 rel = transform.position - player.position;
        circleAngle = Mathf.Atan2(rel.z, rel.x) * Mathf.Rad2Deg;
        Current = State.Circle;
        stateTimer = circleTime;
        nextCircleStep = 0f;
    }

    private void UpdateCircle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Current = State.Chasing;
            nextRepath = 0f;
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh || Time.time < nextCircleStep)
            return;
        nextCircleStep = Time.time + circleStepInterval;
        circleAngle += circleStep;
        float rad = circleAngle * Mathf.Deg2Rad;
        GoTo(player.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * circleRadius);
    }

    // ------------------------------------------------------------ skok-atak

    private void BeginLeap()
    {
        Current = State.Leap;
        leapWinding = true;
        leapTimer = leapTelegraph;
        agent.ResetPath();
        agent.isStopped = true;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        Telegraph.Cone(transform.position, transform.forward, leapConeAngle, leapMaxDistance, leapTelegraph);
    }

    private void UpdateLeap()
    {
        if (leapWinding)
        {
            leapTimer -= Time.deltaTime;
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
            if (leapTimer > 0f)
                return;
            leapWinding = false;
            leapStart = transform.position;
            Vector3 flat = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            Vector3 aim = player.position - flat * 1f;
            leapEnd = NavMesh.SamplePosition(aim, out var hit, 2f, NavMesh.AllAreas) ? hit.position : leapStart;
            leapTimer = 0f;
            agent.enabled = false;
            return;
        }

        leapTimer += Time.deltaTime;
        float t = Mathf.Clamp01(leapTimer / leapDuration);
        Vector3 pos = Vector3.Lerp(leapStart, leapEnd, t);
        pos.y += leapArcHeight * 4f * t * (1f - t);
        transform.position = pos;
        if (t < 1f)
            return;

        transform.position = leapEnd;
        agent.enabled = true;
        agent.Warp(leapEnd);
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        Current = State.Chasing;
        nextRepath = 0f;
        if (melee != null && melee.enabled)
            melee.StartAttack();
    }

    private float FlatDistance(Vector3 p)
    {
        var d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private bool FindPlayer()
    {
        if (player != null)
            return true;
        if (Time.time < nextPlayerSearch)
            return false;
        nextPlayerSearch = Time.time + 0.5f;
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            player = go.transform;
        return player != null;
    }
}
