using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Automatyczne rozstawienie poziomów 2–8 (i opcjonalnie 1) na podstawie upieczonego NavMesha:
/// wejście = punkt NavMesha najbliżej kierunku +X od środka, krata = najdalszy osiągalny punkt
/// (długość ścieżki), strefy fal w 1/3 i 2/3 (poziomy 1–2: 2 strefy, 3–8: 3 strefy) na ścieżce,
/// punkty spawnu na NavMeshu obok strefy, skład fal z dokumentu projektowego §8.1.
/// To rozstawienie testowe, do poprawy przez level designera.
///   Unity.exe -batchmode -quit -projectPath . -executeMethod P1Wiring_Levels.Run
/// </summary>
public static class P1Wiring_Levels
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string SlimePath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string SpiderPath = "Assets/Prefabs/Enemies/Spider.prefab";
    const string ShooterPath = "Assets/Prefabs/Enemies/Shooter.prefab";
    const string BossPath = "Assets/Prefabs/Enemies/Boss Spider Mother.prefab";
    const string HeartPath = "Assets/Prefabs/Pickups/Heart.prefab";

    // Reguły rozstawienia (zadanie "jakość poziomów")
    const float ZoneProbeRadius = 6f;       // okrąg 12 punktów wokół kandydata
    const float ZoneSearchRange = 12f;      // ± po ścieżce od pozycji życzeniowej
    const float MinZoneEdge = 3f;           // krawędź NavMesha >= 3 m od środka strefy
    const float MinZoneGap = 20f;           // odstęp między strefami (po ścieżce)
    const float MinZoneFromEntry = 14f;     // strefa od wejścia
    const float MinZoneFromExit = 8f;       // strefa od kraty
    const float MinSpawnEdge = 1.5f;        // spawn od krawędzi NavMesha
    const float MinSpawnFromEntry = 6f;     // spawn od wejścia

    // poziom -> (slime, pająk, strzelec), dokument §8.1
    static readonly Dictionary<int, (int slime, int spider, int shooter)> Composition = new Dictionary<int, (int, int, int)>
    {
        // hordy (14 października 2026, po playteście: "mają być masy"); to samo w P6Wiring_Balance.Composition
        { 1, (24, 2, 0) }, { 2, (32, 0, 0) }, { 3, (30, 9, 0) }, { 4, (33, 12, 0) },
        { 5, (30, 12, 6) }, { 6, (33, 15, 6) }, { 7, (36, 18, 9) }, { 8, (36, 18, 9) },
    };

    /// <summary>Bootstrap: rozstawia tylko poziomy, które nie mają jeszcze żadnej WaveZone (bezpieczne w WireAll).</summary>
    [MenuItem("LD57/Wire P1 Levels 2-8 (tylko nierozstawione)")]
    public static void Run() => Run(false, false);

    /// <summary>Wymuszone: nadpisuje rozstawienie poziomów 2–8, także ręczne.</summary>
    [MenuItem("LD57/Wire P1 Levels 2-8 (NADPISZ rozstawienie)")]
    public static void RunForce() => Run(false, true);

    [MenuItem("LD57/Wire P1 Levels 1-8 (NADPISZ, z poziomem 1)")]
    public static void RunAll() => Run(true, true);

    public static void Run(bool includeLevel1) => Run(includeLevel1, false);

    public static void Run(bool includeLevel1, bool force)
    {
        for (int level = includeLevel1 ? 1 : 2; level <= 8; level++)
        {
            if (!force)
            {
                var existing = Load($"{LevelsFolder}/Level {level}.prefab");
                if (existing != null && existing.GetComponentInChildren<WaveZone>(true) != null)
                {
                    Debug.Log($"[P1Levels] Level {level}: ma już strefy, pomijam (użyj wariantu NADPISZ, żeby rozstawić od nowa)");
                    continue;
                }
            }
            WireLevel(level);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P1Levels] done");
    }

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static void WireLevel(int level)
    {
        var path = $"{LevelsFolder}/Level {level}.prefab";
        var prefab = Load(path);
        if (prefab == null) { Debug.LogWarning($"[P1Levels] brak {path}"); return; }

        // 1. Geometria liczona w tymczasowej scenie z prawdziwą instancją poziomu: NavMeshSurface ładuje
        //    swoje dane dokładnie tak jak w grze (AddNavMeshData w trybie prefabu dawało inną siatkę).
        var instance = OpenLevelInTempScene(prefab, level, out var previousScene);
        if (instance == null) return;

        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length < 3)
        {
            Debug.LogWarning($"[P1Levels] Level {level}: pusta triangulacja NavMesha");
            RestoreScene(previousScene);
            return;
        }
        var verts = tri.vertices.Distinct().ToList();
        var center = verts.Aggregate(Vector3.zero, (a, v) => a + v) / verts.Count;
        Debug.Log($"[P1Levels] Level {level}: {verts.Count} wierzchołków NavMesha, środek {center}");

        Vector3 entry = verts.OrderByDescending(v => Vector3.Dot((v - center).normalized, Vector3.right)).First();
        entry = Sample(entry, 4f);

        var candidates = verts.Where((v, i) => i % Mathf.Max(1, verts.Count / 96) == 0).ToList();
        float bestLen = -1; Vector3 exit = entry; NavMeshPath bestPath = null;
        foreach (var c in candidates)
        {
            var p = new NavMeshPath();
            if (!NavMesh.CalculatePath(entry, c, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete) continue;
            float len = PathLength(p.corners);
            if (len > bestLen) { bestLen = len; exit = c; bestPath = p; }
        }
        if (bestPath == null)
        {
            Debug.LogWarning($"[P1Levels] Level {level}: brak kompletnej ścieżki z wejścia, pomijam");
            RestoreScene(previousScene);
            return;
        }
        Debug.Log($"[P1Levels] Level {level}: wejście {entry}, krata {exit}, ścieżka {bestLen:F0} m");

        int zoneCount = level <= 2 ? 2 : 3;
        var corners = bestPath.corners;
        var comp = Composition[level];
        var perZone = Distribute(comp, zoneCount);

        // Wszystkie pozycje policzone teraz, na żywym NavMeshu.
        var layout = new LevelLayout
        {
            Entry = entry + Vector3.up * 0.5f,
            EntryForward = Flat(PointAlong(corners, bestLen * 0.05f) - entry),
            Exit = exit,
            ExitForward = Flat(exit - PointAlong(corners, Mathf.Max(0, bestLen - 4f))),
        };
        // Pierwsza strefa tuż za wejściem (gracz ma spotkać wrogów po kilku krokach), ostatnia ~10 m przed kratą,
        // reszta równo pomiędzy. To pozycje "życzeniowe": każdą przesuwamy po ścieżce (±ZoneSearchRange) do sali,
        // czyli miejsca z największą liczbą punktów NavMesha wokół i krawędzią >= MinZoneEdge.
        // Trigger strefy ma co najmniej 6 m, a pierwsza strefa >= 14 m od wejścia, żeby fala nie odpalała
        // się w chwili spawnu gracza (tak było na jamowej mapie, poziom 1).
        float firstDist = Mathf.Clamp(bestLen * 0.12f, MinZoneFromEntry, 25f);
        float lastDist = Mathf.Max(firstDist + 8f, bestLen - 10f);
        var chosen = new List<float>();
        for (int z = 0; z < zoneCount; z++)
        {
            float want = zoneCount == 1 ? firstDist : Mathf.Lerp(firstDist, lastDist, z / (float)(zoneCount - 1));
            float lo = Mathf.Max(MinZoneFromEntry, chosen.Count > 0 ? chosen[chosen.Count - 1] + MinZoneGap : 0f);
            float hi = bestLen - MinZoneFromExit - MinZoneGap * (zoneCount - 1 - z);
            float dist = BestZoneDist(corners, want, lo, hi, out int pts, out float edgeDist);
            chosen.Add(dist);
            Debug.Log($"[P1Levels] Level {level} strefa {z + 1}: życzenie {want:F1} m -> {dist:F1} m po ścieżce, siatka {pts}/12, krawędź {edgeDist:F1} m");

            var zonePos = PointAlong(corners, dist);
            var dir = Flat(PointAlong(corners, Mathf.Min(bestLen, dist + 2f)) - PointAlong(corners, Mathf.Max(0f, dist - 2f)));
            if (dir.sqrMagnitude < 0.001f) dir = Flat(PointAlong(corners, Mathf.Min(bestLen, dist + 3f)) - zonePos);
            var side = Vector3.Cross(Vector3.up, dir.normalized);
            // box = szerokość NavMesha w osi side (6–14 m) x 4 x głębokość w osi dir (6–10 m)
            float width = EdgeReach(zonePos, side, 7f) + EdgeReach(zonePos, -side, 7f);
            float depth = EdgeReach(zonePos, dir.normalized, 5f) + EdgeReach(zonePos, -dir.normalized, 5f);
            var zl = new ZoneLayout
            {
                Position = zonePos, Forward = dir,
                BoxSize = new Vector3(Mathf.Clamp(width, 6f, 14f), 4f, Mathf.Clamp(depth, 6f, 10f)),
            };
            var (sl, sp, sh) = perZone[z];
            var ahead1 = SpawnOnPath(corners, dist + 6f, Vector3.zero, entry);
            var ahead2 = SpiderOnWall(SpawnOnPath(corners, dist + 9f, side * 3f, entry), entry, instance);
            var ahead3 = SpawnOnPath(corners, dist + 9f, -side * 3f, entry);
            if (sl > 0) zl.Entries.Add((SlimePath, ahead1, sl));
            if (sp > 0) zl.Entries.Add((SpiderPath, ahead2, sp));
            if (sh > 0) zl.Entries.Add((ShooterPath, ahead3, sh));
            if (level == 8 && z == zoneCount - 1)
                zl.Entries.Add((BossPath, SpawnOnPath(corners, bestLen - 6f, Vector3.zero, entry), 1));
            layout.Zones.Add(zl);
        }
        // Landmarki (hero propy biomu przy wejściu, strefach i kracie): liczone tu, na żywym NavMeshu.
        layout.Landmarks = PlanLandmarks(level, corners, entry, layout.EntryForward, exit, layout.ExitForward,
            layout.Zones.Select(zl => (zl.Position, zl.Forward)).ToList(), layout.Zones.SelectMany(zl => zl.Entries.Select(en => en.pos)).ToList());
        RestoreScene(previousScene);

        // 2. Zapis do prefabu.
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.localScale = Vector3.one;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            var tracker = GetOrAdd<LevelEnemyTracker>(root);
            SetInt(tracker, "levelNumber", level);

            var entryGo = GetOrCreateChild(root, "Level Entry");
            GetOrAdd<LevelEntry>(entryGo);
            entryGo.transform.position = layout.Entry;
            if (layout.EntryForward.sqrMagnitude > 0.001f) entryGo.transform.rotation = Quaternion.LookRotation(layout.EntryForward);

            foreach (var old in root.GetComponentsInChildren<WaveZone>(true).Where(z => !z.name.StartsWith("Wave Zone ") || ZoneIndex(z.name) > zoneCount).ToList())
                Object.DestroyImmediate(old.gameObject);

            var heart = Load(HeartPath);
            var slime = Load(SlimePath);
            for (int z = 0; z < layout.Zones.Count; z++)
            {
                var zl = layout.Zones[z];
                var zone = GetOrCreateChild(root, $"Wave Zone {z + 1}");
                zone.transform.position = zl.Position;
                zone.transform.rotation = zl.Forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(zl.Forward) : Quaternion.identity;
                var box = GetOrAdd<BoxCollider>(zone);
                box.isTrigger = true;
                box.size = zl.BoxSize;
                box.center = new Vector3(0, 2, 0);
                var wz = GetOrAdd<WaveZone>(zone);
                for (int i = zone.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(zone.transform.GetChild(i).gameObject);
                var so = new SerializedObject(wz);
                var arr = so.FindProperty("entries");
                arr.arraySize = zl.Entries.Count;
                for (int i = 0; i < zl.Entries.Count; i++)
                {
                    var (prefabPath, pos, count) = zl.Entries[i];
                    var enemyPrefab = Load(prefabPath);
                    if (enemyPrefab == null) { Debug.LogWarning($"[P1Levels] brak {prefabPath}, używam slime'a"); enemyPrefab = slime; }
                    var spawn = new GameObject("Spawn " + (char)('A' + i));
                    spawn.transform.SetParent(zone.transform, false);
                    spawn.transform.position = pos;
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("EnemyPrefab").objectReferenceValue = enemyPrefab;
                    e.FindPropertyRelative("SpawnPoint").objectReferenceValue = spawn.transform;
                    e.FindPropertyRelative("Count").intValue = count;
                }
                so.FindProperty("heartPrefab").objectReferenceValue = heart;
                so.FindProperty("spreadRadius").floatValue = 0.8f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var gate = GetOrCreateChild(root, "Exit Gate");
            gate.transform.position = layout.Exit;
            gate.transform.rotation = layout.ExitForward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(layout.ExitForward) : Quaternion.identity;
            var trigger = GetOrAdd<BoxCollider>(gate);
            trigger.isTrigger = true;
            trigger.size = new Vector3(6, 4, 3);
            trigger.center = new Vector3(0, 2, 0);
            var barsT = gate.transform.Find("Bars");
            var bars = barsT != null ? barsT.gameObject : null;
            if (bars == null)
            {
                bars = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bars.name = "Bars";
                bars.transform.SetParent(gate.transform, false);
            }
            bars.transform.localPosition = new Vector3(0, 2, -2.2f);
            bars.transform.localScale = new Vector3(6, 4, 0.3f);
            // słupy portalu po bokach kraty (cylindry 0,15 x 2,5, ciemne, bez colliderów)
            var pillarMat = EnsureMaterial("Gate Pillar", new Color(0.09f, 0.08f, 0.1f), null, 0.15f);
            foreach (var (pname, px) in new[] { ("Gate Pillar L", -3.2f), ("Gate Pillar R", 3.2f) })
            {
                var old = gate.transform.Find(pname);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var pillar = ColliderlessPrimitive(PrimitiveType.Cylinder, pname, gate.transform, pillarMat);
                pillar.transform.localPosition = new Vector3(px, 1.25f, -2.2f);
                pillar.transform.localScale = new Vector3(0.3f, 1.25f, 0.3f);
            }
            var exitGate = GetOrAdd<ExitGate>(gate);
            SetRef(exitGate, "closedVisual", bars);
            SetRef(exitGate, "blocking", bars.GetComponent<BoxCollider>());
            SetRef(exitGate, "trigger", trigger);

            ApplyLandmarks(root, layout.Landmarks);
            Debug.Log($"[P1Levels] Level {level}: {LandmarkSummary(layout.Landmarks)}");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[P1Levels] Level {level}: {zoneCount} strefy, skład slime {comp.slime} pająk {comp.spider} strzelec {comp.shooter}, zapisane");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    class ZoneLayout
    {
        public Vector3 Position, Forward;
        public Vector3 BoxSize = new Vector3(12, 4, 8);
        public List<(string prefabPath, Vector3 pos, int count)> Entries = new List<(string, Vector3, int)>();
    }

    class LevelLayout
    {
        public Vector3 Entry, EntryForward, Exit, ExitForward;
        public List<ZoneLayout> Zones = new List<ZoneLayout>();
        public List<LandmarkSpot> Landmarks = new List<LandmarkSpot>();
    }

    static void Restore(string scenePath)
    {
        if (!string.IsNullOrEmpty(scenePath) && System.IO.File.Exists(scenePath))
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>Punkt na ścieżce + offset, dociągnięty do NavMesha; gdy się nie da, próbuje bliżej ścieżki i z drugiej strony.</summary>
    static Vector3 SampleOnPath(Vector3[] corners, float distance, Vector3 offset)
    {
        var basePoint = PointAlong(corners, Mathf.Clamp(distance, 0, PathLength(corners)));
        foreach (var candidate in new[] { basePoint + offset, basePoint + offset * 0.5f, basePoint, basePoint - offset })
            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                return hit.position;
        return basePoint;
    }

    // ---------------------------------------------------------------- tymczasowa scena i zapytania o geometrię

    /// <summary>
    /// Otwiera pustą scenę z instancją prefabu poziomu (skala 1, w zerze) i włączonym NavMeshem poziomu.
    /// Zwraca null (i przywraca scenę), gdy poziom nie ma NavMeshSurface z danymi. Wołający musi zawołać RestoreScene.
    /// </summary>
    public static GameObject OpenLevelInTempScene(GameObject prefab, int level, out string previousScene)
    {
        previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        var surface = instance.GetComponentInChildren<NavMeshSurface>(true);
        if (surface == null || surface.navMeshData == null)
        {
            Debug.LogWarning($"[P1Levels] Level {level}: brak NavMeshSurface z danymi, pomijam");
            RestoreScene(previousScene);
            return null;
        }
        surface.enabled = false;
        surface.enabled = true; // OnEnable dodaje navMeshData do systemu nawigacji
        Physics.SyncTransforms();
        return instance;
    }

    public static void RestoreScene(string scenePath) => Restore(scenePath);

    /// <summary>Najbliższa ściana poziomu w kierunku poziomym (collidery poziomu, bez triggerów i kraty).</summary>
    public static bool WallHit(GameObject levelRoot, Vector3 origin, Vector3 dir, float maxDistance, out RaycastHit best)
    {
        best = default;
        float bestD = float.MaxValue;
        // Jamowa jaskinia ma normalne na zewnątrz: od środka ściany są tylnymi ściankami, więc włączamy ich trafianie.
        bool prevBack = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        var hits = Physics.RaycastAll(origin, dir, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        Physics.queriesHitBackfaces = prevBack;
        foreach (var h in hits)
        {
            if (h.collider == null || !h.collider.transform.IsChildOf(levelRoot.transform)) continue;
            if (h.collider.GetComponentInParent<ExitGate>() != null) continue;
            if (Mathf.Abs(h.normal.y) > 0.6f) continue; // podłoga/sufit to nie ściana
            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return bestD < float.MaxValue;
    }

    /// <summary>Najbliższa ściana z 8 kierunków poziomych w promieniu maxDistance. dir = kierunek do ściany.</summary>
    public static bool NearestWall(GameObject levelRoot, Vector3 floorPoint, float maxDistance, out RaycastHit hit, out Vector3 dir)
    {
        hit = default; dir = Vector3.forward;
        bool found = false;
        float bestD = float.MaxValue;
        var origin = floorPoint + Vector3.up * 1f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (WallHit(levelRoot, origin, d, maxDistance, out var h) && h.distance < bestD)
            { bestD = h.distance; hit = h; dir = d; found = true; }
        }
        return found;
    }

    static float EdgeReach(Vector3 p, Vector3 dir, float max)
        => NavMesh.Raycast(p, p + dir * max, out var h, NavMesh.AllAreas) ? h.distance : max;

    /// <summary>12 punktów na okręgu 6 m, które leżą na NavMeshu (tol. 0,5 m), i odległość do najbliższej krawędzi.</summary>
    static void EvalSpot(Vector3 c, out int pts, out float edge)
    {
        pts = 0;
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            var q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ZoneProbeRadius;
            if (NavMesh.SamplePosition(q, out _, 0.5f, NavMesh.AllAreas)) pts++;
        }
        edge = NavMesh.FindClosestEdge(c, out var eh, NavMesh.AllAreas) ? eh.distance : 0f;
    }

    /// <summary>
    /// Najlepsze miejsce strefy w oknie want ± ZoneSearchRange (ograniczonym do [lo, hi]) co 1 m:
    /// najpierw krawędź >= MinZoneEdge, potem najwięcej punktów siatki, potem najbliżej życzenia.
    /// </summary>
    static float BestZoneDist(Vector3[] corners, float want, float lo, float hi, out int bestPts, out float bestEdge)
    {
        float a = Mathf.Max(lo, want - ZoneSearchRange), b = Mathf.Min(hi, want + ZoneSearchRange);
        if (a > b) a = b = Mathf.Clamp(want, Mathf.Min(lo, hi), Mathf.Max(lo, hi));
        float best = a, bestScore = float.MinValue;
        bestPts = 0; bestEdge = 0f;
        for (float d = a; d <= b + 0.01f; d += 1f)
        {
            EvalSpot(PointAlong(corners, d), out int pts, out float edge);
            float score = (edge >= MinZoneEdge ? 1000f : 0f) + pts * 10f + Mathf.Min(edge, 6f) - Mathf.Abs(d - want) * 0.1f;
            if (score > bestScore) { bestScore = score; best = d; bestPts = pts; bestEdge = edge; }
        }
        return best;
    }

    /// <summary>Punkt spawnu na ścieżce: >= MinSpawnEdge od krawędzi NavMesha, >= MinSpawnFromEntry od wejścia.</summary>
    static Vector3 SpawnOnPath(Vector3[] corners, float distance, Vector3 offset, Vector3 entry)
    {
        var p = AwayFromEdge(SampleOnPath(corners, distance, offset));
        for (int i = 0; i < 6 && Flat(p - entry).magnitude < MinSpawnFromEntry; i++)
        {
            distance += 3f;
            p = AwayFromEdge(SampleOnPath(corners, distance, offset));
        }
        return p;
    }

    static Vector3 AwayFromEdge(Vector3 p)
    {
        for (int i = 0; i < 4; i++)
        {
            if (!NavMesh.FindClosestEdge(p, out var eh, NavMesh.AllAreas) || eh.distance >= MinSpawnEdge) break;
            var away = Flat(p - eh.position);
            if (away.sqrMagnitude < 1e-4f) away = Flat(eh.normal);
            if (away.sqrMagnitude < 1e-4f) break;
            var q = p + away.normalized * (MinSpawnEdge - eh.distance + 0.2f);
            if (!NavMesh.SamplePosition(q, out var h, 1f, NavMesh.AllAreas)) break;
            p = h.position;
        }
        return p;
    }

    /// <summary>Punkt pająka: 0,5 m od najbliższej ściany (8 kierunków, do 5 m); brak ściany = punkt bez zmian. Wyjątek od reguły krawędzi.</summary>
    static Vector3 SpiderOnWall(Vector3 p, Vector3 entry, GameObject levelRoot)
    {
        if (!NearestWall(levelRoot, p, 5f, out var hit, out var dir)) return p;
        var q = hit.point - dir * 0.5f;
        q.y = p.y;
        if (!NavMesh.SamplePosition(q, out var h, 1f, NavMesh.AllAreas)) return p;
        return Flat(h.position - entry).magnitude < MinSpawnFromEntry ? p : h.position;
    }

    /// <summary>Materiał URP/Lit zapisany jako asset (idempotentnie), do propów w prefabach poziomów.</summary>
    public static Material EnsureMaterial(string name, Color color, Color? emission = null, float smoothness = 0.1f)
    {
        const string folder = "Assets/Prefabs/Levels/Generated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Levels", "Generated");
        var path = $"{folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Primitive bez collidera, jako dziecko parent.</summary>
    public static GameObject ColliderlessPrimitive(PrimitiveType type, string name, Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // ---------------------------------------------------------------- landmarki (runda 12): Landmark N + LandmarkMarker

    public struct LandmarkSpot { public LandmarkKind Kind; public Vector3 Pos; }

    const float LandmarkMinPath = 2.5f;     // landmark nie stoi w korytarzu ścieżki
    const float LandmarkMinSpawn = 2f;      // ani na punkcie spawnu
    const float LandmarkGoodGap = 11f;      // docelowy odstęp między landmarkami (= odstęp klastrów poziomów 5-8)

    /// <summary>
    /// Landmarki: wejście i krata 4 m w bok od osi (obie strony i kilka przesunięć, wygrywa najlepszy wynik), strefy na krawędzi sali
    /// 4-6 m od środka (16 kierunków; krawędź NavMesha 0,8 m za landmarkiem). Wszystko dociągnięte do NavMesha, >= 2,5 m od osi ścieżki,
    /// >= 2 m od spawnów, a odstępy między landmarkami maksymalizowane (cel 11 m). Kolejność: wejście, strefy, krata.
    /// </summary>
    public static List<LandmarkSpot> PlanLandmarks(int level, Vector3[] corners, Vector3 entry, Vector3 entryForward, Vector3 exit, Vector3 exitForward,
        IList<(Vector3 pos, Vector3 forward)> zones, IList<Vector3> spawns)
    {
        var spots = new List<LandmarkSpot>();
        var taken = new List<Vector3>();
        float minGap = DecorLayout.MinClusterSpacing(level);   // landmarki są klastrami: odstęp 7 / 9 / 11 m jak między klastrami
        PlanSideLandmark(spots, taken, minGap, LandmarkKind.Entry, entry, entryForward, corners, spawns, corners.Length >= 2 ? Flat(corners[1] - corners[0]) : Vector3.forward);
        if (zones != null)
            foreach (var z in zones) PlanZoneLandmark(spots, taken, minGap, z.pos, z.forward, corners, spawns);
        PlanSideLandmark(spots, taken, minGap, LandmarkKind.Gate, exit, exitForward, corners, spawns, corners.Length >= 2 ? Flat(corners[corners.Length - 1] - corners[corners.Length - 2]) : Vector3.forward);
        return spots;
    }

    static float GapTo(List<Vector3> taken, Vector3 p)
    {
        float best = 99f;
        foreach (var t in taken) best = Mathf.Min(best, Flat(t - p).magnitude);
        return best;
    }

    static bool LandmarkOk(Vector3 want, Vector3[] corners, IList<Vector3> spawns, out Vector3 snapped)
    {
        snapped = want;
        if (!NavMesh.SamplePosition(want, out var h, 1.5f, NavMesh.AllAreas)) return false;
        if (Flat(h.position - want).magnitude > 1.5f) return false;
        if (DecorLayout.PathDistance(corners, h.position) < LandmarkMinPath) return false;
        if (spawns != null) foreach (var s in spawns) if (Flat(s - h.position).magnitude < LandmarkMinSpawn) return false;
        snapped = h.position;
        return true;
    }

    static void PlanSideLandmark(List<LandmarkSpot> spots, List<Vector3> taken, float minGap, LandmarkKind kind, Vector3 center, Vector3 forward,
        Vector3[] corners, IList<Vector3> spawns, Vector3 pathDir)
    {
        var tangent = Flat(forward);
        if (tangent.sqrMagnitude < 0.01f) tangent = pathDir;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.forward;
        tangent.Normalize();
        var side = Vector3.Cross(Vector3.up, tangent);
        float bestScore = float.MinValue; Vector3 best = default; bool found = false;
        foreach (float lat in new[] { 4f, 5f, 3.2f })
            foreach (float along in new[] { 0f, -3f, 3f, -6f, 6f, -9f, -12f })
                foreach (int sgn in new[] { -1, 1 })
                {
                    var want = center + side * (lat * sgn) + tangent * along;
                    if (!LandmarkOk(want, corners, spawns, out var snapped)) continue;
                    float gap = GapTo(taken, snapped);
                    float score = (gap >= minGap ? 1000f : 0f) - Mathf.Abs(lat - 4f) * 2f - Mathf.Abs(along) * 0.5f + Mathf.Min(gap, LandmarkGoodGap);
                    if (score > bestScore) { bestScore = score; best = snapped; found = true; }
                }
        if (!found) { Debug.LogWarning($"[P1Levels] landmark {kind}: brak miejsca 3-5 m od osi, pomijam"); return; }
        if (GapTo(taken, best) < minGap) Debug.LogWarning($"[P1Levels] landmark {kind}: najlepsze miejsce {GapTo(taken, best):F1} m od innego landmarku (cel {minGap:F0} m)");
        spots.Add(new LandmarkSpot { Kind = kind, Pos = best });
        taken.Add(best);
    }

    static void PlanZoneLandmark(List<LandmarkSpot> spots, List<Vector3> taken, float minGap, Vector3 center, Vector3 forward, Vector3[] corners, IList<Vector3> spawns)
    {
        var fwd = Flat(forward);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        float bestScore = float.MinValue; Vector3 best = default; bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float reach = EdgeReach(center, dir, 9f);
            float d = Mathf.Min(reach - 0.8f, 6f);                       // 0,8 m od krawędzi, nie dalej niż 6 m od środka
            if (d < 2.5f) continue;
            if (!LandmarkOk(center + dir * d, corners, spawns, out var snapped)) continue;
            float dist = Flat(snapped - center).magnitude;
            bool inRange = dist >= 4f && dist <= 6.05f;
            float gap = GapTo(taken, snapped);
            float perpendicular = 1f - Mathf.Abs(Vector3.Dot(dir, fwd));   // boki sali zamiast osi ścieżki
            float score = (gap >= minGap ? 1000f : 0f) + (inRange ? 50f : 0f) + (reach <= 6.9f ? 10f : 0f) + perpendicular * 5f + Mathf.Min(gap, LandmarkGoodGap);
            if (score > bestScore) { bestScore = score; best = snapped; found = true; }
        }
        if (!found) { Debug.LogWarning("[P1Levels] landmark strefy: brak krawędzi sali w zasięgu, pomijam"); return; }
        if (GapTo(taken, best) < minGap) Debug.LogWarning($"[P1Levels] landmark strefy: najlepsze miejsce {GapTo(taken, best):F1} m od innego landmarku (cel {minGap:F0} m)");
        spots.Add(new LandmarkSpot { Kind = LandmarkKind.Zone, Pos = best });
        taken.Add(best);
    }

    /// <summary>Kasuje stare `Landmark N` i tworzy nowe (pusty obiekt + LandmarkMarker, pivot na podłodze).</summary>
    public static void ApplyLandmarks(GameObject root, List<LandmarkSpot> spots)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var c = root.transform.GetChild(i);
            if (c.name.StartsWith("Landmark ") && c.GetComponent<LandmarkMarker>() != null) Object.DestroyImmediate(c.gameObject);
        }
        for (int i = 0; i < spots.Count; i++)
        {
            var go = new GameObject($"Landmark {i + 1}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = spots[i].Pos;
            var marker = go.AddComponent<LandmarkMarker>();
            int kind = (int)spots[i].Kind;
            SetProp(marker, "kind", p => p.enumValueIndex = kind);
        }
    }

    /// <summary>Raport landmarków do logu: liczba i najmniejszy odstęp (m).</summary>
    public static string LandmarkSummary(List<LandmarkSpot> spots)
    {
        float min = float.MaxValue;
        for (int i = 0; i < spots.Count; i++)
            for (int j = i + 1; j < spots.Count; j++)
                min = Mathf.Min(min, Flat(spots[i].Pos - spots[j].Pos).magnitude);
        return $"landmarki {spots.Count} ({string.Join(", ", spots.Select(s => s.Kind.ToString()))}), min odstęp {(min == float.MaxValue ? "n/d" : min.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))} m";
    }

    /// <summary>Istniejące `Landmark N` instancji poziomu albo (gdy brak) świeży plan z jej Level Entry / Wave Zone N / Exit Gate. Wołać w tymczasowej scenie.</summary>
    public static List<LandmarkSpot> LandmarksFromInstance(int level, GameObject instance, Vector3[] corners, bool forcePlan, out bool existed)
    {
        existed = false;
        var found = new List<LandmarkSpot>();
        foreach (var m in instance.GetComponentsInChildren<LandmarkMarker>(true)) found.Add(new LandmarkSpot { Kind = m.Kind, Pos = m.transform.position });
        if (found.Count > 0 && !forcePlan) { existed = true; return found; }

        var entryT = instance.transform.Find("Level Entry");
        var gateT = instance.transform.Find("Exit Gate");
        if (entryT == null || gateT == null || corners == null || corners.Length < 2) return new List<LandmarkSpot>();
        var zones = new List<(Vector3 pos, Vector3 forward)>();
        var spawns = new List<Vector3>();
        foreach (var z in instance.GetComponentsInChildren<WaveZone>(true).OrderBy(z => ZoneIndex(z.name)))
        {
            zones.Add((Sample(z.transform.position, 4f), z.transform.forward));
            foreach (Transform c in z.transform) if (c.name.StartsWith("Spawn ")) spawns.Add(c.position);
        }
        return PlanLandmarks(level, corners, Sample(entryT.position, 4f), entryT.forward, Sample(gateT.position, 4f), gateT.forward, zones, spawns);
    }

    /// <summary>Ścieżka wejście-krata istniejącego poziomu w tymczasowej scenie (null, gdy brak).</summary>
    public static Vector3[] PathOfInstance(GameObject instance)
    {
        var entryT = instance.transform.Find("Level Entry");
        var gateT = instance.transform.Find("Exit Gate");
        if (entryT == null || gateT == null) return null;
        if (!NavMesh.SamplePosition(entryT.position, out var a, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(gateT.position, out var b, 4f, NavMesh.AllAreas)) return null;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) return null;
        return path.corners;
    }

    /// <summary>Tylko landmarki, bez ruszania stref: planuje od nowa i nadpisuje `Landmark N` na poziomach 1-8.</summary>
    [MenuItem("LD57/Wire P12 Landmarks (NADPISZ)")]
    public static void RunLandmarks()
    {
        for (int level = 1; level <= 8; level++)
        {
            var path = $"{LevelsFolder}/Level {level}.prefab";
            var prefab = Load(path);
            if (prefab == null) { Debug.LogWarning($"[P1Levels] brak {path}"); continue; }
            var instance = OpenLevelInTempScene(prefab, level, out var previousScene);
            if (instance == null) continue;
            List<LandmarkSpot> spots;
            try { spots = LandmarksFromInstance(level, instance, PathOfInstance(instance), true, out _); }
            finally { RestoreScene(previousScene); }
            if (spots.Count == 0) { Debug.LogWarning($"[P1Levels] Level {level}: brak landmarków (brak ścieżki lub stref)"); continue; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyLandmarks(root, spots);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[P1Levels] Level {level}: {LandmarkSummary(spots)}, zapisane");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------- helpers

    static int ZoneIndex(string name) => int.TryParse(name.Replace("Wave Zone ", ""), out var i) ? i : 99;

    static List<(int, int, int)> Distribute((int slime, int spider, int shooter) c, int zones)
    {
        var result = new List<(int, int, int)>();
        for (int z = 0; z < zones; z++)
            result.Add((Share(c.slime, zones, z), Share(c.spider, zones, z), Share(c.shooter, zones, z)));
        return result;
    }

    // Rozdziel n na `zones` części, reszta idzie do ostatnich stref (fala przy kracie największa).
    static int Share(int n, int zones, int z)
    {
        int basePart = n / zones, rest = n % zones;
        return basePart + (z >= zones - rest ? 1 : 0);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    static Vector3 Sample(Vector3 p, float maxDistance)
        => NavMesh.SamplePosition(p, out var hit, maxDistance, NavMesh.AllAreas) ? hit.position : p;

    public static float PathLength(Vector3[] corners)
    {
        float len = 0;
        for (int i = 1; i < corners.Length; i++) len += Vector3.Distance(corners[i - 1], corners[i]);
        return len;
    }

    public static Vector3 PointAlong(Vector3[] corners, float distance)
    {
        if (corners.Length == 0) return Vector3.zero;
        float acc = 0;
        for (int i = 1; i < corners.Length; i++)
        {
            float seg = Vector3.Distance(corners[i - 1], corners[i]);
            if (acc + seg >= distance)
                return Vector3.Lerp(corners[i - 1], corners[i], seg > 0 ? (distance - acc) / seg : 0);
            acc += seg;
        }
        return corners[corners.Length - 1];
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P1Levels] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object target, string field, Object value) => SetProp(target, field, p => p.objectReferenceValue = value);
    static void SetInt(Object target, string field, int value) => SetProp(target, field, p => p.intValue = value);
}
