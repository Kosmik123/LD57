using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss: matka pająków. Własna maszyna stanów (bez grafu Behavior):
/// OnCeiling -> Drop -> Charge -> Summon -> ReturnToCeiling -> OnCeiling.
/// Śmierć obsługuje EnemyDeath, wygraną GameManager (EnemyHealth.IsBoss).
/// </summary>
[RequireComponent(typeof(Health))]
public class SpiderMotherBoss : MonoBehaviour
{
    public enum State { OnCeiling, Drop, Charge, Summon, ReturnToCeiling }

    /// <summary>Zmiana stanu; argument to nazwa stanu (np. "Drop").</summary>
    public event System.Action<string> OnStateChanged;
    /// <summary>Początek zapowiedzi zeskoku (VFX/dźwięk).</summary>
    /// <summary>Zapowiedź zeskoku: punkt lądowania (dla pierścienia na ziemi).</summary>
    public event System.Action<Vector3> OnTelegraph;
    /// <summary>Zmiana fazy (2 przy HP < 60 %, 3 przy HP < 30 %).</summary>
    public event System.Action<int> OnPhaseChanged;

    [Header("Sufit")]
    [SerializeField] private float ceilingHeight = 6f;
    [SerializeField] private float ceilingTime = 2f;
    [SerializeField] private float returnTime = 1f;

    [Header("Zeskok")]
    [SerializeField] private float telegraphTime = 1f;
    [SerializeField, Tooltip("Boss spada na pozycję gracza sprzed tylu sekund.")]
    private float targetLag = 0.5f;
    [SerializeField] private float fallTime = 0.25f;
    [SerializeField] private int landingDamage = 15;
    [SerializeField] private float landingRadius = 2.5f;
    [SerializeField, Tooltip("Odrzut gracza w metrach (konwencja jak w DamagingBlade: VelocityChange).")]
    private float landingKnockback = 3f;

    [Header("Szarża")]
    [SerializeField] private float chargeTime = 3f;
    [SerializeField] private float chargeSpeed = 4f;
    [SerializeField] private float chargeTelegraph = 0.5f;
    [SerializeField] private float chargeConeAngle = 25f;
    [SerializeField] private float chargeConeRange = 8f;

    [Header("Fazy")]
    [SerializeField, Range(0f, 1f)] private float phase2Threshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.3f;
    [SerializeField] private float phase2CeilingMultiplier = 0.6f;
    [SerializeField] private float phase3CeilingMultiplier = 0.4f;
    [SerializeField] private float phase2ChargeSpeedMultiplier = 1.3f;
    [SerializeField] private int phase3MinionCount = 5;
    [SerializeField] private float roarPause = 1f;
    [SerializeField, Tooltip("Faza 3: tyle dodatkowych par Szarża + Przyzwanie przed powrotem na sufit.")]
    private int phase3ExtraRounds = 2;

    [Header("Przyzwanie")]
    [SerializeField, Tooltip("Mały pająk. Null = przyzwanie nic nie robi.")]
    private GameObject minionPrefab;
    [SerializeField] private int minionCount = 3;
    [SerializeField] private float summonRadius = 3f;
    [SerializeField] private float summonDuration = 1f;

    public State Current { get; private set; }
    /// <summary>Faza walki: 1, 2 (HP &lt; 60 %), 3 (HP &lt; 30 %).</summary>
    public int Phase { get; private set; } = 1;

    private bool roarPending;

    private Health health;
    private NavMeshAgent agent;
    private Rigidbody body;
    private EnemyMeleeAttack melee;
    private Collider[] bodyColliders;
    private Transform player;
    private Vector3 home;

    private struct Sample { public float time; public Vector3 position; }
    private readonly Queue<Sample> history = new Queue<Sample>();
    private Sample lagged;
    private bool hasLagged;

