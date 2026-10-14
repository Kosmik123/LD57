using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Strzelec (kiting): strzela tylko z linią wzroku, w cooldownie krąży bokiem (strafe) 7-9 m od gracza,
/// gdy zasłonięty szuka punktu z widokiem. Zapowiedź strzału to linia telegrafu.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class ShooterController : MonoBehaviour
{
    /// <summary>Początek zapowiedzi strzału (animacja/VFX).</summary>
    public event System.Action OnWindupStarted;
    /// <summary>Pocisk wystrzelony.</summary>
    public event System.Action OnShot;

    [SerializeField, Tooltip("Środek pasa utrzymywanego dystansu (7-9 m przy keepBand = 1).")] private float keepDistance = 8f;
    [SerializeField] private float keepBand = 1f;
    [SerializeField] private float fleeDistance = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField, Tooltip("Gracz dalej niż tyle: strzelec bezczynny (chyba że alarm).")] private float detectRange = 25f;
    [SerializeField] private float shootRange = 12f;
    [SerializeField] private float cooldown = 2f;
    [SerializeField] private float windup = 0.8f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private int damage = 6;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float muzzleHeight = 0.8f;
    [SerializeField] private int burstCount = 1;
    [SerializeField] private float burstInterval = 0.15f;
    [SerializeField] private float strafeSwitchTime = 1.5f;
    [SerializeField] private float strafeStep = 3f;
    [SerializeField] private float coverSearchRadius = 6f;

    private NavMeshAgent agent;
    private Health health;
    private EnemyAlert alert;
    private Transform player;
    private float nextPlayerSearch;
    private float nextShotTime;
    private float nextRepath;
    private float nextSideSwitch;
    private float nextCoverSearch;
    private int strafeSide = 1;
    private bool shooting;
    private bool aware;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        alert = GetComponent<EnemyAlert>();
    }

    private void OnEnable()
    {
        if (alert != null)
            alert.OnAlerted += Alert_OnAlerted;
    }

    private void Start()
    {
        agent.speed = moveSpeed;
        nextShotTime = Time.time + cooldown;
        strafeSide = Random.value < 0.5f ? -1 : 1;
    }

    private void OnDisable()
    {
        if (alert != null)
            alert.OnAlerted -= Alert_OnAlerted;
        StopAllCoroutines();
        shooting = false;
    }

    private void Alert_OnAlerted()
    {
        aware = true;
    }

    private void Update()
    {
        if (health.IsDead || !agent.enabled || !agent.isOnNavMesh)
            return;
        if (!FindPlayer())
            return;

        var to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (!aware)
        {
            if (dist > detectRange)
                return;
            aware = true;
            EnemyAlert.Raise(transform.position);
        }

        if (shooting)
        {
            Face(to);
            return;
        }

        bool ready = Time.time >= nextShotTime;
        bool los = HasLineOfSight(transform.position, player.position);

        if (ready && dist <= shootRange && los)
        {
            StartCoroutine(ShootRoutine());
            return;
        }

        if (Time.time < nextRepath)
            return;
        nextRepath = Time.time + 0.2f;

        if (ready && !los)
        {
            SeekLineOfSight();
            return;
        }

        Strafe(to, dist);

        if (dist > 0.01f && agent.velocity.sqrMagnitude < 0.01f)
            Face(to);
    }

    /// <summary>Ruch prostopadły do kierunku na gracza z korektą dystansu; strona zmienia się co strafeSwitchTime.</summary>
    private void Strafe(Vector3 to, float dist)
    {
        if (Time.time >= nextSideSwitch)
        {
            nextSideSwitch = Time.time + strafeSwitchTime;
            strafeSide = -strafeSide;
        }
        Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
        Vector3 away = -dir;
        Vector3 side = Vector3.Cross(Vector3.up, dir) * strafeSide;

        if (dist < fleeDistance)
        {
            MoveTo(transform.position + away * 4f);
            return;
        }

        Vector3 move = side * strafeStep;
        if (dist > keepDistance + keepBand)
            move += dir * strafeStep;
        else if (dist < keepDistance - keepBand)
            move += away * strafeStep;

        Vector3 dest = transform.position + move;
        if (!TrySetDestination(dest))
        {
            // Ściana po tej stronie: zmień stronę od razu.
            strafeSide = -strafeSide;
            nextSideSwitch = Time.time + strafeSwitchTime;
            TrySetDestination(transform.position + side * -strafeStep + (dist > keepDistance + keepBand ? dir : Vector3.zero) * strafeStep);
        }
    }

    private bool TrySetDestination(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out var hit, 1.5f, NavMesh.AllAreas))
            return false;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
        return true;
    }

    /// <summary>Zasłonięty: znajdź na NavMeshu punkt w coverSearchRadius, z którego widać gracza (8 kierunków).</summary>
    private void SeekLineOfSight()
    {
        if (Time.time < nextCoverSearch)
            return;
        nextCoverSearch = Time.time + 0.5f;

        Vector3 best = default;
        float bestCost = float.MaxValue;
        bool found = false;
        for (int i = 0; i < 8; i++)
        {
            float rad = i * Mathf.PI * 0.25f;
            Vector3 point = transform.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * coverSearchRadius;
            if (!NavMesh.SamplePosition(point, out var hit, 1.5f, NavMesh.AllAreas))
                continue;
            Vector3 flat = player.position - hit.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d > shootRange || !HasLineOfSight(hit.position, player.position))
                continue;
            float cost = Mathf.Abs(d - keepDistance);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = hit.position;
                found = true;
            }
        }
        if (found)
        {
            agent.isStopped = false;
            agent.SetDestination(best);
        }
        else if (player != null)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    /// <summary>Promień od strzelca do gracza nie trafia w nic poza graczem i wrogami.</summary>
    private bool HasLineOfSight(Vector3 fromPosition, Vector3 playerPosition)
    {
        Vector3 origin = fromPosition + Vector3.up * muzzleHeight;
        Vector3 target = playerPosition + Vector3.up * muzzleHeight;
        Vector3 dir = target - origin;
        float distance = dir.magnitude;
        if (distance < 0.01f)
            return true;
        dir /= distance;

        int count = Physics.RaycastNonAlloc(origin, dir, HitBuffer, distance, ~(1 << 6), QueryTriggerInteraction.Ignore);
        float nearestBlocker = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = HitBuffer[i].collider;
            if (ColliderTags.IsPlayer(col) || ColliderTags.IsEnemy(col))
                continue;
            if (HitBuffer[i].distance < nearestBlocker)
                nearestBlocker = HitBuffer[i].distance;
        }
        return nearestBlocker >= distance - 0.2f;
    }

    private IEnumerator ShootRoutine()
    {
        shooting = true;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        OnWindupStarted?.Invoke();
        if (player != null)
            Telegraph.Line(transform.position + Vector3.up * muzzleHeight, player.position, windup);
        Sfx.Play(SfxKind.Windup, transform.position);

        yield return new WaitForSeconds(windup);

        int shots = Mathf.Max(1, burstCount);
        for (int i = 0; i < shots; i++)
        {
            if (player != null)
            {
                var dir = player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
                dir.Normalize();
                Face(dir);
                var projectile = ProjectilePool.Get();
                if (projectile != null)
                {
                    Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * 0.6f;
                    projectile.Launch(origin, dir, projectileSpeed, damage, projectileLifetime);
                    OnShot?.Invoke();
                    Sfx.Play(SfxKind.Shot, transform.position);
                }
            }
            if (i < shots - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
        shooting = false;
        nextShotTime = Time.time + cooldown;
        nextSideSwitch = Time.time + strafeSwitchTime;
    }

    private void MoveTo(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out var hit, 3f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void Face(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(flatDirection);
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
