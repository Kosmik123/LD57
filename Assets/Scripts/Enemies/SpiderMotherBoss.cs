using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss: matka pająków (P12). Chodzi po ziemi (NavMeshAgent), składa jajka, strzela siecią, tupie i szarżuje.
/// Zabija się go niszcząc nogi: każda z 8 nóg (BossLeg) ma własne HP; gdy żywych nóg zostaje 3, boss się przewraca (Collapse)
/// i dopiero wtedy ginie przez Health (EnemyHealth.IsBoss -> GameManager: wygrana).
/// Health bossa jest nietykalny dla broni (collidery ciała to "Body Hurtbox" -> RouteBodyHit) i odzwierciedla sumę HP nóg (pasek).
/// Zabicie przez Health.ChangeHealth(-N) z wyłączoną nietykalnością (testy) nadal kończy run.
/// Maszyna stanów (Brain): Intro -> Walk -> {Stomp | LayEggs | Charge(+Stun) | WebShot} -> Walk ... -> Collapse.
/// </summary>
[RequireComponent(typeof(Health))]
public class SpiderMotherBoss : MonoBehaviour
{
    public enum State { Intro, Walk, LayEggs, WebShot, Stomp, Charge, Stun, Collapse }

    /// <summary>Zmiana stanu; argument to nazwa stanu (np. "Stomp") albo "Phase2"/"Phase3".</summary>
    public event System.Action<string> OnStateChanged;
    /// <summary>Zapowiedź ataku: punkt (środek tupnięcia, początek szarży, cel sieci).</summary>
    public event System.Action<Vector3> OnTelegraph;
    /// <summary>Zmiana fazy (2 po utracie 2 nóg, 3 po utracie 4 nóg).</summary>
    public event System.Action<int> OnPhaseChanged;
    /// <summary>Chwilowe zdarzenia dla animacji: "StompImpact", "WebFire", "EggLaid", "ChargeStart", "Roar", "LegBroken".</summary>
    public event System.Action<string> OnAction;
    /// <summary>Noga odpadła (przed ukryciem obiektu nogi).</summary>
    public event System.Action<BossLeg> OnLegDestroyed;

