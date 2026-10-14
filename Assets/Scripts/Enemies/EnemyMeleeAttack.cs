using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Atak wręcz wroga z zapowiedzią (windup). Trafienie robi DamagingBlade na obiekcie damagingObject.</summary>
public class EnemyMeleeAttack : MonoBehaviour
{
    /// <summary>Początek zapowiedzi (animacja/VFX).</summary>
    public event System.Action OnWindupStarted;
    /// <summary>Atak zakończony (przed cooldownem).</summary>
    public event System.Action OnAttackFinished;

    [Header("Skok (slime)")]
    [SerializeField, Tooltip("Zamiast ciosu: rozpłaszczenie w windupie (telegraf w miejscu lądowania), skok po łuku i zgniecenie.")]
    private bool leap;
    [SerializeField] private float leapSeconds = 0.35f;
    [SerializeField, Tooltip("Promień zgniecenia po lądowaniu (= promień telegrafu).")] private float landRadius = 1.3f;
    [SerializeField] private int leapDamage = 10;
    [SerializeField, Tooltip("Wyprzedzenie celu: prędkość gracza × sekundy.")] private float leapLead = 0.25f;

    [Header("Cios")]
    [SerializeField] private float windup = 0.6f;
    [SerializeField] private float activeTime = 0.2f;
    [SerializeField] private float cooldown = 1.2f;
    [SerializeField] private float range = 1.5f;
    [SerializeField, Tooltip("Dziecko z DamagingBlade (tag Player), domyślnie wyłączone.")]
    private GameObject damagingObject;
    [SerializeField, Tooltip("Atakuj sam, gdy gracz jest w zasięgu (bez węzła w grafie Behavior). Wyłącz, jeśli graf steruje atakiem.")]
    private bool autoAttack = true;

    [SerializeField, Tooltip("Odskok po ciosie w sekundach (0 = brak). Czyta kontroler (SlimeController).")]
    private float retreatAfterAttack = 0f;

    private Transform target;
    private float nextTargetSearch;

    public float RetreatAfterAttack => retreatAfterAttack;

    public float Range => range;
    public bool IsAttacking { get; private set; }
    private bool onCooldown;
    public bool CanAttack => !IsAttacking && !onCooldown;
    public bool IsLeap => leap;
    /// <summary>0..1 w trakcie lotu, -1 poza nim (SlimeAnimator rysuje łuk).</summary>
    public float LeapProgress { get; private set; } = -1f;
    public Vector3 LeapTarget { get; private set; }
    public event System.Action OnLeapLanded;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnDisable()
    {
        // Przerwany atak (np. śmierć) nie zostawia aktywnego hitboxa.
        StopAllCoroutines();
        IsAttacking = false;
        onCooldown = false;
        if (damagingObject != null)
            damagingObject.SetActive(false);
    }

    public void StartAttack()
    {
        if (!CanAttack || !isActiveAndEnabled)
            return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        if (leap)
        {
            yield return LeapRoutine();
            yield break;
        }
        IsAttacking = true;
        SetStopped(true);
        OnWindupStarted?.Invoke();
        Telegraph.Circle(transform.position + transform.forward * (range * 0.5f), range * 0.6f, windup);
        Sfx.Play(SfxKind.Windup, transform.position);

        yield return new WaitForSeconds(windup);

        if (damagingObject != null)
            damagingObject.SetActive(true);
        yield return new WaitForSeconds(activeTime);
        if (damagingObject != null)
            damagingObject.SetActive(false);

        SetStopped(false);
        IsAttacking = false;
        onCooldown = true;
        OnAttackFinished?.Invoke();

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private IEnumerator LeapRoutine()
    {
        IsAttacking = true;
        SetStopped(true);
        LeapTarget = PickLeapTarget();
        var face = LeapTarget - transform.position; face.y = 0f;
        if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(face);
        OnWindupStarted?.Invoke();
        Telegraph.Circle(LeapTarget, landRadius, windup);
        Sfx.Play(SfxKind.Windup, transform.position);

        yield return new WaitForSeconds(windup);

        // lot: pozycja po prostej (łuk rysuje animator przez LeapProgress), agent zostaje na NavMeshu
        var start = transform.position;
        float t = 0f;
        while (t < leapSeconds)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / leapSeconds);
            LeapProgress = p;
            var desired = Vector3.Lerp(start, LeapTarget, p);
            var delta = desired - transform.position;
            delta.y = 0f;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.Move(delta);
            }
            else
            {
                transform.position += delta;
            }
            yield return null;
        }
        LeapProgress = -1f;
        Land();

        SetStopped(false);
        IsAttacking = false;
        onCooldown = true;
        OnAttackFinished?.Invoke();

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private Vector3 PickLeapTarget()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
        if (target == null) return transform.position + transform.forward * 2f;
        var pos = target.position;
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            var v = rb.linearVelocity; v.y = 0f;
            pos += v * leapLead;
        }
        var from = transform.position;
        var flat = pos - from; flat.y = 0f;
        float max = range + 1f;
        if (flat.magnitude > max) pos = from + flat.normalized * max;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            pos = hit.position;
        return pos;
    }

    private void Land()
    {
        Sfx.Play(SfxKind.EnemyDie, transform.position, 0.5f); // zastępczy plask lądowania
        OnLeapLanded?.Invoke();
        bool hitPlayer = false;
        foreach (var col in Physics.OverlapSphere(transform.position, landRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hitPlayer || !ColliderTags.IsPlayer(col)) continue;
            var health = col.GetComponentInParent<Health>();
            if (health == null && col.attachedRigidbody != null) health = col.attachedRigidbody.GetComponent<Health>();
            if (health == null) continue;
            health.ChangeHealth(-leapDamage);
            hitPlayer = true;
        }
    }

    private void Update()
    {
        if (!autoAttack || !CanAttack)
            return;
        if (target == null)
        {
            if (Time.time < nextTargetSearch) return;
            nextTargetSearch = Time.time + 0.5f;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            target = player.transform;
        }
        var toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > range * range)
            return;
        if (toTarget.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toTarget);
        StartAttack();
    }

    private void SetStopped(bool stopped)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = stopped;
    }
}
