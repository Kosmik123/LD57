using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Populacja poziomu: wrogowie krążący po całym bieżącym poziomie, niezależnie od stref fal.
/// Na prefabie poziomu, obok LevelEnemyTracker. Aktywna tylko, gdy poziom jest bieżący i nie wyczyszczony.
/// Start poziomu: rozsiewa targetAlive wrogów po całym NavMeshu poziomu; potem dosypuje po jednym co respawnInterval
/// z punktu 10-20 m od gracza (najlepiej niewidocznego). Nie jest liczona przez LevelEnemyTracker (krata otwiera się po strefach).
/// </summary>
public class LevelPopulation : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>Testy dymne ustawiają true: populacja nie spawnuje nic.</summary>
    public static bool DisabledForTests;

    [SerializeField, Range(1, 8)] private int levelNumber = 1;
    [SerializeField, Tooltip("Ilu wrogów populacji żyje naraz.")] private int targetAlive = 6;
    [SerializeField] private List<Entry> entries = new List<Entry>();
    [SerializeField, Tooltip("Co ile sekund dochodzi jeden wróg, gdy żywych < targetAlive.")] private float respawnInterval = 4f;
    [SerializeField] private float spawnDistanceMin = 10f;
    [SerializeField] private float spawnDistanceMax = 20f;
    [SerializeField, Tooltip("Rozsiew startowy: nie bliżej niż tyle od gracza.")] private float seedMinDistance = 8f;
    [SerializeField, Tooltip("Limit łączny spawnów na poziom (0 = bez limitu).")] private int maxTotal = 0;
    [SerializeField, Tooltip("Poziomy stoją jeden pod drugim: bierzemy trójkąty NavMesha w tym pasie wokół wysokości LevelEntry.")]
    private float heightTolerance = 3f;

    [Header("Pająk na ścianie (poziom 1) albo wspinający się z klifu (poziomy 2-8)")]
    [SerializeField, Range(0f, 1f), Tooltip("NIEUŻYWANE od P12 (zostawione, bo prefaby poziomów mają zapisaną starą wartość): pająk ZAWSZE próbuje ściany/klifu.")]
    private float spiderClimbChance = 1f;
    [SerializeField] private float climbDepthMin = 3f;
    [SerializeField] private float climbDepthMax = 5f;
    [SerializeField, Tooltip("Ile punktów bazowych sprawdzić, szukając ściany/klifu, zanim pająk wyląduje na podłodze (fallback).")]
    private int spiderSearchAttempts = 8;
    [SerializeField, Tooltip("Pająk na ścianie nie bliżej niż tyle od gracza (m).")] private float wallMinPlayerDistance = 4f;

    public int LevelNumber => levelNumber;
    public int AliveCount { get { Purge(); return alive.Count; } }
    public bool IsActive => active;

    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Vector3[]> triangles = new List<Vector3[]>();
    private bool active;
    private int totalSpawned;
    private float timer;
    private float levelHeight;
    private Transform player;

    private void OnEnable()
    {
        GameEvents.OnLevelStarted += GameEvents_OnLevelStarted;
        GameEvents.OnLevelCleared += GameEvents_OnLevelCleared;
        GameEvents.OnPlayerDied += Stop;
        GameEvents.OnRunWon += Stop;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelStarted -= GameEvents_OnLevelStarted;
        GameEvents.OnLevelCleared -= GameEvents_OnLevelCleared;
        GameEvents.OnPlayerDied -= Stop;
        GameEvents.OnRunWon -= Stop;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
    }

    private void OnDestroy()
    {
        // populacja ginie razem z poziomem (restart runu)
        foreach (var go in spawned)
            if (go != null)
                Destroy(go);
        spawned.Clear();
    }

    private void GameEvents_OnLevelStarted(int level)
    {
        if (level != levelNumber)
        {
            Stop(); // żywi zostają na starym poziomie
            return;
        }
        if (DisabledForTests || active)
            return;
        active = true;
        timer = respawnInterval;
        var entry = GetComponentInChildren<LevelEntry>();
        levelHeight = entry != null ? entry.transform.position.y : transform.position.y;
        BuildTriangles();
        // Rozsiew z opóźnieniem: poziom startuje w pierwszej klatce (zanim testy ustawią DisabledForTests), a gracz
        // i tak nie powinien dostać wrogów w tej samej sekundzie, w której wszedł na poziom.
        StartCoroutine(SeedAfterDelay(1f));
    }

    private System.Collections.IEnumerator SeedAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!active || DisabledForTests) yield break;
        SeedAll();
    }

    private void GameEvents_OnLevelCleared(int level)
    {
        if (level == levelNumber)
            Stop();
    }

    private void Stop() => active = false;

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy) => alive.Remove(enemy);

    private void Purge() => alive.RemoveAll(e => e == null);

    private void Update()
    {
        if (!active || DisabledForTests)
            return;
        Purge();
        if (alive.Count >= targetAlive)
        {
            timer = respawnInterval;
            return;
        }
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = respawnInterval;
        if (!FindPlayer())
            return;
        if (TryAroundPlayer(player.position, out var pos))
            SpawnOne(pos);
    }

    // ------------------------------------------------------------ punkty

    private bool FindPlayer()
    {
        if (player != null)
            return true;
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            player = go.transform;
        return player != null;
    }

    /// <summary>Trójkąty NavMesha należące do tego poziomu (po wysokości względem LevelEntry).</summary>
    private void BuildTriangles()
    {
        triangles.Clear();
        var tri = NavMesh.CalculateTriangulation();
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            var a = tri.vertices[tri.indices[i]];
            var b = tri.vertices[tri.indices[i + 1]];
            var c = tri.vertices[tri.indices[i + 2]];
            if (Mathf.Abs(a.y - levelHeight) > heightTolerance || Mathf.Abs(b.y - levelHeight) > heightTolerance || Mathf.Abs(c.y - levelHeight) > heightTolerance)
                continue;
            triangles.Add(new[] { a, b, c });
        }
    }

    private bool RandomPointOnLevel(out Vector3 point)
    {
        point = default;
        if (triangles.Count == 0)
            return false;
        var t = triangles[Random.Range(0, triangles.Count)];
        float u = Random.value, v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        Vector3 p = t[0] + (t[1] - t[0]) * u + (t[2] - t[0]) * v;
        if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - levelHeight) > heightTolerance)
            return false;
        point = hit.position;
        return true;
    }

    private static bool IsReachable(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private void SeedAll()
    {
        FindPlayer();
        for (int n = 0; n < targetAlive; n++)
        {
            if (maxTotal > 0 && totalSpawned >= maxTotal)
                break;
            Vector3? chosen = null;
            for (int attempt = 0; attempt < 20 && chosen == null; attempt++)
            {
                if (!RandomPointOnLevel(out var p))
                    continue;
                if (player != null)
                {
                    var d = p - player.position;
                    d.y = 0f;
                    if (d.magnitude < seedMinDistance || !IsReachable(player.position, p))
                        continue;
                }
                chosen = p;
            }
            if (chosen.HasValue)
                SpawnOne(chosen.Value);
        }
    }

    /// <summary>Punkt NavMesha spawnDistanceMin..Max od gracza na wysokości poziomu, najlepiej niewidoczny (Linecast jak w WaveZone.AroundPlayer).</summary>
    private bool TryAroundPlayer(Vector3 playerPosition, out Vector3 result)
    {
        Vector3? visibleFallback = null;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(spawnDistanceMin, spawnDistanceMax);
            Vector3 candidate = playerPosition + new Vector3(dir.x, 0f, dir.y) * r;
            if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - levelHeight) > heightTolerance)
                continue;
            bool visible = !Physics.Linecast(hit.position + Vector3.up * 0.6f, playerPosition + Vector3.up * 0.6f, ~(1 << 6), QueryTriggerInteraction.Ignore);
            if (!visible)
            {
                result = hit.position;
                return true;
            }
            visibleFallback ??= hit.position;
        }
        result = visibleFallback ?? default;
        return visibleFallback.HasValue;
    }

    // ------------------------------------------------------------ spawn

    private GameObject PickPrefab()
    {
        float sum = 0f;
        foreach (var e in entries)
            if (e != null && e.prefab != null)
                sum += e.weight;
        if (sum <= 0f)
            return null;
        float roll = Random.value * sum;
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null)
                continue;
            roll -= e.weight;
            if (roll <= 0f)
                return e.prefab;
        }
        return null;
    }

    private void SpawnOne(Vector3 navPosition)
    {
        if (maxTotal > 0 && totalSpawned >= maxTotal)
            return;
        var prefab = PickPrefab();
        if (prefab == null)
            return;

        Vector3 position = navPosition;
        Quaternion rotation = Quaternion.identity;
        bool snap = true, climbing = false, wallPerch = false;
        Vector3 ledge = default;
        // pająk ZAWSZE próbuje: poziom 1 ściana, poziomy 2-8 klif (zapasowo ta druga), podłoga tylko gdy obie zawiodą
        if (prefab.GetComponent<SpiderController>() != null)
        {
            switch (FindSpiderSpot(navPosition, out position, out rotation, out ledge))
            {
                case SpiderSpot.Cliff:
                    snap = false;
                    climbing = true;
                    break;
                case SpiderSpot.Wall:
                    snap = false;
                    wallPerch = true;
                    break;
                default:
                    SpiderSpawnHelper.SpawnFallbacks++;
                    Debug.Log($"[Spider] fallback: podłoga (poziom {levelNumber}, brak ściany i klifu w {spiderSearchAttempts} próbach)");
                    break;
            }
        }

        var instance = Instantiate(prefab, position, rotation);
        spawned.Add(instance);
        if (snap && instance.TryGetComponent<NavMeshAgent>(out var agent))
        {
            agent.enabled = false;
            instance.transform.position = position;
            agent.enabled = true;
            agent.Warp(position);
        }

        if (!instance.TryGetComponent<EnemyHealth>(out var enemy))
        {
            Debug.LogWarning($"Prefab {prefab.name} nie ma EnemyHealth, populacja go nie policzy.", this);
            return;
        }
        if (instance.TryGetComponent<SpiderController>(out var spider))
        {
            if (climbing)
                spider.BeginClimb(ledge);
            else if (wallPerch)
            {
                spider.EnableWallWalk();
                spider.PerchNow();
            }
        }
        // pająk na klifie/ścianie nie "wyjeżdża spod ziemi" (SpawnEntrance wsunąłby Visual w ścianę)
        if (!climbing && !wallPerch && instance.GetComponent<SpawnEntrance>() == null)
            instance.AddComponent<SpawnEntrance>();
        // wróg bez własnego Idle (strzelec): wędrowanie do pierwszego alarmu
        if (instance.GetComponent<SlimeController>() == null && instance.GetComponent<SpiderController>() == null
            && instance.GetComponent<Wanderer>() == null)
            instance.AddComponent<Wanderer>();
        alive.Add(enemy);
        totalSpawned++;
    }

    private enum SpiderSpot { Floor, Cliff, Wall }

    /// <summary>
    /// Szuka miejsca dla pająka: najpierw tryb główny poziomu (1 = ściana, 2-8 = klif) we wszystkich punktach bazowych
    /// (navPosition + losowe punkty poziomu), potem drugi tryb; Floor = nic nie znaleziono.
    /// </summary>
    private SpiderSpot FindSpiderSpot(Vector3 navPosition, out Vector3 position, out Quaternion rotation, out Vector3 ledge)
    {
        position = navPosition;
        rotation = Quaternion.identity;
        ledge = default;
        bool havePlayer = FindPlayer();
        Vector3 playerPos = havePlayer ? player.position : default;
        int attempts = Mathf.Max(1, spiderSearchAttempts);
        for (int pass = 0; pass < 2; pass++)
        {
            bool wall = levelNumber == 1 ? pass == 0 : pass == 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 basePoint = navPosition;
                if (attempt > 0 && !RandomPointOnLevel(out basePoint))
                    continue;
                if (wall)
                {
                    if (!SpiderSpawnHelper.FindWallAbove(basePoint, out var wp, out var wn, 20f))
                        continue;
                    if (havePlayer)
                    {
                        var d = wp - playerPos;
                        d.y = 0f;
                        if (d.magnitude < wallMinPlayerDistance)
                            continue;
                    }
                    if (wp.y < levelHeight - heightTolerance || wp.y > levelHeight + heightTolerance + 3f)
                        continue;
                    position = wp;
                    rotation = SpiderSpawnHelper.SurfaceRotation(wn, havePlayer ? playerPos - wp : Vector3.down);
                    return SpiderSpot.Wall;
                }
                float depth = Random.Range(climbDepthMin, climbDepthMax);
                if (!SpiderSpawnHelper.FindCliffBelow(basePoint, playerPos, out var cp, out var cn, out var cl, depth, 6f, 12f))
                    continue;
                if (Mathf.Abs(cl.y - levelHeight) > heightTolerance)
                    continue;
                position = cp;
                rotation = SpiderSpawnHelper.SurfaceRotation(cn, Vector3.up);
                ledge = cl;
                return SpiderSpot.Cliff;
            }
        }
        return SpiderSpot.Floor;
    }
}
