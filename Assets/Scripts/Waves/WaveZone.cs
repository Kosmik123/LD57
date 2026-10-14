using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Strefa fali: trigger na ścieżce poziomu. Pierwsze wejście gracza spawnuje falę z listy wpisów.
/// Gdy ostatni wróg tej fali zginie: GameEvents.RaiseWaveCleared(this) i serce w miejscu ostatniego wroga.
/// KONTRAKT (właściciel: agent Run flow). Pola publiczne tutaj są częścią kontraktu i nie zmieniają nazw.
/// </summary>
public class WaveZone : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("Prefab wroga z EnemyHealth. Musi stać na NavMeshu po spawnie.")]
        public GameObject EnemyPrefab;
        [Tooltip("Punkt spawnu (na podłodze, na NavMeshu). Dla pająka: punkt na ścianie, pająk sam zeskakuje.")]
        public Transform SpawnPoint;
        [Min(1)]
        public int Count = 1;
    }

    /// <summary>Wróg zespawnowany przez tę strefę. Używane przez LevelEnemyTracker.</summary>
    public event System.Action<WaveZone, EnemyHealth> OnEnemySpawned;

    [SerializeField]
    private SpawnEntry[] entries;
    public SpawnEntry[] Entries => entries;

    [SerializeField, Tooltip("Prefab serca (HeartPickup) tworzony po wybiciu fali.")]
    private GameObject heartPrefab;

    [SerializeField, Tooltip("Promień rozsunięcia, gdy Count > 1.")]
    private float spreadRadius = 0.8f;

    [SerializeField, Tooltip("Ilu wrogów tej strefy żyje naraz; reszta dochodzi strumieniem z okolicy gracza (6–11 m, zza rogu).")]
    private int maxAlive = 8;
    [SerializeField] private float spawnInterval = 0.9f;
    [SerializeField, Tooltip("Ilu wrogów na jeden tik strumienia (gdy jest miejsce).")] private int perTick = 2;
    [SerializeField] private float streamRadiusMin = 6f;
    [SerializeField] private float streamRadiusMax = 11f;

    /// <summary>Testy dymne: cała strefa od razu (bez strumienia), żeby liczenie i wybijanie było deterministyczne.</summary>
    public static bool InstantSpawnForTests;

    [SerializeField, Tooltip("Po drugim pulsie cała fala od razu wie o graczu (EnemyAlert.Raise).")]
    private bool alertOnSpawn = true;

    private const float HideSearchRadius = 8f;
    private bool pendingPulse;
    private Vector3? lastDeathPosition;

    /// <summary>Liczba wrogów, jaką ta strefa spawnuje łącznie (suma Count). LevelEnemyTracker sumuje to po wszystkich strefach poziomu.</summary>
    public int TotalEnemyCount
    {
        get
        {
            int total = 0;
            if (entries != null)
                foreach (var entry in entries)
                    total += entry.Count;
            return total;
        }
    }

    /// <summary>True po pierwszym wejściu gracza.</summary>
    public bool IsTriggered { get; protected set; }

    /// <summary>True gdy wszyscy wrogowie tej strefy są martwi.</summary>
    public bool IsCleared { get; protected set; }

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private void OnDestroy()
    {
        // Strefa ginie razem z poziomem: zabierz ze sobą wrogów i serca, które zostały.
        foreach (var go in spawnedObjects)
            if (go != null)
                Destroy(go);
        spawnedObjects.Clear();
    }

    private void OnEnable()
    {
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
    }

    private float armedAt = -1f;

    private void Start()
    {
        if (armedAt < 0f) armedAt = Time.time + 1f; // strefa reaguje sekundę po starcie poziomu
    }

    private void OnTriggerEnter(Collider other) => TryTrigger(other);

    /// <summary>Gracz może zacząć w środku triggera (wejście poziomu tuż przy strefie): Stay łapie go po uzbrojeniu.</summary>
    private void OnTriggerStay(Collider other) => TryTrigger(other);

    private void TryTrigger(Collider other)
    {
        if (armedAt < 0f) armedAt = Time.time + 1f; // uzbrojenie sekundę po pierwszym kontakcie/starcie
        if (Time.time < armedAt) return;
        if (IsTriggered || !ColliderTags.IsPlayer(other))
            return;
        // wszystkie poziomy stoją w scenie naraz (pierścienie obok siebie): strefa innego poziomu nie reaguje
        if (LevelManager.Instance != null)
        {
            var tracker = GetComponentInParent<LevelEnemyTracker>();
            if (tracker != null && tracker.LevelNumber != LevelManager.Instance.CurrentLevel)
                return;
        }

        IsTriggered = true;
        SpawnAll();
    }

    private void SpawnAll()
    {
        if (entries == null)
            return;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        pendingPulse = true;
        var queue = BuildQueue();
        var player = FindPlayer();

        // wysyp startowy z punktów spawnu strefy
        int initial = InstantSpawnForTests ? queue.Count : Mathf.Min(Mathf.Max(1, maxAlive), queue.Count);
        SpawnFromQueue(queue, initial, false, player);
        if (alertOnSpawn && aliveEnemies.Count > 0)
            EnemyAlert.Raise(transform.position);

        // strumień: dopóki jest kolejka, dosypuj z okolicy gracza, gdy zrobi się miejsce
        while (queue.Count > 0)
        {
            yield return new WaitForSeconds(spawnInterval);
            int room = Mathf.Max(1, maxAlive) - aliveEnemies.Count;
            if (room <= 0) continue;
            if (player == null) player = FindPlayer();
            SpawnFromQueue(queue, Mathf.Min(Mathf.Max(1, perTick), room), true, player);
        }
        pendingPulse = false;

        // strefa bez wrogów (albo wszyscy już polegli) jest od razu wyczyszczona
        if (!IsCleared && aliveEnemies.Count == 0)
            Clear(lastDeathPosition ?? transform.position);
    }

    private struct Queued { public SpawnEntry Entry; public int Index; }

    /// <summary>Kolejka spawnu: wpisy przeplatane (slime, pająk, strzelec, slime, ...), żeby strumień mieszał typy.</summary>
    private List<Queued> BuildQueue()
    {
        var queue = new List<Queued>();
        int max = 0;
        foreach (var entry in entries)
            if (entry != null && entry.EnemyPrefab != null) max = Mathf.Max(max, entry.Count);
        for (int i = 0; i < max; i++)
            foreach (var entry in entries)
                if (entry != null && entry.EnemyPrefab != null && i < entry.Count)
                    queue.Add(new Queued { Entry = entry, Index = i });
        return queue;
    }

    private static Transform FindPlayer()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private void SpawnFromQueue(List<Queued> queue, int count, bool stream, Transform player)
    {
        for (int n = 0; n < count && queue.Count > 0; n++)
        {
            var q = queue[0];
            queue.RemoveAt(0);
            Spawn(q.Entry.EnemyPrefab, PositionFor(q.Entry, q.Index, stream, player));
        }
    }

    private Vector3 PositionFor(SpawnEntry entry, int i, bool stream, Transform player)
    {
        Vector3 center = entry.SpawnPoint != null ? entry.SpawnPoint.position : transform.position;
        // pająk i boss wchodzą ze ściany/własnym skokiem: ich punktu nie przenosimy
        bool movable = entry.EnemyPrefab.GetComponent<SpiderController>() == null
            && entry.EnemyPrefab.GetComponent<SpiderMotherBoss>() == null;
        if (movable && player != null)
            center = stream ? AroundPlayer(player.position) : HiddenPointNear(center, player.position);

        Vector3 position = center;
        if (!stream && entry.Count > 1)
        {
            // hordy: promień rośnie z pierwiastkiem liczebności, co drugi wróg na wewnętrznym pierścieniu
            float radius = spreadRadius * Mathf.Max(1f, Mathf.Sqrt(entry.Count / 3f)) * (i % 2 == 0 ? 1f : 0.55f);
            float angle = i * Mathf.PI * 2f / entry.Count + Random.Range(-0.2f, 0.2f);
            position += new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        }
        else if (stream)
        {
            position += new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
        }
        return position;
    }

    /// <summary>Punkt NavMesha 6–11 m od gracza, najlepiej niewidoczny (zza rogu); w ostateczności punkt strefy.</summary>
    private Vector3 AroundPlayer(Vector3 playerPosition)
    {
        Vector3? visibleFallback = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(streamRadiusMin, streamRadiusMax);
            Vector3 candidate = playerPosition + new Vector3(dir.x, 0f, dir.y) * r;
            if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                continue;
            bool visible = !Physics.Linecast(hit.position + Vector3.up * 0.6f, playerPosition + Vector3.up * 0.6f, ~(1 << 6), QueryTriggerInteraction.Ignore);
            if (!visible) return hit.position;
            visibleFallback ??= hit.position;
        }
        return visibleFallback ?? transform.position;
    }

    private Vector3 HiddenPointNear(Vector3 point, Vector3 playerPosition)
    {
        if (!IsVisible(point, playerPosition))
            return point;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 candidate = point + new Vector3(dir.x, 0f, dir.y) * Random.Range(3f, HideSearchRadius);
            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)
                && (hit.position - point).sqrMagnitude <= HideSearchRadius * HideSearchRadius
                && !IsVisible(hit.position, playerPosition))
                return hit.position;
        }
        return point;
    }

    private static bool IsVisible(Vector3 point, Vector3 playerPosition)
    {
        Vector3 from = playerPosition + Vector3.up;
        Vector3 to = point + Vector3.up;
        int mask = ~(1 << 6);
        return !Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore);
    }

    private void Spawn(GameObject prefab, Vector3 position)
    {
        // Bez rodzica: prefab poziomu może być skalowany (NavMeshAgent źle znosi skalowaną hierarchię).
        // Sprzątanie przy zmianie poziomu robi OnDestroy strefy.
        // Rozsunięcie wokół punktu może wypaść poza krawędź NavMesha: dociągnij do najbliższego punktu na siatce.
        if (NavMesh.SamplePosition(position, out var navHit, 2.5f, NavMesh.AllAreas))
            position = navHit.position;
        var instance = Instantiate(prefab, position, Quaternion.identity);
        spawnedObjects.Add(instance);
        if (instance.TryGetComponent<NavMeshAgent>(out var agent))
        {
            agent.enabled = false;
            instance.transform.position = position;
            agent.enabled = true;
            agent.Warp(position);
        }

        if (instance.TryGetComponent<EnemyHealth>(out var enemy))
        {
            if (instance.GetComponent<SpawnEntrance>() == null)
                instance.AddComponent<SpawnEntrance>();
            aliveEnemies.Add(enemy);
            OnEnemySpawned?.Invoke(this, enemy);
        }
        else
        {
            Debug.LogWarning($"Prefab {prefab.name} nie ma EnemyHealth, strefa {name} go nie policzy.", this);
        }
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (!IsTriggered || IsCleared || !aliveEnemies.Remove(enemy))
            return;

        lastDeathPosition = enemy.transform.position;
        // w trakcie spawnu (przed drugim pulsem) pusta lista nie znaczy czystej strefy
        if (aliveEnemies.Count == 0 && !pendingPulse)
            Clear(enemy.transform.position);
    }

    private void Clear(Vector3 lastEnemyPosition)
    {
        IsCleared = true;
        if (heartPrefab != null)
            spawnedObjects.Add(Instantiate(heartPrefab, lastEnemyPosition + Vector3.up * 0.5f, Quaternion.identity));
        GameEvents.RaiseWaveCleared(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        if (TryGetComponent<BoxCollider>(out var box))
        {
            var old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = old;
        }
        else if (TryGetComponent<SphereCollider>(out var sphere))
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
        }

        if (entries == null)
            return;
        Gizmos.color = Color.red;
        foreach (var entry in entries)
        {
            if (entry == null || entry.SpawnPoint == null)
                continue;
            Gizmos.DrawWireSphere(entry.SpawnPoint.position, entry.Count > 1 ? spreadRadius : 0.3f);
            Gizmos.DrawLine(transform.position, entry.SpawnPoint.position);
        }
    }
}
