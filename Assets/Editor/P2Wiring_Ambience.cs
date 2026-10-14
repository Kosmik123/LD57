using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Klimat poziomow 1-8, swiatlo kraty, napis sterowania i lampa gracza.</summary>
public static class P2Wiring_Ambience
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string PlayerPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("LD57/Wire P2 Ambience")]
    public static void Run()
    {
        for (int level = 1; level <= 8; level++) WireLevel(level);
        WirePlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P2Ambience] done");
    }

    static void WireLevel(int level)
    {
        var path = $"{LevelsFolder}/Level {level}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[P2Ambience] brak {path}"); return; }
        // Pozycje pochodni/grzybów liczone w tymczasowej scenie (NavMesh + collidery), przed otwarciem prefabu do zapisu.
        var lights = PlanWallLights(level);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<LevelAmbience>() == null)
                GetOrAdd<LevelAmbience>(root).ApplyPresetValues(level);
            else if (RefreshUntouchedPreset(root, level))
                Debug.Log($"[P2Ambience] Level {level}: LevelAmbience miał stary, nieedytowany preset; podmieniony na biomowy");
            else
                Debug.Log($"[P2Ambience] Level {level}: LevelAmbience istnieje, wartości bez zmian");

            var gateT = root.transform.Find("Exit Gate");
            if (gateT != null)
            {
                var lightGo = GetOrCreateChild(gateT.gameObject, "Exit Light");
                if (lightGo.transform.localPosition == Vector3.zero)
                    lightGo.transform.localPosition = new Vector3(0, 3, 0);
                var l = GetOrAdd<Light>(lightGo);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.15f);
                l.range = 6f;
                l.intensity = 1.5f;
                l.enabled = false;
                var gate = gateT.GetComponent<ExitGate>();
                if (gate != null) SetRef(gate, "exitLight", l);
                else Debug.LogWarning($"[P2Ambience] Level {level}: brak ExitGate");
            }
            else Debug.LogWarning($"[P2Ambience] Level {level}: brak Exit Gate");

            if (level == 1) WireSign(root);
            BuildWallLights(root, level, lights);
            if (!lights.LandmarksExisted && lights.Landmarks.Count > 0)
            {
                P1Wiring_Levels.ApplyLandmarks(root, lights.Landmarks);   // prefab bez landmarków (levelsforce nie był puszczony): dopisz bez ruszania stref
                Debug.Log($"[P2Ambience] Level {level}: dodano {P1Wiring_Levels.LandmarkSummary(lights.Landmarks)}");
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- pochodnie, kryształy i grzyby (runda 12: mierzalny rozkład)

    struct WallLightSpot { public Vector3 Floor; public Vector3 Wall; public Vector3 Normal; public bool Standing; }

    enum LightKind { Torch, Crystal, Mushroom, LandmarkLight }

    class LightCand
    {
        public WallLightSpot Spot; public LightKind Kind; public int Priority; public int Order;
        public Vector3 Pos => Spot.Floor;
    }

    class LightPlan
    {
        public readonly List<WallLightSpot> Torches = new List<WallLightSpot>();         // poziomy 1-6: słupy po obu stronach ścieżki, na zmianę
        public readonly List<WallLightSpot> Crystals = new List<WallLightSpot>();        // poziomy 7-8: kryształy na zmianę
        public readonly List<WallLightSpot> Mushrooms = new List<WallLightSpot>();       // poziomy 5-6: przy krawędzi/ścianie
        public readonly List<WallLightSpot> LandmarkLights = new List<WallLightSpot>();  // poziomy 5-6: światło kryształów landmarku
        public List<P1Wiring_Levels.LandmarkSpot> Landmarks = new List<P1Wiring_Levels.LandmarkSpot>();
        public bool LandmarksExisted;
    }

    const float TorchSideOffset = 2.5f;   // m od osi ścieżki w bok
    const float AccentSideOffset = 3.1f;  // akcenty parami: 2 x 3,1 = 6,2 m między sobą (>= MinLightGap)
    const float MinLightGap = 6f;         // żadne dwa źródła światła bliżej niż 6 m
    const int MaxLights = 24;             // limit źródeł na poziom (razem z Exit Light)
    const float MinAxisDistance = 1.6f;   // słup nie może stanąć na osi ścieżki
    const int PrioRegular = 1, PrioAccent = 2, PrioLandmark = 3;

    /// <summary>Odstęp świateł po łuku ścieżki (m): 1–2 co 9, 3–4 co 12, 5–6 co 15; 7–8 (kryształy) co 12.</summary>
    static float TorchSpacing(int level) => level <= 2 ? 9f : level <= 4 ? 12f : level <= 6 ? 15f : 12f;

    /// <summary>
    /// Pozycje liczone na żywym NavMeshu i colliderach poziomu w tymczasowej scenie.
    /// Kandydaci (priorytet): landmarki (3: las = 2 pochodnie u stóp, kryształy = światło), akcenty (2: 2 przy wejściu, 2 przy kracie,
    /// po 1 z każdej strony środka strefy), regularne (1: co `TorchSpacing` po łuku, na zmianę lewo/prawo 2,5 m od osi, dociągnięte do NavMesha;
    /// poziomy 5-6 dodatkowo grzyby). Potem deduplikacja (żadne dwa źródła bliżej niż 6 m, wygrywa wyższy priorytet; Exit Light stały)
    /// i przycięcie do 24 źródeł (najpierw regularne o najmniejszym odstępie do sąsiada).
    /// </summary>
    static LightPlan PlanWallLights(int level)
    {
        var plan = new LightPlan();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelsFolder}/Level {level}.prefab");
        var instance = P1Wiring_Levels.OpenLevelInTempScene(prefab, level, out var previousScene);
        if (instance == null) return plan;
        try
        {
            var entryT = instance.transform.Find("Level Entry");
            var gateT = instance.transform.Find("Exit Gate");
            if (entryT == null || gateT == null) { Debug.LogWarning($"[P2Ambience] Level {level}: brak Level Entry/Exit Gate, bez pochodni"); return plan; }
            var corners = P1Wiring_Levels.PathOfInstance(instance);
            if (corners == null) { Debug.LogWarning($"[P2Ambience] Level {level}: brak ścieżki wejście-krata (poza NavMeshem lub niekompletna), bez pochodni"); return plan; }
            float len = P1Wiring_Levels.PathLength(corners);
            float spacing = TorchSpacing(level);
            bool torchLevel = level <= 6;   // 1-6 pochodnie, 7-8 kryształy
            var regularKind = torchLevel ? LightKind.Torch : LightKind.Crystal;

            plan.Landmarks = P1Wiring_Levels.LandmarksFromInstance(level, instance, corners, false, out plan.LandmarksExisted);

            var cands = new List<LightCand>();
            int order = 0, misses = 0;
            void Add(WallLightSpot s, LightKind k, int prio) => cands.Add(new LightCand { Spot = s, Kind = k, Priority = prio, Order = order++ });

            // 1. landmarki: las (1-2) = 2 pochodnie u stóp (po obu stronach wzdłuż ścieżki), kryształy (5-6) = jedno światło
            int biome = level <= 2 ? 0 : level <= 4 ? 1 : level <= 6 ? 2 : 3;
            foreach (var lm in plan.Landmarks)
            {
                if (biome == 0)
                {
                    var tangent = PathTangentNear(corners, lm.Pos);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        if (TrySnap(corners, lm.Pos + tangent * (AccentSideOffset * sgn), out var spot)) Add(spot, LightKind.Torch, PrioLandmark);
                        else misses++;
                    }
                }
                else if (biome == 2)
                    Add(new WallLightSpot { Floor = lm.Pos, Wall = lm.Pos, Normal = Vector3.up, Standing = true }, LightKind.LandmarkLight, PrioLandmark);
            }

            // 2. akcenty: wejście (2), krata (2), strefy (po 1 z każdej strony środka)
            void Pair(Vector3 center, Vector3 tangent, float offset)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (SideSpot(corners, center, tangent, side, offset, out var spot)) Add(spot, regularKind, PrioAccent);
                    else misses++;
                }
            }
            Pair(P1Wiring_Levels.PointAlong(corners, Mathf.Min(1.5f, len)), TangentAt(corners, len, Mathf.Min(1.5f, len)), AccentSideOffset);
            Pair(P1Wiring_Levels.PointAlong(corners, Mathf.Max(0f, len - 6.5f)), TangentAt(corners, len, Mathf.Max(0f, len - 6.5f)), AccentSideOffset);
            foreach (var z in instance.GetComponentsInChildren<WaveZone>(true))
            {
                var zp = z.transform.position;
                if (NavMesh.SamplePosition(zp, out var zh, 4f, NavMesh.AllAreas)) zp = zh.position;
                var zt = z.transform.forward; zt.y = 0f;
                if (zt.sqrMagnitude < 0.01f) zt = PathTangentNear(corners, zp);
                Pair(zp, zt.normalized, AccentSideOffset);
            }

            // 3. regularne: co spacing po łuku, na zmianę lewo/prawo
            int regular = 0;
            for (float d = spacing * 0.5f; d < len - 3f; d += spacing, regular++)
            {
                int side = regular % 2 == 0 ? -1 : 1;
                if (SideSpot(corners, P1Wiring_Levels.PointAlong(corners, d), TangentAt(corners, len, d), side, TorchSideOffset, out var spot)) Add(spot, regularKind, PrioRegular);
                else misses++;
            }

            if (level == 5 || level == 6)
            {
                for (float d = spacing; d < len - 3f; d += spacing)
                {
                    var floor = P1Wiring_Levels.PointAlong(corners, d);
                    if (P1Wiring_Levels.NearestWall(instance, floor, 14f, out var hit, out _))
                        Add(new WallLightSpot { Floor = floor, Wall = hit.point, Normal = hit.normal }, LightKind.Mushroom, PrioRegular);
                    else if (NavMesh.FindClosestEdge(floor, out var edge, NavMesh.AllAreas) && edge.distance > 1f)
                    {
                        var inward = floor - edge.position; inward.y = 0f;
                        inward = inward.sqrMagnitude > 0.001f ? inward.normalized : Vector3.forward;
                        var post = edge.position + inward * 0.6f;
                        Add(new WallLightSpot { Floor = new Vector3(post.x, floor.y, post.z), Wall = post, Normal = inward, Standing = true }, LightKind.Mushroom, PrioRegular);
                    }
                }
            }

            // 4. deduplikacja i limit
            var fixedPts = new List<Vector3> { gateT.position };   // Exit Light stoi na kracie (+3 m w górę)
            int generated = cands.Count;
            var kept = Dedup(cands, fixedPts, out int removed);
            int trimmed = Trim(kept, fixedPts);

            foreach (var c in kept)
            {
                switch (c.Kind)
                {
                    case LightKind.Torch: plan.Torches.Add(c.Spot); break;
                    case LightKind.Crystal: plan.Crystals.Add(c.Spot); break;
                    case LightKind.Mushroom: plan.Mushrooms.Add(c.Spot); break;
                    default: plan.LandmarkLights.Add(c.Spot); break;
                }
            }

            var all = kept.Select(c => c.Pos).Concat(fixedPts).ToList();
            NeighborStats(all, out float minGap, out float meanGap);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log($"[P2Ambience] Level {level}: świateł {all.Count}, min odstęp {minGap.ToString("F1", ci)} m, średni {meanGap.ToString("F1", ci)} m, usunięto {removed} duplikatów" +
                      $" | pochodni {plan.Torches.Count}, kryształów {plan.Crystals.Count}, grzybów {plan.Mushrooms.Count}, świateł landmarków {plan.LandmarkLights.Count}, Exit Light 1;" +
                      $" kandydatów {generated}, przycięto {trimmed} do limitu {MaxLights}, bez NavMesha {misses}, ścieżka {len.ToString("F0", ci)} m, odstęp po łuku {spacing.ToString("F0", ci)} m," +
                      $" landmarki {plan.Landmarks.Count}{(plan.LandmarksExisted ? " (istniejące)" : " (nowe)")}");
        }
        finally { P1Wiring_Levels.RestoreScene(previousScene); }
        return plan;
    }

    /// <summary>Kierunek ścieżki (poziomy, znormalizowany) w punkcie o długości łuku d.</summary>
    static Vector3 TangentAt(Vector3[] corners, float len, float d)
    {
        var ahead = P1Wiring_Levels.PointAlong(corners, Mathf.Min(len, d + 0.75f));
        var behind = P1Wiring_Levels.PointAlong(corners, Mathf.Max(0f, d - 0.75f));
        var t = ahead - behind; t.y = 0f;
        return t.sqrMagnitude < 0.0001f ? Vector3.forward : t.normalized;
    }

    /// <summary>Kierunek najbliższego odcinka ścieżki względem punktu.</summary>
    static Vector3 PathTangentNear(Vector3[] corners, Vector3 p)
    {
        float best = float.MaxValue; var dir = Vector3.forward;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 a = new Vector2(corners[i - 1].x, corners[i - 1].z), b = new Vector2(corners[i].x, corners[i].z), ab = b - a, q = new Vector2(p.x, p.z);
            float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2);
            float dist = Vector2.Distance(q, a + ab * t);
            if (dist < best && l2 > 1e-6f) { best = dist; dir = new Vector3(ab.x, 0f, ab.y).normalized; }
        }
        return dir;
    }

    /// <summary>Punkt `offset` m w bok (side = -1 lewo, +1 prawo) od środka, dociągnięty do NavMesha; zawęża offset, gdy korytarz jest wąski.</summary>
    static bool SideSpot(Vector3[] corners, Vector3 center, Vector3 tangent, int side, float offset, out WallLightSpot spot)
    {
        var left = Vector3.Cross(Vector3.up, tangent);
        for (float off = offset; off >= 1.9f; off -= 0.5f)
            if (TrySnap(corners, center + left * side * off, out spot)) return true;
        spot = default;
        return false;
    }

    static bool TrySnap(Vector3[] corners, Vector3 want, out WallLightSpot spot)
    {
        spot = default;
        if (!NavMesh.SamplePosition(want, out var snap, 3f, NavMesh.AllAreas)) return false;
        if (DecorLayout.PathDistance(corners, snap.position) < MinAxisDistance) return false;   // wąski korytarz: słup nie może stanąć na osi
        spot = new WallLightSpot { Floor = snap.position, Wall = snap.position, Normal = Vector3.up, Standing = true };
        return true;
    }

    static float FlatDist(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

    /// <summary>Greedy: od najwyższego priorytetu (przy równym: kolejność generowania) bierzemy źródło, jeśli jest >= 6 m od wszystkich przyjętych i stałych.</summary>
    static List<LightCand> Dedup(List<LightCand> cands, List<Vector3> fixedPts, out int removed)
    {
        removed = 0;
        var kept = new List<LightCand>();
        foreach (var c in cands.OrderByDescending(x => x.Priority).ThenBy(x => x.Order))
        {
            bool ok = true;
            foreach (var f in fixedPts) if (FlatDist(f, c.Pos) < MinLightGap - 0.001f) { ok = false; break; }
            if (ok) foreach (var k in kept) if (FlatDist(k.Pos, c.Pos) < MinLightGap - 0.001f) { ok = false; break; }
            if (ok) kept.Add(c); else removed++;
        }
        return kept;
    }

    /// <summary>Do limitu 24 źródeł (z Exit Light): kasuje najniższy priorytet, a w nim źródło o najmniejszym odstępie do najbliższego sąsiada.</summary>
    static int Trim(List<LightCand> kept, List<Vector3> fixedPts)
    {
        int trimmed = 0;
        while (kept.Count + fixedPts.Count > MaxLights && kept.Count > 0)
        {
            int minPrio = kept.Min(c => c.Priority);
            LightCand victim = null; float victimNn = float.MaxValue;
            foreach (var c in kept)
            {
                if (c.Priority != minPrio) continue;
                float nn = float.MaxValue;
                foreach (var o in kept) if (o != c) nn = Mathf.Min(nn, FlatDist(o.Pos, c.Pos));
                foreach (var f in fixedPts) nn = Mathf.Min(nn, FlatDist(f, c.Pos));
                if (nn < victimNn) { victimNn = nn; victim = c; }
            }
            if (victim == null) break;
            kept.Remove(victim);
            trimmed++;
        }
        return trimmed;
    }

    /// <summary>Najmniejszy i średni odstęp do najbliższego sąsiada po wszystkich źródłach (0, gdy mniej niż 2).</summary>
    static void NeighborStats(List<Vector3> pts, out float min, out float mean)
    {
        min = 0f; mean = 0f;
        if (pts.Count < 2) return;
        min = float.MaxValue;
        float sum = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            float nn = float.MaxValue;
            for (int j = 0; j < pts.Count; j++) if (i != j) nn = Mathf.Min(nn, FlatDist(pts[i], pts[j]));
            min = Mathf.Min(min, nn);
            sum += nn;
        }
        mean = sum / pts.Count;
    }

    static void BuildWallLights(GameObject root, int level, LightPlan plan)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var c = root.transform.GetChild(i);
            if (c.name.StartsWith("Torch ") || c.name.StartsWith("Mushrooms ") || c.name.StartsWith("Crystal ")) Object.DestroyImmediate(c.gameObject);
        }

        if (plan.Torches.Count > 0)
        {
            var holderMat = P1Wiring_Levels.EnsureMaterial("Torch Holder", new Color(0.25f, 0.14f, 0.07f), null, 0.05f);
            var bowlMat = P1Wiring_Levels.EnsureMaterial("Torch Bowl", new Color(0.1f, 0.08f, 0.07f), null, 0.3f);
            for (int i = 0; i < plan.Torches.Count; i++)
            {
                var s = plan.Torches[i];
                var go = new GameObject($"Torch {i + 1}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(s.Floor.x, s.Floor.y + 1.65f, s.Floor.z);   // wierzch słupa 1,6 m
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.3f);
                l.range = 6f;
                l.intensity = 2f;
                l.shadows = LightShadows.None;   // dużo świateł: bez cieni
                go.AddComponent<LightFlicker>();
                go.AddComponent<Torch>();
                var pole = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Cylinder, "Holder", go.transform, holderMat);
                pole.transform.localPosition = new Vector3(0f, -0.85f, 0f);
                pole.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);   // 1,6 m
                var bowl = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Cylinder, "Bowl", go.transform, bowlMat);
                bowl.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                bowl.transform.localScale = new Vector3(0.24f, 0.05f, 0.24f);
            }
        }

        if (plan.Crystals.Count > 0)
        {
            var rng = new System.Random(level * 104729);
            var crystalMat = P1Wiring_Levels.EnsureMaterial("Crystal Violet", new Color(0.35f, 0.1f, 0.6f), new Color(1.6f, 0.5f, 3.0f), 0.6f);
            for (int i = 0; i < plan.Crystals.Count; i++)
            {
                var s = plan.Crystals[i];
                var go = new GameObject($"Crystal Light {i + 1}");
                go.transform.SetParent(root.transform, false);
                var basePos = s.Floor;
                go.transform.position = basePos + Vector3.up * 0.9f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.65f, 0.3f, 1f);
                l.range = 7f;
                l.intensity = 2f;
                l.shadows = LightShadows.None;
                go.AddComponent<LightFlicker>();
                float[] heights = { 1.5f, 1.05f, 0.8f };
                for (int k = 0; k < 3; k++)
                {
                    var shard = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Cube, $"Shard {k + 1}", go.transform, crystalMat);
                    float h = heights[k] * (0.85f + (float)rng.NextDouble() * 0.3f);
                    var off = new Vector3((float)(rng.NextDouble() - 0.5) * 0.5f, 0f, (float)(rng.NextDouble() - 0.5) * 0.5f);
                    shard.transform.position = basePos + off + Vector3.up * (h * 0.45f);
                    shard.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 24f, (float)rng.NextDouble() * 90f, (float)(rng.NextDouble() - 0.5) * 24f);
                    shard.transform.localScale = new Vector3(0.16f, h, 0.16f);   // wydłużony, fioletowy, emisyjny
                }
            }
        }

        // światło landmarku kryształowego (poziomy 5-6); same kryształy landmarku buduje CaveDecor w runtime
        for (int i = 0; i < plan.LandmarkLights.Count; i++)
        {
            var go = new GameObject($"Crystal Landmark Light {i + 1}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = plan.LandmarkLights[i].Floor + Vector3.up * 2.2f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 0.85f, 1f);
            l.range = 9f;
            l.intensity = 2.5f;
            l.shadows = LightShadows.None;
            go.AddComponent<LightFlicker>();
        }

        if (plan.Mushrooms.Count > 0)
        {
            var rng = new System.Random(level * 7919);
            var shroomMat = P1Wiring_Levels.EnsureMaterial("Mushroom Glow", new Color(0.1f, 0.35f, 0.3f), new Color(0.2f, 1.2f, 0.9f), 0.4f);
            for (int i = 0; i < plan.Mushrooms.Count; i++)
            {
                var s = plan.Mushrooms[i];
                var n = s.Normal; n.y = 0f; n = n.sqrMagnitude > 0.001f ? n.normalized : Vector3.up;
                var go = new GameObject($"Mushrooms {i + 1}");
                go.transform.SetParent(root.transform, false);
                var basePos = new Vector3(s.Wall.x, s.Floor.y, s.Wall.z) + n * 0.3f;
                go.transform.position = basePos + Vector3.up * 0.35f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.5f, 1f, 0.8f);
                l.range = 4f;
                l.intensity = 1.5f;
                l.shadows = LightShadows.None;
                for (int k = 0; k < 3; k++)
                {
                    float diameter = 0.15f + (float)rng.NextDouble() * 0.15f;
                    var off = new Vector3((float)(rng.NextDouble() - 0.5) * 0.5f, 0f, (float)(rng.NextDouble() - 0.5) * 0.5f);
                    var sph = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Sphere, $"Mushroom {k + 1}", go.transform, shroomMat);
                    sph.transform.position = basePos + off + Vector3.up * (diameter * 0.4f);
                    sph.transform.localScale = Vector3.one * diameter;
                }
            }
        }
    }

    // Stare presety ambientu poziomow 1, 2, 7 (przed biomami). Podmieniamy tylko komponenty z nietknietym starym ambientem.
    static readonly Dictionary<int, Color> OldAmbient = new Dictionary<int, Color>
    {
        [1] = new Color(.55f, .45f, .35f), [2] = new Color(.42f, .32f, .24f), [7] = new Color(.02f, .03f, .07f),
    };

    static bool RefreshUntouchedPreset(GameObject root, int level)
    {
        if (!OldAmbient.TryGetValue(level, out var old)) return false;
        var amb = root.GetComponent<LevelAmbience>();
        var so = new SerializedObject(amb);
        var p = so.FindProperty("ambientColor");
        if (p == null) return false;
        var c = p.colorValue;
        if (Mathf.Abs(c.r - old.r) > 0.005f || Mathf.Abs(c.g - old.g) > 0.005f || Mathf.Abs(c.b - old.b) > 0.005f) return false;
        amb.ApplyPresetValues(level);
        EditorUtility.SetDirty(amb);
        return true;
    }

    static void WireSign(GameObject root)
    {
        var entry = root.GetComponentInChildren<LevelEntry>(true);
        if (entry == null) { Debug.LogWarning("[P2Ambience] Level 1: brak Level Entry"); return; }
        var go = GetOrCreateChild(root, "Controls Sign");
        Vector3 fwd = entry.transform.forward; fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        // Wejscie lezy 0,5 m nad NavMeshem (P1Wiring_Levels); napis 2 m przed wejsciem, 0,05 nad podloga.
        Vector3 pos = entry.transform.position + fwd * 2f;
        pos.y = entry.transform.position.y - 0.5f + 0.05f;
        go.transform.position = pos;
        // Napis lezy (X 90), a jego gora wskazuje forward wejscia.
        go.transform.rotation = Quaternion.LookRotation(Vector3.down, fwd);

        var tmp = GetOrAdd<TextMeshPro>(go);
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 3.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(12, 4);
        var sign = GetOrAdd<WorldSign>(go);
        const string text = "WSAD ruch · LPM atak · Shift dash\nQ E R skille · Spacja uderzenie\nZejdź w dół. Nie wracaj.";
        tmp.text = text;
        SetProp(sign, "text", p => p.stringValue = text);
        SetRef(sign, "label", tmp);
    }

    static void WirePlayer()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null) { Debug.LogWarning("[P2Ambience] brak Player.prefab"); return; }
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var lampGo = GetOrCreateChild(root, "Lamp");
            if (lampGo.transform.localPosition == Vector3.zero) lampGo.transform.localPosition = new Vector3(0, 2f, 0);
            var l = GetOrAdd<Light>(lampGo);
            l.type = LightType.Point;
            l.range = 12f;
            l.intensity = 0f;
            l.color = new Color(1f, 0.8f, 0.55f);
            l.enabled = false;
            var lamp = GetOrAdd<PlayerLamp>(lampGo);
            SetRef(lamp, "lampLight", l);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
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
        if (p == null) { Debug.LogError($"[P2Ambience] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object target, string field, Object value) => SetProp(target, field, p => p.objectReferenceValue = value);
}