    [Header("Intro i chód")]
    [SerializeField] private float introTime = 1.5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField, Tooltip("Spowolnienie za każdą utraconą nogę (0,08 = 8 %).")] private float speedLossPerLeg = 0.08f;
    [SerializeField] private float stopDistance = 2.2f;
    [SerializeField] private float turnSpeed = 150f;

    [Header("Jajka")]
    [SerializeField] private GameObject eggPrefab;
    [SerializeField] private float eggInterval = 12f;
    [SerializeField] private float eggCrouchTime = 1.8f;
    [SerializeField] private int eggCount = 3;
    [SerializeField] private float eggSpacing = 1.5f;
    [SerializeField, Tooltip("Maks. jajek naraz (liczy SpiderEgg.CountAlive).")] private int maxEggs = 9;

    [Header("Sieć")]
    [SerializeField] private GameObject webPrefab;
    [SerializeField] private float webInterval = 7f;
    [SerializeField] private float webMinRange = 4f;
    [SerializeField] private float webMaxRange = 12f;
    [SerializeField] private float webTelegraph = 0.6f;
    [SerializeField] private float webSpeed = 14f;
    [SerializeField] private float webRange = 14f;
    [SerializeField] private int webDamage = 4;
    [SerializeField] private float webRootSeconds = 1f;
    [SerializeField] private float webMuzzleHeight = 1.6f;
    [SerializeField, Tooltip("Faza 3: odstęp między dwiema sieciami.")] private float webSecondGap = 0.45f;

    [Header("Tupnięcie")]
    [SerializeField] private float stompTriggerRange = 3f;
    [SerializeField] private float stompWindup = 0.7f;
    [SerializeField] private float stompRadius = 3f;
    [SerializeField] private int stompDamage = 12;
    [SerializeField] private float stompCooldown = 3f;
    [SerializeField] private float stompKnockback = 3f;

    [Header("Szarża")]
    [SerializeField] private float chargeInterval = 10f;
    [SerializeField] private float chargeMinRange = 6f;
    [SerializeField] private float chargeMaxRange = 14f;
    [SerializeField, Tooltip("Ryk i zapowiedź (stożek) przed szarżą.")] private float chargeRoarTime = 0.6f;
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private int chargeDamage = 15;
    [SerializeField] private float chargeConeAngle = 24f;
    [SerializeField, Tooltip("Szarża biegnie tyle metrów poza gracza.")] private float chargeOvershoot = 4f;
    [SerializeField] private float chargeMaxDistance = 16f;
    [SerializeField] private float chargeHitRadius = 2f;
    [SerializeField] private float chargeKnockback = 4f;
    [SerializeField, Tooltip("Oszołomienie po szarży: okno na nogi.")] private float stunTime = 1f;

    [Header("Nogi i fazy")]
    [SerializeField, Tooltip("Boss przewraca się, gdy żywych nóg jest tyle albo mniej.")] private int collapseAtLegsAlive = 3;
    [SerializeField, Tooltip("Obrażenia w ciało przechodzą na najbliższą nogę razy tyle.")] private float bodyHitFactor = 0.5f;
    [SerializeField] private float collapseTime = 2f;
    [SerializeField] private float fallingLegSeconds = 1.5f;
    [SerializeField, Tooltip("Faza 2 po utracie tylu nóg: cykle szybsze.")] private int phase2LegsLost = 2;
    [SerializeField, Tooltip("Faza 3 po utracie tylu nóg: dwie sieci naraz.")] private int phase3LegsLost = 4;
    [SerializeField, Tooltip("Mnożnik odstępów między atakami od fazy 2.")] private float phaseCycleScale = 0.8f;
    [SerializeField, Range(0f, 1f), Tooltip("Pierwsze ataki pojawiają się po tej części pełnego odstępu.")] private float firstAttackFraction = 0.5f;

    [SerializeField, HideInInspector, Tooltip("Stare pole z P1 (przyzwanie). Nieużywane; P1Wiring_Boss nadal je wpisuje.")]
    private GameObject minionPrefab;

    public State Current { get; private set; } = State.Intro;
    /// <summary>Faza walki: 1, 2 (utracone 2 nogi), 3 (utracone 4 nogi).</summary>
    public int Phase { get; private set; } = 1;
    public IReadOnlyList<BossLeg> Legs => legs;
    public int LegCount => legs != null ? legs.Length : 0;
    public int AliveLegs { get; private set; }
    /// <summary>Ile nóg trzeba jeszcze zniszczyć do przewrócenia.</summary>
    public int LegsToDestroy => Mathf.Max(0, AliveLegs - collapseAtLegsAlive);
    public bool IsCollapsing => collapsing;
    /// <summary>Prędkość ruchu w m/s (z różnicy pozycji, działa też przy szarży przez agent.Move).</summary>
    public float MoveSpeed { get; private set; }

    private Health health;
    private NavMeshAgent agent;
    private EnemyMeleeAttack melee;
    private BossLeg[] legs;
    private BossBodyHitbox hitbox;
    private Collider[] hitboxColliders;
    private Transform player;
    private Coroutine brain;
    private bool collapsing, dead;
    private Vector3? lookOverride;
    private Vector3 lastPos;
    private float nextEgg, nextWeb, nextCharge, nextStomp, nextRepath;

    private float SpeedMultiplier => Mathf.Max(0.3f, 1f - speedLossPerLeg * (LegCount - AliveLegs));
    private float CycleScale => Phase >= 2 ? phaseCycleScale : 1f;

    private void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        melee = GetComponent<EnemyMeleeAttack>();
        hitbox = GetComponentInChildren<BossBodyHitbox>(true);
        hitboxColliders = hitbox != null ? hitbox.GetComponentsInChildren<Collider>(true) : new Collider[0];

        legs = GetComponentsInChildren<BossLeg>(true);
        System.Array.Sort(legs, (a, b) => a.Index.CompareTo(b.Index));
        AliveLegs = legs.Length;

        // Bron nie rani Health bossa: obrażenia idą w nogi (BossLeg) albo w ciało (BossBodyHitbox -> RouteBodyHit).
        health.IsInvulnerable = true;
        if (melee != null) melee.enabled = false; // atak bliski zastąpiło Stomp
        if (agent != null) agent.updateRotation = false;
        lastPos = transform.position;
    }

    private void OnEnable()
    {
        health.OnDied += Health_OnDied;
        if (legs != null)
            foreach (var leg in legs)
            {
                leg.OnDamaged += Leg_OnDamaged;
                leg.OnBroken += Leg_OnBroken;
            }
        SetCameraZoom(0.9f); // walka z bossem: kamera lekko bliżej
    }

    private void OnDisable()
    {
        health.OnDied -= Health_OnDied;
        if (legs != null)
            foreach (var leg in legs)
            {
                if (leg == null) continue;
                leg.OnDamaged -= Leg_OnDamaged;
                leg.OnBroken -= Leg_OnBroken;
            }
        SetCameraZoom(1f);
    }

    private static void SetCameraZoom(float mult)
    {
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null) cam.SetZoom(mult);
    }

    // Start, nie OnEnable: WaveZone ustawia pozycję i robi Warp zaraz po Instantiate.
    private void Start()
    {
        float now = Time.time;
        nextEgg = now + eggInterval * firstAttackFraction;
        nextWeb = now + webInterval * firstAttackFraction;
        nextCharge = now + chargeInterval * firstAttackFraction;
        nextStomp = now;
        lastPos = transform.position;
        if (!collapsing && !dead)
            brain = StartCoroutine(Brain());
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        // SpawnEntrance (WaveZone) włącza EnemyMeleeAttack po wejściu; boss bije tupnięciem, nie zwykłym atakiem.
        if (melee != null && melee.enabled)
            melee.enabled = false;

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            var delta = transform.position - lastPos;
            delta.y = 0f;
            MoveSpeed = Mathf.Lerp(MoveSpeed, delta.magnitude / dt, 0.3f);
        }
        lastPos = transform.position;

        if (dead || collapsing) return;
        Vector3 look = lookOverride ?? (player != null ? player.position - transform.position : transform.forward);
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), turnSpeed * dt);
    }

    // ------------------------------------------------------------------ obrażenia

    /// <summary>Trafienie w ciało (BossBodyHitbox): przenosi obrażenia x bodyHitFactor na najbliższą żywą nogę.</summary>
    public void RouteBodyHit(int damage)
    {
        if (collapsing || dead || damage <= 0) return;
        BossLeg best = null;
        float bestSqr = float.MaxValue;
        Vector3 from = player != null ? player.position : transform.position;
        foreach (var leg in legs)
        {
            if (!leg.IsAlive) continue;
            float sqr = (leg.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = leg; }
        }
        if (best != null)
            best.Damage(Mathf.Max(1, Mathf.RoundToInt(damage * bodyHitFactor)));
    }

    /// <summary>Health bossa = suma HP żywych nóg: odbij obrażenia nogi w Health roota (liczby, błysk, pasek).</summary>
    private void Leg_OnDamaged(BossLeg leg, int amount)
    {
        if (dead || health.IsDead) return;
        amount = Mathf.Min(amount, health.CurrentHealth - 1); // nigdy nie zabijaj tą drogą
        if (amount <= 0) return;
        bool inv = health.IsInvulnerable;
        health.IsInvulnerable = false;
        health.ChangeHealth(-amount);
        health.IsInvulnerable = inv;
    }

    private void Leg_OnBroken(BossLeg leg)
    {
        if (dead) return;
        AliveLegs = 0;
        foreach (var l in legs) if (l.IsAlive) AliveLegs++;

        // Efekty: odłamki, odpadająca noga, wstrząs.
        var pos = leg.transform.position + Vector3.up * 0.3f;
        var outward = leg.transform.forward;
        outward.y = 0.4f;
        VoxelFx.Shards(pos, outward, new Color(0.45f, 0.05f, 0.08f), 14, true, 7f);
        VoxelFx.Shards(pos, outward, new Color(3.2f, 0.8f, 0.4f), 6, false, 6f);
        VoxelFx.Flash(pos, new Color(3.2f, 0.9f, 0.5f), 1.3f);
        var cam = CameraShake.Instance;
        if (cam != null) cam.Shake(0.25f, 0.2f);
        Sfx.Play(SfxKind.EnemyDie, pos);
        SpawnFallingLeg(leg);
        OnLegDestroyed?.Invoke(leg);
        OnAction?.Invoke("LegBroken");
        leg.gameObject.SetActive(false);

        int lost = LegCount - AliveLegs;
        if (lost >= phase2LegsLost && Phase < 2) SetPhase(2);
        if (lost >= phase3LegsLost && Phase < 3) SetPhase(3);

        if (AliveLegs <= collapseAtLegsAlive)
            BeginCollapse();
    }

    private void SpawnFallingLeg(BossLeg leg)
    {
        var clone = Instantiate(leg.gameObject, leg.transform.position, leg.transform.rotation);
        clone.name = "Detached Leg";
        // Kolejność ma znaczenie: BossLeg wymaga Health.
        foreach (var c in clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        var rb = clone.GetComponent<Rigidbody>(); if (rb != null) DestroyImmediate(rb);
        var bl = clone.GetComponent<BossLeg>(); if (bl != null) DestroyImmediate(bl);
        var hp = clone.GetComponent<Health>(); if (hp != null) DestroyImmediate(hp);
        clone.tag = "Untagged";
        clone.transform.SetParent(null, true);
        clone.transform.localScale = leg.transform.lossyScale;
        var dir = leg.transform.forward; dir.y = 0f;
        var velocity = dir.normalized * Random.Range(2f, 4f) + Vector3.up * Random.Range(4f, 6f) + Random.insideUnitSphere * 0.8f;
        DetachedLegPiece.Launch(clone, velocity, fallingLegSeconds);
    }

    private void SetPhase(int phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(Phase);
        OnStateChanged?.Invoke("Phase" + Phase);
        OnAction?.Invoke("Roar");
        Sfx.Play(SfxKind.BossTelegraph, transform.position);
    }

    // ------------------------------------------------------------------ śmierć / przewrócenie

    private void Health_OnDied()
    {
        dead = true;
        StopAllCoroutines();
        brain = null;
        StopAgent();
        if (melee != null) melee.enabled = false;
        foreach (var leg in legs)
            if (leg != null && leg.Health != null) leg.Health.IsInvulnerable = true;
    }

    private void BeginCollapse()
    {
        if (collapsing || dead) return;
        collapsing = true;
        if (brain != null) StopCoroutine(brain);
        brain = null;
        StartCoroutine(CollapseRoutine());
    }

    private IEnumerator CollapseRoutine()
    {
        // Zatrzymaj wszystko, nogi i ciało niezniszczalne na czas przewracania.
        foreach (var leg in legs)
            if (leg.IsAlive) leg.Health.IsInvulnerable = true;
        foreach (var c in hitboxColliders) if (c != null) c.enabled = false;
        lookOverride = null;
        StopAgent();
        SetState(State.Collapse);
        var pos = transform.position;
        Sfx.Play(SfxKind.BossTelegraph, pos);
        var cam = CameraShake.Instance;
        if (cam != null) cam.Shake(collapseTime, 0.18f);
        VoxelFx.VoxelRing(pos, 3.5f, new Color(1.6f, 0.4f, 0.3f), 0.8f);
        yield return new WaitForSeconds(collapseTime * 0.6f);
        VoxelFx.Burst(pos + Vector3.up * 0.5f, new Color(0.5f, 0.05f, 0.08f), 24, 0.25f, 6f, 0.8f, 1f, true);
        if (cam != null) cam.Shake(0.4f, 0.35f);
        yield return new WaitForSeconds(collapseTime * 0.4f);

        health.IsInvulnerable = false;
        health.ChangeHealth(-health.MaxHealth); // OnEnemyDied -> GameManager (wygrana), EnemyDeath niszczy obiekt
    }

    // ------------------------------------------------------------------ mózg

    private void SetState(State state)
    {
        if (Current == state && state != State.Intro) return;
        Current = state;
        OnStateChanged?.Invoke(state.ToString());
    }

    private bool HavePlayer => player != null;

    private float FlatDistanceToPlayer()
    {
        var d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private IEnumerator Brain()
    {
        yield return IntroRoutine();
        while (!collapsing && !dead)
        {
            if (!enabled || !HavePlayer) { yield return null; continue; }

            float dist = FlatDistanceToPlayer();
            float t = Time.time;
            if (dist <= stompTriggerRange && t >= nextStomp)
                yield return StompRoutine();
            else if (t >= nextEgg && SpiderEgg.CountAlive < maxEggs)
                yield return EggsRoutine();
            else if (t >= nextCharge && dist >= chargeMinRange && dist <= chargeMaxRange)
                yield return ChargeRoutine();
            else if (t >= nextWeb && dist >= webMinRange && dist <= webMaxRange)
                yield return WebRoutine();
            else
            {
                ChaseStep(dist);
                yield return null;
            }
        }
    }

    private IEnumerator IntroRoutine()
    {
        SetState(State.Intro);
        StopAgent();
        var pos = transform.position;
        Sfx.Play(SfxKind.BossTelegraph, pos);
        var cam = CameraShake.Instance;
        if (cam != null) cam.Shake(introTime, 0.12f);
        VoxelFx.VoxelRing(pos, 4f, new Color(1.6f, 0.3f, 0.25f), 0.9f);
        yield return new WaitForSeconds(introTime); // ryk gra animator na stan Intro
    }

    // ------------------------------------------------------------------ ruch

    private bool AgentUsable => agent != null && agent.enabled && agent.isOnNavMesh;

    private void StopAgent()
    {
        if (AgentUsable)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void ChaseStep(float dist)
    {
        SetState(State.Walk);
        float speed = walkSpeed * SpeedMultiplier;
        if (AgentUsable)
        {
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            agent.isStopped = false;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                agent.SetDestination(player.position);
            }
        }
        else if (dist > stopDistance)
        {
            // Awaryjnie bez NavMesha (np. test na gołej scenie): prosto na gracza.
            var dir = player.position - transform.position;
            dir.y = 0f;
            transform.position += dir.normalized * speed * Time.deltaTime;
        }
    }

    private Vector3 FlatDirectionToPlayer()
    {
        var d = player != null ? player.position - transform.position : transform.forward;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    // ------------------------------------------------------------------ tupnięcie

    private IEnumerator StompRoutine()
    {
        SetState(State.Stomp);
        StopAgent();
        var center = transform.position;
        Telegraph.Circle(center, stompRadius, stompWindup);
        OnTelegraph?.Invoke(center);
        Sfx.Play(SfxKind.Windup, center);
        yield return new WaitForSeconds(stompWindup);

        OnAction?.Invoke("StompImpact");
        DamagePlayerInRadius(center, stompRadius, stompDamage, stompKnockback);
        Sfx.Play(SfxKind.Hit, center);
        VoxelFx.VoxelRing(center, stompRadius, new Color(1.5f, 0.6f, 0.4f), 0.5f);
        VoxelFx.Shards(center + Vector3.up * 0.1f, Vector3.up, new Color(0.3f, 0.25f, 0.22f), 14, true, 6f);
        var cam = CameraShake.Instance;
        if (cam != null) cam.Shake(0.3f, 0.3f);
        yield return new WaitForSeconds(0.45f);
        nextStomp = Time.time + stompCooldown * CycleScale;
    }

    /// <summary>Obrażenia gracza w promieniu od punktu (jedna próba na Health). True, jeśli gracz oberwał.</summary>
    private bool DamagePlayerInRadius(Vector3 center, float radius, int damage, float knockback)
    {
        var hits = Physics.OverlapSphere(center + Vector3.up * 0.5f, radius, ~0, QueryTriggerInteraction.Ignore);
        bool any = false;
        var done = new HashSet<Health>();
        foreach (var col in hits)
        {
            if (!ColliderTags.IsPlayer(col)) continue;
            var rb = col.attachedRigidbody;
            var target = rb != null ? rb.GetComponent<Health>() : col.GetComponentInParent<Health>();
            if (target == null || !done.Add(target)) continue;
            if (target.IsInvulnerable) continue; // dash = nietykalność
            target.ChangeHealth(-damage);
            any = true;
            if (rb != null && !rb.isKinematic && knockback > 0f)
            {
                var dir = rb.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
                rb.AddForce(dir * knockback, ForceMode.VelocityChange);
            }
        }
        return any;
    }

    // ------------------------------------------------------------------ jajka

    private IEnumerator EggsRoutine()
    {
        SetState(State.LayEggs);
        StopAgent();
        int count = Mathf.Max(0, Mathf.Min(eggCount, maxEggs - SpiderEgg.CountAlive));
        Sfx.Play(SfxKind.Windup, transform.position);
        float step = eggCrouchTime / (count + 1);
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(step);
            LayEgg(i);
        }
        yield return new WaitForSeconds(step);
        nextEgg = Time.time + eggInterval * CycleScale;
    }

    private void LayEgg(int i)
    {
        Vector3 behind = transform.position - transform.forward * (eggSpacing * (i + 1) + 1.2f);
        if (NavMesh.SamplePosition(behind, out var hit, 2f, NavMesh.AllAreas))
            behind = hit.position;
        else if (Physics.Raycast(behind + Vector3.up * 3f, Vector3.down, out var ray, 8f, ~0, QueryTriggerInteraction.Ignore))
            behind = ray.point;
        if (eggPrefab != null)
            Instantiate(eggPrefab, behind, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        OnAction?.Invoke("EggLaid");
        VoxelFx.Burst(behind + Vector3.up * 0.2f, new Color(0.95f, 0.9f, 0.7f), 8, 0.12f, 3f, 0.4f, 1f, true);
    }

    // ------------------------------------------------------------------ sieć

    private IEnumerator WebRoutine()
    {
        SetState(State.WebShot);
        StopAgent();
        int shots = Phase >= 3 ? 2 : 1;
        for (int s = 0; s < shots; s++)
        {
            Vector3 dir = FlatDirectionToPlayer();
            lookOverride = dir;
            Vector3 groundFrom = transform.position + dir * 1.2f;
            Telegraph.Line(groundFrom, groundFrom + dir * webRange, webTelegraph);
            OnTelegraph?.Invoke(player != null ? player.position : groundFrom + dir * webRange);
            Sfx.Play(SfxKind.Windup, transform.position);
            yield return new WaitForSeconds(webTelegraph);

            Vector3 muzzle = transform.position + Vector3.up * webMuzzleHeight + dir * 1.6f;
            if (webPrefab != null)
            {
                var go = Instantiate(webPrefab, muzzle, Quaternion.LookRotation(dir));
                var web = go.GetComponent<WebProjectile>();
                if (web != null) web.Launch(transform, muzzle, dir, webSpeed, webRange, webDamage, webRootSeconds);
                else Destroy(go);
            }
            OnAction?.Invoke("WebFire");
            Sfx.Play(SfxKind.Shot, muzzle);
            VoxelFx.Burst(muzzle, Color.white, 8, 0.1f, 4f, 0.3f);
            if (s + 1 < shots)
                yield return new WaitForSeconds(webSecondGap);
        }
        yield return new WaitForSeconds(0.4f);
        lookOverride = null;
        nextWeb = Time.time + webInterval * CycleScale;
    }

    // ------------------------------------------------------------------ szarża

    private IEnumerator ChargeRoutine()
    {
        SetState(State.Charge);
        StopAgent();
        Vector3 dir = FlatDirectionToPlayer();
        float distance = Mathf.Min(chargeMaxDistance, FlatDistanceToPlayer() + chargeOvershoot);
        lookOverride = dir;
        var origin = transform.position;
        Telegraph.Cone(origin, dir, chargeConeAngle, distance, chargeRoarTime);
        OnTelegraph?.Invoke(origin);
        Sfx.Play(SfxKind.BossTelegraph, origin);
        OnAction?.Invoke("Roar");
        yield return new WaitForSeconds(chargeRoarTime);

        OnAction?.Invoke("ChargeStart");
        float speed = chargeSpeed * SpeedMultiplier;
        float travelled = 0f;
        bool hit = false;
        int blocked = 0;
        while (travelled < distance && !collapsing && !dead)
        {
            float step = speed * Time.deltaTime;
            var before = transform.position;
            var delta = dir * step;
            if (AgentUsable) agent.Move(delta);
            else transform.position += delta;
            float moved = (transform.position - before).magnitude;
            travelled += moved;
            blocked = moved < step * 0.25f ? blocked + 1 : 0;
            if (blocked > 3) break; // ściana

            if (!hit && DamagePlayerInRadius(transform.position + dir * 1.2f, chargeHitRadius, chargeDamage, chargeKnockback))
                hit = true;
            if (Random.value < 0.5f)
                VoxelFx.Burst(transform.position + Vector3.up * 0.1f, new Color(0.35f, 0.3f, 0.25f), 2, 0.2f, 2f, 0.5f, 1f, true);
            yield return null;
        }

        lookOverride = null;
        SetState(State.Stun);
        var cam = CameraShake.Instance;
        if (cam != null) cam.Shake(0.2f, 0.15f);
        yield return new WaitForSeconds(stunTime);
        nextCharge = Time.time + chargeInterval * CycleScale;
    }
}
