using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Slime bez grafu Behavior: Idle (wędrówka) -> Chase (otaczanie gracza w sektorach) -> Attack (cios robi
/// EnemyMeleeAttack.autoAttack) -> Retreat (odskok) -> Chase. Zgubienie gracza -> Idle.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class SlimeController : MonoBehaviour
{
    public enum State { Idle, Chase, Attack, Retreat }

    /// <summary>Zmiana stanu; argument to nazwa stanu (np. "Chase").</summary>
    public event System.Action<string> OnStateChanged;

    [Header("Idle")]
    [SerializeField] private float wanderRadius = 4f;
    [SerializeField] private Vector2 wanderInterval = new Vector2(2f, 4f);
    [SerializeField] private float idleSpeed = 2f;

    [Header("Pościg")]
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float loseRange = 14f;
    [SerializeField] private float surroundRadius = 1.8f;
    [SerializeField] private float groupRadius = 6f;
    [SerializeField] private float sectorRecalcInterval = 0.5f;
    [SerializeField, Tooltip("Gdy gracz bliżej niż tyle, a cios gotowy, slime rzuca się prosto na gracza (zamiast stać na okręgu).")]
    private float lungeDistance = 2.4f;

    [Header("Odskok")]
    [SerializeField] private float retreatDistance = 2.5f;
    [SerializeField, Tooltip("Gdy EnemyMeleeAttack.RetreatAfterAttack = 0, użyj tej wartości.")]
    private float defaultRetreatTime = 1.2f;

    public State Current { get; private set; } = State.Idle;

    // Rejestr slime'ów w pościgu (do przydziału sektorów).
    private static readonly List<SlimeController> Chasers = new List<SlimeController>();

    private NavMeshAgent agent;
    private Health health;
    private EnemyMeleeAttack melee;
    private EnemyAlert alert;
    private Transform player;
    private float chaseSpeed;
    private float nextPlayerSearch;
    private float nextWander;
    private float nextSectorCalc;
    private float nextRepath;
    private float retreatEnd;
    private float sectorAngle;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        melee = GetComponent<EnemyMeleeAttack>();
        alert = GetComponent<EnemyAlert>();
        chaseSpeed = agent.speed;
    }

    private void OnEnable()
    {
        if (melee != null)
        {
            melee.OnWindupStarted += Melee_OnWindupStarted;
            melee.OnAttackFinished += Melee_OnAttackFinished;
        }
        if (alert != null)
            alert.OnAlerted += Alert_OnAlerted;
    }

    private void OnDisable()
    {
        if (melee != null)
        {
            melee.OnWindupStarted -= Melee_OnWindupStarted;
            melee.OnAttackFinished -= Melee_OnAttackFinished;
        }
        if (alert != null)
            alert.OnAlerted -= Alert_OnAlerted;
        Chasers.Remove(this);
    }

    private void Start()
    {
        nextWander = Time.time + Random.Range(wanderInterval.x, wanderInterval.y);
        sectorAngle = Random.value * 360f;
        agent.speed = idleSpeed;
    }

    private void Alert_OnAlerted()
    {
        if (Current == State.Idle && !health.IsDead)
            EnterChase();
    }

    private void Melee_OnWindupStarted()
    {
        if (Current == State.Chase || Current == State.Idle)
            SetState(State.Attack);
    }

    private void Melee_OnAttackFinished()
    {
        if (Current != State.Attack)
            return;
        float t = melee.RetreatAfterAttack > 0f ? melee.RetreatAfterAttack : defaultRetreatTime;
        BeginRetreat(t);
    }

    private void SetState(State state)
    {
        if (Current == state)
            return;
        Current = state;
        if (state == State.Chase)
        {
            if (!Chasers.Contains(this)) Chasers.Add(this);
        }
        else
        {
            Chasers.Remove(this);
        }
        OnStateChanged?.Invoke(state.ToString());
    }

    private void EnterChase()
    {
        agent.speed = chaseSpeed;
        nextSectorCalc = 0f;
        nextRepath = 0f;
        SetState(State.Chase);
    }

    private void Update()
    {
        if (health.IsDead || !agent.enabled || !agent.isOnNavMesh)
            return;
        bool havePlayer = FindPlayer();
        float dist = havePlayer ? FlatDistance(player.position) : float.MaxValue;

        switch (Current)
        {
            case State.Idle:
                if (havePlayer && dist <= detectRange)
                {
                    EnemyAlert.Raise(transform.position);
                    EnterChase();
                    break;
                }
                UpdateWander();
                break;
            case State.Chase:
                if (!havePlayer || dist > loseRange)
                {
                    agent.speed = idleSpeed;
                    agent.ResetPath();
                    nextWander = Time.time + 1f;
                    SetState(State.Idle);
                    break;
                }
                UpdateChase(dist);
                break;
            case State.Attack:
                // Agentem steruje EnemyMeleeAttack (stop na czas ciosu).
                break;
            case State.Retreat:
                if (Time.time >= retreatEnd)
                    EnterChase();
                break;
        }
    }

    private void UpdateWander()
    {
        if (Time.time < nextWander)
            return;
        nextWander = Time.time + Random.Range(wanderInterval.x, wanderInterval.y);
        agent.speed = idleSpeed;
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        var point = transform.position + new Vector3(r.x, 0f, r.y);
        if (NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private void UpdateChase(float dist)
    {
        if (Time.time >= nextSectorCalc)
        {
            nextSectorCalc = Time.time + sectorRecalcInterval;
            AssignSector();
        }
        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + 0.2f;

        Vector3 dest;
        if (dist <= lungeDistance && melee != null && melee.CanAttack)
        {
            dest = player.position;
        }
        else
        {
            float rad = sectorAngle * Mathf.Deg2Rad;
            dest = player.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * surroundRadius;
        }
        if (NavMesh.SamplePosition(dest, out var hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(player.position);
    }

    /// <summary>Slime'y w groupRadius od siebie (łącznie z tym) dzielą okrąg na równe sektory wg kolejności w rejestrze.</summary>
    private void AssignSector()
    {
        int index = 0, count = 0;
        float sqr = groupRadius * groupRadius;
        foreach (var other in Chasers)
        {
            if (other != this && (other.transform.position - transform.position).sqrMagnitude > sqr)
                continue;
            if (other == this)
                index = count;
            count++;
        }
        if (count < 1) count = 1;
        sectorAngle = index * 360f / count;
    }

    private void BeginRetreat(float seconds)
    {
        SetState(State.Retreat);
        retreatEnd = Time.time + seconds;
        agent.speed = chaseSpeed;
        if (!FindPlayer() || !agent.enabled || !agent.isOnNavMesh)
            return;
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
        // Odskok: retreatDistance od gracza (nie od slime'a), z gracza w kierunku "od niego".
        Vector3 dest = player.position + away * retreatDistance;
        if (NavMesh.SamplePosition(dest, out var hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
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
