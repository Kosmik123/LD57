using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pająk: siedzi na ścianie (punkt spawnu poza NavMeshem), zeskakuje na gracza po paraboli, potem goni.
/// Atak robi EnemyMeleeAttack (autoAttack), włączany dopiero po lądowaniu.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class SpiderController : MonoBehaviour
{
    public enum State { Perched, Dropping, Chasing, Retreat, Circle, Leap, Climbing, WallWalk }

    [SerializeField] private bool perchOnSpawn = true;
    /// <summary>Publiczny setter dla spawnów specjalnych (jajko bossa: pająk od razu na podłodze).</summary>
    public bool PerchOnSpawn { get => perchOnSpawn; set => perchOnSpawn = value; }
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

    [Header("Ściany")]
    // Uwaga: pola mają NOWE nazwy (wallRun*, climbLeapDistance, climbWarpAfter), bo stare (wallWalkMin/Max, climbLeapRange,
    // climbTimeout) są zapisane w Spider.prefab ze starymi wartościami i nadpisywałyby domyślne.
    [SerializeField, Tooltip("Czas biegu po ścianie w stronę gracza przed zeskokiem (min).")] private float wallRunMin = 1.5f;
    [SerializeField] private float wallRunMax = 3f;
    [SerializeField, Tooltip("Mnożnik prędkości pająka biegnącego po ścianie.")] private float wallRunSpeedMultiplier = 1.15f;
    [SerializeField, Tooltip("Bieg po ścianie kończy się też, gdy gracz bliżej niż tyle.")] private float wallWalkStopDistance = 2.5f;
    [SerializeField, Tooltip("Po wyjściu z klifu: skok od razu, jeśli gracz bliżej niż tyle.")] private float climbLeapDistance = 7f;
    [SerializeField, Tooltip("Awaryjnie: po tylu sekundach wspinaczki pająk jest przenoszony na półkę.")] private float climbWarpAfter = 4f;
    [SerializeField, Tooltip("Pająk z perchOnSpawn przyczepia się do ściany, jeśli jest ona bliżej niż tyle (m).")] private float wallSnapRange = 1.1f;
    [SerializeField, Tooltip("Zeskok ze ściany celuje w pozycję gracza za tyle sekund.")] private float dropLead = 0.25f;
    [SerializeField, Tooltip("Prędkość skoku-ataku z dystansu większego niż leapMaxDistance (m/s).")] private float longLeapSpeed = 16f;

    public State Current { get; private set; } = State.Chasing;
    /// <summary>Pająk przyklejony do ściany/klifu (Climbing, WallWalk albo Perched na ścianie): transform.up ≈ SurfaceNormal.</summary>
    public bool OnSurface => Current == State.Climbing || Current == State.WallWalk || (Current == State.Perched && perchedOnSurface);
    public Vector3 SurfaceNormal
    {
        get
        {
            if ((Current == State.Climbing || Current == State.WallWalk) && crawler != null)
                return crawler.SurfaceNormal;
            return Current == State.Perched && perchedOnSurface ? perchNormal : Vector3.up;
        }
    }
    /// <summary>Pierwsze wykrycie gracza (błysk oczu w SpiderAnimator).</summary>
    public event System.Action OnSpotted;

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
    private float leapTotal;
    private WallCrawler crawler;
    private bool wallWalkAllowed, wallWalkDone, useLead;
    private float walkTimer;
    private Vector3 climbTarget;
    private Vector3 playerLastPos, playerVelocity;
    private bool initialStateSet, perchedOnSurface, spotted;
    private Vector3 perchNormal = Vector3.up;
    private float walkHeight;

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
        if (crawler != null)
        {
            crawler.OnReachedFloor -= Crawler_OnReachedFloor;
            crawler.OnReachedFloor += Crawler_OnReachedFloor;
        }
    }

    private void Health_OnHealthChanged(int delta)
    {
        if (delta < 0 && Current == State.Perched)
            RequestDrop();
        else if (delta < 0 && Current == State.WallWalk)
            EndWallWalk();
    }

    private void Alert_OnAlerted()
    {
        if (Current != State.Perched)
            return;
        NotifySpotted();
        if (TryBeginWallWalk())
            return;
        RequestDrop();
    }

    private void NotifySpotted()
    {
        if (spotted)
            return;
        spotted = true;
        OnSpotted?.Invoke();
    }

    // ------------------------------------------------------------ ściany

    /// <summary>LevelPopulation: pająk na pionowej ścianie najpierw biegnie po niej do gracza, dopiero potem skacze.</summary>
    public void EnableWallWalk() => wallWalkAllowed = true;

    private WallCrawler EnsureCrawler()
    {
        if (crawler == null)
        {
            crawler = GetComponent<WallCrawler>();
            if (crawler == null)
                crawler = gameObject.AddComponent<WallCrawler>();
            crawler.OnReachedFloor -= Crawler_OnReachedFloor;
            crawler.OnReachedFloor += Crawler_OnReachedFloor;
        }
        return crawler;
    }

    /// <summary>
    /// Wspinaczka z poziomu niżej: wołać zaraz po Instantiate, pająk stoi już na klifie pod krawędzią półki.
    /// ledgePoint = punkt na półce (NavMesh) za krawędzią. Zwraca false, gdy brak powierzchni (wtedy pająk trafia od razu na półkę).
    /// </summary>
    public bool BeginClimb(Vector3 ledgePoint)
    {
        climbTarget = ledgePoint;
        if (agent == null)
            Awake();
        initialStateSet = true;
        if (agent.enabled)
            agent.enabled = false;
        if (body != null)
            body.isKinematic = true;
        if (melee != null)
            melee.enabled = false;
        Current = State.Climbing;
        stateTimer = climbWarpAfter;
        SpiderSpawnHelper.ClimbsStarted++;
        Debug.Log($"[Spider] wspinaczka start y={transform.position.y:F1} -> półka y={ledgePoint.y:F1}");
        var c = EnsureCrawler();
        c.SpeedMultiplier = 1f;
        if (!c.Begin(ledgePoint, true))
        {
            SpiderSpawnHelper.SpawnFallbacks++;
            Debug.Log("[Spider] fallback: brak powierzchni przy starcie wspinaczki, pająk od razu na półce");
            FinishClimb(ledgePoint);
            return false;
        }
        return true;
    }

    private void Crawler_OnReachedFloor()
    {
        if (Current != State.Climbing)
            return;
        SpiderSpawnHelper.ClimbsReachedFloor++;
        Debug.Log("[Spider] wspinaczka: pająk na półce");
        FinishClimb(crawler.FloorPoint);
    }

    private void FinishClimb(Vector3 where)
    {
        if (Current != State.Climbing)
            return;
        if (crawler != null)
            crawler.Stop();
        Vector3 pos = NavMesh.SamplePosition(where, out var hit, 3f, NavMesh.AllAreas) ? hit.position : where;
        Vector3 fwd = Vector3.ProjectOnPlane(climbTarget - pos, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        agent.enabled = true;
        agent.Warp(pos);
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        if (melee != null)
            melee.enabled = true;
        Current = State.Chasing;
        nextRepath = 0f;
        NotifySpotted();
        if (melee != null && FindPlayer() && FlatDistance(player.position) <= climbLeapDistance
            && Mathf.Abs(player.position.y - pos.y) < 3f && agent.isOnNavMesh)
            BeginLeap();
    }

    private bool TryBeginWallWalk()
    {
        if (!wallWalkAllowed || wallWalkDone || Current != State.Perched || !FindPlayer())
            return false;
        wallWalkDone = true;
        var c = EnsureCrawler();
        walkHeight = transform.position.y;
        if (!c.Begin(WalkTarget(), false))
            return false;
        c.SpeedMultiplier = wallRunSpeedMultiplier;
        Current = State.WallWalk;
        walkTimer = Random.Range(wallRunMin, wallRunMax);
        SpiderSpawnHelper.WallWalks++;
        Debug.Log($"[Spider] bieg po ścianie {walkTimer:F1} s, normalna {crawler.SurfaceNormal}");
        return true;
    }

    private void UpdateWallWalk()
    {
        walkTimer -= Time.deltaTime;
        crawler.SetTarget(WalkTarget());
        if (walkTimer <= 0f || FlatDistance(player.position) <= wallWalkStopDistance || crawler.LostTime > 0.5f || !crawler.Active)
            EndWallWalk();
    }

    /// <summary>Gracz zrzutowany na ścianę: ta sama wysokość co na starcie biegu, żeby pająk biegł bokiem, a nie zjeżdżał do podłogi.</summary>
    private Vector3 WalkTarget() => new Vector3(player.position.x, walkHeight, player.position.z);

    private void EndWallWalk()
    {
        if (Current != State.WallWalk)
            return;
        crawler.Stop();
        crawler.SpeedMultiplier = 1f;
        perchedOnSurface = true;
        perchNormal = crawler.SurfaceNormal;
        Current = State.Perched;
        useLead = true;
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
        if (initialStateSet)
            return; // BeginClimb / PerchNow ustawiły stan przed Start
        initialStateSet = true;
        if (perchOnSpawn)
            Perch();
        else
            Current = State.Chasing;
    }

    /// <summary>Test / spawn na ścianie: usiądź teraz (bez czekania na Start), przyczepiając się do ściany w pobliżu.</summary>
    public void PerchNow()
    {
        if (agent == null)
            Awake();
        if (Current == State.Climbing || (initialStateSet && Current == State.Perched))
            return;
        initialStateSet = true;
        Perch();
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
        perchedOnSurface = TryAttachToWall();
    }

    /// <summary>
    /// Siedzący pająk ma wisieć NA ścianie (transform.up = normalna), nie w powietrzu nogami w dół. Szuka pionowej ściany
    /// w wallSnapRange (12 kierunków); jeśli stoi na podłodze, unosi się na 2-3 m, o ile ściana tam sięga.
    /// </summary>
    private bool TryAttachToWall()
    {
        // na podłodze (NavMesh tuż pod pająkiem) promienie z 1 m wysokości, inaczej z miejsca pająka (spawn już na ścianie, nie podnosić go drugi raz)
        bool onFloor = NavMesh.SamplePosition(transform.position, out var floor, 1.2f, NavMesh.AllAreas);
        Vector3 origin = onFloor ? transform.position + Vector3.up * 1f : transform.position + transform.up * 0.3f;
        RaycastHit best = default;
        Vector3 bestDir = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (SpiderSpawnHelper.Raycast(origin, dir, wallSnapRange, transform, out var hit) && Mathf.Abs(hit.normal.y) < 0.3f && hit.distance < bestDist)
            {
                best = hit;
                bestDir = dir;
                bestDist = hit.distance;
            }
        }
        if (bestDist == float.MaxValue)
        {
            // brak ściany: pająk siedzi w powietrzu, ale pionowo (nie bokiem po rotacji ze spawnu na ścianie)
            transform.rotation = UprightRotation(transform.forward);
            return false;
        }

        Vector3 point = best.point, normal = best.normal;
        if (onFloor)
        {
            var high = new Vector3(origin.x, floor.position.y + Random.Range(2f, 3f), origin.z);
            if (SpiderSpawnHelper.Raycast(high, bestDir, wallSnapRange + 1f, transform, out var h2)
                && Mathf.Abs(h2.normal.y) < 0.3f && Vector3.Dot(h2.normal, normal) > 0.7f)
            {
                point = h2.point;
                normal = h2.normal;
            }
        }
        Vector3 toPlayer = FindPlayer() ? player.position - point : Vector3.down;
        transform.SetPositionAndRotation(point + normal * 0.1f, SpiderSpawnHelper.SurfaceRotation(normal, toPlayer));
        perchNormal = normal;
        SpiderSpawnHelper.WallPerches++;
        Debug.Log($"[Spider] siedzi na ścianie y={transform.position.y:F1} normalna {normal}");
        return true;
    }

    private void Update()
    {
        if (health.IsDead)
        {
            if (crawler != null && crawler.Active)
                crawler.Stop();
            return;
        }
        TrackPlayerVelocity();

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
                else if (FindPlayer())
                {
                    float d = FlatDistance(player.position);
                    if (d <= detectRange)
                        NotifySpotted();
                    if (wallWalkAllowed && !wallWalkDone && d <= detectRange)
                    {
                        if (!TryBeginWallWalk() && d <= Mathf.Min(dropTriggerRange, detectRange))
                            RequestDrop();
                    }
                    else if (d <= Mathf.Min(dropTriggerRange, detectRange))
                        RequestDrop();
                }
                break;
            case State.WallWalk:
                UpdateWallWalk();
                break;
            case State.Climbing:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    SpiderSpawnHelper.ClimbWarps++;
                    Debug.Log("[Spider] wspinaczka: awaryjny warp na półkę");
                    FinishClimb(climbTarget);
                }
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
        perchedOnSurface = false;
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
        if (useLead)
            playerPos += new Vector3(playerVelocity.x, 0f, playerVelocity.z) * dropLead;
        useLead = false;
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

        // ze ściany pająk obraca się do pionu płynnie (a nie skokiem)
        Quaternion want = UprightRotation(dropEnd - dropStart);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-14f * Time.deltaTime));

        if (t >= 1f)
            Land();
    }

    private Quaternion UprightRotation(Vector3 heading)
    {
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        return Quaternion.LookRotation(heading.normalized, Vector3.up);
    }

    private void Land()
    {
        health.IsInvulnerable = false;
        transform.SetPositionAndRotation(dropEnd, UprightRotation(dropEnd - dropStart));
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
        if (crawler != null)
        {
            crawler.OnReachedFloor -= Crawler_OnReachedFloor;
            crawler.Stop();
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
        SpiderSpawnHelper.Leaps++;
        Debug.Log("[Spider] skok-atak");
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        Telegraph.Cone(transform.position, transform.forward, leapConeAngle, Mathf.Max(leapMaxDistance, FlatDistance(player.position)), leapTelegraph);
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
            leapTotal = Mathf.Max(leapDuration, Vector3.Distance(leapStart, leapEnd) / Mathf.Max(1f, longLeapSpeed));
            agent.enabled = false;
            return;
        }

        leapTimer += Time.deltaTime;
        float t = Mathf.Clamp01(leapTimer / leapTotal);
        Vector3 pos = Vector3.Lerp(leapStart, leapEnd, t);
        pos.y += leapArcHeight * Mathf.Max(1f, Vector3.Distance(leapStart, leapEnd) / leapMaxDistance) * 4f * t * (1f - t);
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

    private void TrackPlayerVelocity()
    {
        if (player == null)
            return;
        float dt = Time.deltaTime;
        if (dt > 0f && playerLastPos != Vector3.zero)
            playerVelocity = Vector3.Lerp(playerVelocity, (player.position - playerLastPos) / dt, 0.3f);
        playerLastPos = player.position;
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