    private void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        melee = GetComponent<EnemyMeleeAttack>();
    }

    private void OnEnable()
    {
        health.OnDied += Health_OnDied;
        health.OnHealthChanged += Health_OnHealthChanged;
        SetCameraZoom(0.9f); // walka z bossem: kamera lekko bliżej (A6)
    }

    private void OnDisable()
    {
        health.OnDied -= Health_OnDied;
        health.OnHealthChanged -= Health_OnHealthChanged;
        SetCameraZoom(1f);
    }

    private void Health_OnHealthChanged(int delta)
    {
        if (delta >= 0 || health.IsDead || health.MaxHealth <= 0)
            return;
        float ratio = (float)health.CurrentHealth / health.MaxHealth;
        int target = ratio < phase3Threshold ? 3 : ratio < phase2Threshold ? 2 : 1;
        while (Phase < target)
        {
            Phase++;
            roarPending = true;
            OnPhaseChanged?.Invoke(Phase);
            OnStateChanged?.Invoke("Phase" + Phase);
        }
    }

    private float CeilingTimeNow => ceilingTime * (Phase >= 3 ? phase3CeilingMultiplier : Phase == 2 ? phase2CeilingMultiplier : 1f);

    private IEnumerator RoarIfPending()
    {
        if (!roarPending)
            yield break;
        roarPending = false;
        Sfx.Play(SfxKind.BossTelegraph, transform.position);
        yield return new WaitForSeconds(roarPause);
    }

    // Start, nie OnEnable: WaveZone ustawia pozycję i robi Warp zaraz po Instantiate.
    private static void SetCameraZoom(float mult)
    {
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null) cam.SetZoom(mult);
    }

    private void Start()
    {
        home = transform.position;
        // Collidery ciała (nie triggery): wyłączane na sufitze. Hitbox ataku jest triggerem i zostaje.
        var list = new List<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger)
                list.Add(c);
        bodyColliders = list.ToArray();
        StartCoroutine(Cycle());
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
            return;
        }

        // Historia pozycji gracza: lagged to najświeższa próbka starsza niż targetLag.
        history.Enqueue(new Sample { time = Time.time, position = player.position });
        while (history.Count > 1 && Time.time - history.Peek().time > targetLag)
        {
            lagged = history.Dequeue();
            hasLagged = true;
        }
    }

    private void Health_OnDied()
    {
        StopAllCoroutines();
        if (melee != null)
            melee.enabled = false;
    }

    private void SetState(State state)
    {
        Current = state;
        OnStateChanged?.Invoke(state.ToString());
    }

    private Vector3 CeilingPosition => home + Vector3.up * ceilingHeight;

    // ------------------------------------------------------------------ cykl

    private IEnumerator Cycle()
    {
        // Start na sufitze.
        EnterCeiling(true);
        while (!health.IsDead)
        {
            SetState(State.OnCeiling);
            yield return RoarIfPending();
            yield return new WaitForSeconds(CeilingTimeNow);

            SetState(State.Drop);
            yield return DropRoutine();

            if (Phase >= 2)
            {
                // Dwa zeskoki z rzędu: szybki powrót pod sufit i drugi zeskok.
                yield return RoarIfPending();
                SetState(State.ReturnToCeiling);
                yield return ReturnRoutine(returnTime * 0.4f);
                SetState(State.Drop);
                yield return DropRoutine();
            }

            yield return RoarIfPending();
            SetState(State.Charge);
            yield return ChargeRoutine();

            SetState(State.Summon);
            yield return SummonRoutine();

            if (Phase >= 3)
            {
                for (int r = 0; r < phase3ExtraRounds; r++)
                {
                    SetState(State.Charge);
                    yield return ChargeRoutine();
                    SetState(State.Summon);
                    yield return SummonRoutine();
                }
            }

            SetState(State.ReturnToCeiling);
            yield return ReturnRoutine();
        }
    }

    private void EnterCeiling(bool snap)
    {
        if (agent != null) agent.enabled = false;
        if (body != null) body.isKinematic = true;
        SetBodyColliders(false);
        if (melee != null) melee.enabled = false;
        if (snap) transform.position = CeilingPosition;
    }

    private void SetBodyColliders(bool enabled)
    {
        foreach (var c in bodyColliders)
            if (c != null) c.enabled = enabled;
    }

    private IEnumerator DropRoutine()
    {
        // Cel: pozycja gracza sprzed targetLag sekund, zapowiedź na tym miejscu.
        Vector3 target = home;
        if (player != null)
            target = hasLagged ? lagged.position : player.position;
        target = GroundPoint(target);

        transform.position = new Vector3(target.x, CeilingPosition.y, target.z);
        OnTelegraph?.Invoke(target);
        Telegraph.Circle(target, 2.5f, telegraphTime);
        Sfx.Play(SfxKind.BossTelegraph, transform.position);
        yield return new WaitForSeconds(telegraphTime);

        Vector3 start = transform.position;
        for (float t = 0; t < fallTime; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, target, (t * t) / (fallTime * fallTime));
            yield return null;
        }
        transform.position = target;

        SetBodyColliders(true);
        EnableAgentAt(target);
        LandingDamage(target);
    }

    private Vector3 GroundPoint(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas))
            return hit.position;
        if (Physics.Raycast(p + Vector3.up * ceilingHeight, Vector3.down, out var rayHit, ceilingHeight * 3f, ~0, QueryTriggerInteraction.Ignore))
            return rayHit.point;
        return new Vector3(p.x, home.y, p.z);
    }

    /// <summary>Zasada jak w WaveZone: agent wyłączony, pozycja, agent włączony, Warp.</summary>
    private void EnableAgentAt(Vector3 position)
    {
        if (agent == null) return;
        agent.enabled = false;
        transform.position = position;
        agent.enabled = true;
        agent.Warp(position);
    }

    private void LandingDamage(Vector3 center)
    {
        var hits = Physics.OverlapSphere(center, landingRadius, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<Health>();
        foreach (var col in hits)
        {
            if (!ColliderTags.IsPlayer(col)) continue;
            var rb = col.attachedRigidbody;
            var targetHealth = rb != null ? rb.GetComponent<Health>() : col.GetComponentInParent<Health>();
            if (targetHealth == null || !done.Add(targetHealth)) continue;

            targetHealth.ChangeHealth(-landingDamage);
            if (rb != null && !rb.isKinematic && landingKnockback > 0f)
            {
                var dir = rb.position - center;
                dir.y = 0;
                dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
                rb.AddForce(dir * landingKnockback, ForceMode.VelocityChange);
            }
        }
    }

    private IEnumerator ChargeRoutine()
    {
        if (melee != null) melee.enabled = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        if (player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            Telegraph.Cone(transform.position, dir.normalized, chargeConeAngle, chargeConeRange, chargeTelegraph);
        }
        yield return new WaitForSeconds(chargeTelegraph);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = Phase >= 2 ? chargeSpeed * phase2ChargeSpeedMultiplier : chargeSpeed;
            agent.isStopped = false;
        }

        float nextRepath = 0f;
        for (float t = 0; t < chargeTime; t += Time.deltaTime)
        {
            if (player != null && agent != null && agent.enabled && agent.isOnNavMesh && !(melee != null && melee.IsAttacking) && Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.2f;
                agent.SetDestination(player.position);
            }
            yield return null;
        }

        // Dokończ rozpoczęty atak (windup + aktywny hitbox), nie ucinaj zapowiedzi.
        while (melee != null && melee.IsAttacking)
            yield return null;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
        if (melee != null) melee.enabled = false;
    }

    private IEnumerator SummonRoutine()
    {
        if (minionPrefab != null)
        {
            int count = Phase >= 3 ? phase3MinionCount : minionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / Mathf.Max(1, count);
                var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * summonRadius;
                SpawnMinion(transform.position + offset);
            }
        }
        yield return new WaitForSeconds(summonDuration);
    }

    private void SpawnMinion(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
            position = hit.position;

        // Bez rodzica; nie rejestrowane w WaveZone, więc liczą się do XP (EnemyHealth), ale nie do kraty.
        var instance = Instantiate(minionPrefab, position, Quaternion.identity);
        if (instance.TryGetComponent<NavMeshAgent>(out var minionAgent))
        {
            minionAgent.enabled = false;
            instance.transform.position = position;
            minionAgent.enabled = true;
            minionAgent.Warp(position);
        }
    }

    private IEnumerator ReturnRoutine() => ReturnRoutine(returnTime);

    private IEnumerator ReturnRoutine(float duration)
    {
        EnterCeiling(false);
        Vector3 start = transform.position;
        Vector3 end = CeilingPosition;
        duration = Mathf.Max(0.01f, duration);
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, t / duration);
            yield return null;
        }
        transform.position = end;
    }
}
