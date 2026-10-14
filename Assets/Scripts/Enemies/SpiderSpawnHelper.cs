using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wspólna logika pająków na ścianach i klifach: raycasty po geometrii poziomu (zawsze z queriesHitBackfaces = true,
/// bo siatki poziomów 2-8 mają normalne w dół), szukanie ściany nad podłogą (poziom 1) i klifu pod krawędzią półki
/// (poziomy 2-8), statyczne liczniki do testów oraz SpawnForTest. Liczniki podnosi SpiderController; Reset() zeruje.
/// </summary>
public static class SpiderSpawnHelper
{
    public static int WallPerches;
    public static int ClimbsStarted;
    public static int ClimbsReachedFloor;
    public static int ClimbWarps;
    public static int WallWalks;
    public static int Leaps;
    public static int SpawnFallbacks;
    /// <summary>"cliff", "wall" albo "floor": co zwrócił ostatni SpawnForTest.</summary>
    public static string LastSpawnMode = "";

    private static readonly RaycastHit[] buffer = new RaycastHit[32];
    private static Vector3[] navVertices;
    private static float navVerticesAt = -1000f;

    /// <summary>Wszystko oprócz warstwy Player (6) i Ignore Raycast (2).</summary>
    public static int Mask => ~((1 << 6) | (1 << 2));

    public static void Reset()
    {
        WallPerches = ClimbsStarted = ClimbsReachedFloor = ClimbWarps = WallWalks = Leaps = SpawnFallbacks = 0;
        LastSpawnMode = "";
    }

    public static string Summary() =>
        $"wallPerches={WallPerches} climbsStarted={ClimbsStarted} climbsReachedFloor={ClimbsReachedFloor} climbWarps={ClimbWarps} wallWalks={WallWalks} leaps={Leaps} fallbacks={SpawnFallbacks}";

    // ------------------------------------------------------------ raycast

    /// <summary>
    /// Najbliższe trafienie w statyczny collider poziomu (bez triggerów, bez Rigidbody, bez dzieci `ignore`), także w tylną ścianę trójkąta.
    /// faceOrigin: normalna odwrócona tak, by patrzyła w stronę początku promienia (normalne tylnych ścian są odwrócone).
    /// </summary>
    public static bool Raycast(Vector3 origin, Vector3 dir, float distance, Transform ignore, out RaycastHit best, bool faceOrigin = true)
    {
        best = default;
        if (dir.sqrMagnitude < 0.0001f)
            return false;
        dir.Normalize();
        bool prev = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        int n;
        try { n = Physics.RaycastNonAlloc(origin, dir, buffer, distance, Mask, QueryTriggerInteraction.Ignore); }
        finally { Physics.queriesHitBackfaces = prev; }

        float min = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = buffer[i];
            if (h.distance <= 0.0001f || h.distance >= min)
                continue;
            var col = h.collider;
            if (col == null || col.isTrigger || col.attachedRigidbody != null)
                continue;
            if (ignore != null && col.transform.IsChildOf(ignore))
                continue;
            best = h;
            min = h.distance;
            found = true;
        }
        if (found && faceOrigin && Vector3.Dot(best.normal, dir) > 0f)
            best.normal = -best.normal;
        return found;
    }

    public static Quaternion SurfaceRotation(Vector3 normal, Vector3 preferredForward)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(preferredForward, normal);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(Vector3.up, normal);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(Vector3.forward, normal);
        return Quaternion.LookRotation(fwd.normalized, normal);
    }

    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ------------------------------------------------------------ ściana nad podłogą (poziom 1)

    /// <summary>
    /// 16 kierunków poziomych z floor + 1,5 m w górę. Zwraca ścianę (|normal.y| &lt; 0,3, co najmniej 1,5 m od punktu) na wysokości
    /// 2-3 m nad podłogą, punkt 0,4 m od niej, normalna zwrócona do wnętrza pomieszczenia. nearest: najbliższa zamiast losowej.
    /// </summary>
    public static bool FindWallAbove(Vector3 floor, out Vector3 point, out Vector3 normal, float maxDist = 20f, bool nearest = false) // korytarze mają 10–20 m szerokości
    {
        if (FindWallFrom(floor, out point, out normal, maxDist, nearest))
            return true;
        // Korytarze jamowej jaskini mają 10–20 m szerokości: ze środka ściany nie widać. Szukaj od najbliższej
        // krawędzi NavMesha (na poziomie 1 ściana stoi tuż za nią).
        if (NavMesh.FindClosestEdge(floor, out var edge, NavMesh.AllAreas) && edge.distance > 0.3f && edge.distance < 40f)
        {
            var inward = floor - edge.position; inward.y = 0f;
            inward = inward.sqrMagnitude > 0.001f ? inward.normalized : Vector3.forward;
            var from = edge.position + inward * 0.6f;
            if (FindWallFrom(from, out point, out normal, 6f, true))
                return true;
        }
        return false;
    }

    private static bool FindWallFrom(Vector3 floor, out Vector3 point, out Vector3 normal, float maxDist, bool nearest)
    {
        point = default;
        normal = Vector3.up;
        Vector3 origin = floor + Vector3.up * 1.5f;
        float start = Random.value * 22.5f;
        int chosen = -1;
        float chosenDist = float.MaxValue;
        int valid = 0;
        var dirs = new Vector3[16];
        var hits = new RaycastHit[16];
        for (int i = 0; i < 16; i++)
        {
            float a = (start + i * 22.5f) * Mathf.Deg2Rad;
            dirs[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (!Raycast(origin, dirs[i], maxDist, null, out hits[i]) || Mathf.Abs(hits[i].normal.y) >= 0.3f || hits[i].distance < 1.5f)
            {
                hits[i].distance = -1f;
                continue;
            }
            valid++;
            if (nearest)
            {
                if (hits[i].distance < chosenDist) { chosenDist = hits[i].distance; chosen = i; }
            }
            else if (Random.value * valid <= 1f) // rezerwuarowe losowanie
                chosen = i;
        }
        if (chosen < 0)
            return false;

        var hit = hits[chosen];
        // wysokość 2-3 m nad podłogą: ta sama ściana wyżej, o ile tam jest
        Vector3 high = new Vector3(origin.x, floor.y + Random.Range(2f, 3f), origin.z);
        if (Raycast(high, dirs[chosen], maxDist + 1f, null, out var h2) && Mathf.Abs(h2.normal.y) < 0.3f
            && Vector3.Dot(h2.normal, hit.normal) > 0.7f)
            hit = h2;

        normal = hit.normal;
        point = hit.point + hit.normal * 0.4f;
        return true;
    }

    // ------------------------------------------------------------ klif pod krawędzią (poziomy 2-8)

    private static bool OnNav(Vector3 p, float y)
        => NavMesh.SamplePosition(p, out var h, 0.3f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - y) < 1f;

    private static bool NavCenter(Vector3 around, float radius, out Vector3 center)
    {
        if (navVertices == null || Time.realtimeSinceStartup - navVerticesAt > 30f)
        {
            navVertices = NavMesh.CalculateTriangulation().vertices;
            navVerticesAt = Time.realtimeSinceStartup;
        }
        Vector3 sum = default;
        int count = 0;
        foreach (var v in navVertices)
        {
            if (Mathf.Abs(v.y - around.y) < 6f && Flat(v - around) <= radius)
            {
                sum += v;
                count++;
            }
        }
        center = count > 0 ? sum / count : around;
        return count > 0;
    }

    /// <summary>Kierunek poziomy "na zewnątrz" od krawędzi NavMesha: po sondach (NavMesh po jednej stronie), w razie niejednoznaczności od środka NavMesha w 15 m.</summary>
    private static bool OutwardAt(NavMeshHit edge, out Vector3 outward)
    {
        outward = default;
        Vector3 e = edge.position;
        Vector3 n = edge.normal;
        n.y = 0f;
        if (n.sqrMagnitude > 0.0001f)
        {
            n.Normalize();
            bool plus = OnNav(e - n * 0.7f, e.y) && !OnNav(e + n * 0.9f, e.y);   // na zewnątrz = +n
            bool minus = OnNav(e + n * 0.7f, e.y) && !OnNav(e - n * 0.9f, e.y);  // na zewnątrz = -n
            if (plus != minus)
            {
                outward = plus ? n : -n;
                return true;
            }
        }
        if (NavCenter(e, 15f, out var center))
        {
            Vector3 d = e - center;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
            {
                outward = d.normalized;
                return true;
            }
        }
        return false;
    }

    private static bool CliffHit(Vector3 start, Vector3 outward, out RaycastHit hit)
    {
        // start w skale (klif podcięty): tylna ściana tuż na zewnątrz (+outward, do 1 m); start w powietrzu: klif jest ku półce (-outward, do 3 m)
        foreach (var dir in new[] { outward, -outward })
        {
            if (!Raycast(start, dir, dir == outward ? 1f : 3f, null, out hit, false))
                continue;
            Vector3 n = hit.normal;
            if (Vector3.Dot(n, outward) < 0f)
                n = -n;
            if (Vector3.Dot(n, outward) > 0.5f && Mathf.Abs(n.y) < 0.5f)
            {
                hit.normal = n;
                return true;
            }
        }
        hit = default;
        return false;
    }

    /// <summary>
    /// Klif pod krawędzią NavMesha najbliższą nearPoint. point: 0,4 m od klifu, normal: normalna klifu (na zewnątrz półki),
    /// ledge: punkt krawędzi na NavMeshu (cel wspinaczki). playerPos (default = pomiń) odrzuca krawędzie bliżej niż minPlayerDistance.
    /// </summary>
    public static bool FindCliffBelow(Vector3 nearPoint, Vector3 playerPos, out Vector3 point, out Vector3 normal, out Vector3 ledge,
        float depth = 4f, float minPlayerDistance = 5f, float maxEdgeDistance = 12f)
    {
        point = default;
        normal = Vector3.up;
        ledge = default;
        if (!NavMesh.SamplePosition(nearPoint, out var src, 4f, NavMesh.AllAreas))
            return false;
        if (!NavMesh.FindClosestEdge(src.position, out var edge, NavMesh.AllAreas) || Flat(edge.position - src.position) > maxEdgeDistance)
            return false;
        if (playerPos != default && Flat(edge.position - playerPos) < minPlayerDistance)
            return false;
        if (!OutwardAt(edge, out var outward))
            return false;

        foreach (float d in new[] { depth, 3f, 4.5f, 2.5f, 5.5f })
        {
            Vector3 start = edge.position + outward * 0.6f + Vector3.down * d;
            if (!CliffHit(start, outward, out var hit))
                continue;
            normal = hit.normal;
            point = hit.point + hit.normal * 0.4f;
            ledge = edge.position;
            return true;
        }

        // stromy zbocze zamiast pionowego klifu: promień w dół za krawędzią; podłoga (normal.y >= 0,5) albo pustka = brak klifu
        if (Raycast(edge.position + outward * 0.3f + Vector3.up * 0.5f, Vector3.down, 12f, null, out var slope)
            && slope.normal.y < 0.5f && slope.normal.y > -0.5f)
        {
            normal = slope.normal;
            point = slope.point + slope.normal * 0.4f;
            ledge = edge.position;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------ test

    /// <summary>
    /// Pająk do testu: preferClimb = klif pod krawędzią NavMesha najbliższą nearPlayer (BeginClimb), inaczej ściana (EnableWallWalk + PerchNow);
    /// drugi wariant jest zapasowy, a gdy oba zawiodą: podłoga (SpawnFallbacks++). LastSpawnMode mówi, co wyszło.
    /// Liczniki rosną od razu: ClimbsStarted albo WallPerches (ściana), potem WallWalks (po wykryciu gracza w 12 m).
    /// </summary>
    public static GameObject SpawnForTest(GameObject prefab, Vector3 nearPlayer, Vector3 playerPos, bool preferClimb)
    {
        Vector3 point = default, normal = default, ledge = default;
        string mode = null;
        for (int pass = 0; pass < 2 && mode == null; pass++)
        {
            bool climbPass = preferClimb ? pass == 0 : pass == 1;
            if (climbPass)
            {
                if (FindCliffBelow(nearPlayer, playerPos, out point, out normal, out ledge, 4f, 2f, 20f))
                    mode = "cliff";
            }
            else if (FindWallAbove(nearPlayer, out point, out normal, 20f, true))
                mode = "wall";
        }

        GameObject instance;
        if (mode == "cliff")
        {
            instance = Object.Instantiate(prefab, point, SurfaceRotation(normal, Vector3.up));
            if (instance.TryGetComponent<SpiderController>(out var spider))
                spider.BeginClimb(ledge);
        }
        else if (mode == "wall")
        {
            Vector3 toPlayer = playerPos - point;
            instance = Object.Instantiate(prefab, point, SurfaceRotation(normal, toPlayer));
            if (instance.TryGetComponent<SpiderController>(out var spider))
            {
                spider.EnableWallWalk();
                spider.PerchNow();
            }
        }
        else
        {
            mode = "floor";
            SpawnFallbacks++;
            Debug.Log("[Spider] fallback: podłoga (brak ściany i klifu przy teście)");
            Vector3 pos = NavMesh.SamplePosition(nearPlayer, out var hit, 4f, NavMesh.AllAreas) ? hit.position : nearPlayer;
            instance = Object.Instantiate(prefab, pos, Quaternion.identity);
            if (instance.TryGetComponent<NavMeshAgent>(out var agent))
            {
                agent.enabled = false;
                instance.transform.position = pos;
                agent.enabled = true;
                agent.Warp(pos);
            }
        }
        LastSpawnMode = mode;
        return instance;
    }
}
