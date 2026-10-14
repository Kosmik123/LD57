using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

public enum DecorBiome { Forest, Rock, Crystal, Abyss }

/// <summary>Rodzaje propów podłogowych (kształty w DecorPrimitives.Prop). Wymiary nominalne = największy wariant, skala je zmniejsza.</summary>
public enum PropType
{
    Tree, Bush, Stone, Grass, Pebble,                       // las
    Boulder, DeadTree, Rubble, Stalagmite, StalagmiteSmall, // skała
    CrystalBig, CrystalSmall, CrystalTiny, Mushroom,        // kryształy
    Fang, FangSmall, Cocoon, WebLean,                       // otchłań
    LandmarkTree, Menhir, LandmarkCrystals, LandmarkFang    // landmarki
}

/// <summary>Jeden prop do zbudowania (czyste dane: plan powstaje bez tworzenia obiektów).</summary>
public struct PropPlan
{
    public PropType Type;
    public Vector3 Pos;
    public float Yaw, Scale, Height;
    public int Variant;
    /// <summary>Numer klastra; -1 = pojedynczy mały prop, -2 = wypełnienie pokrycia.</summary>
    public int Cluster;
}

public struct ClusterPlan
{
    public Vector3 Center;
    public bool Landmark;
}

/// <summary>
/// Rozkład propów poziomu (rola: kompozycja, nie rozsiew). Wejście: trójkąty NavMesha poziomu, oś ścieżki wejście-krata,
/// punkty zakazane (kotwice, źródła światła), landmarki. Wyjście: lista klastrów "hero + średnie + małe" w odstępach Poissona,
/// rzadkie pojedyncze małe propy i raport. Reguły (wszystkie mierzone w raporcie `[Decor]`):
///  - klaster: 1 hero + 2-4 średnie + 4-8 małych w promieniu 3 m; odstęp środków >= 7 m (poziomy 1-2) / 9 (3-4) / 11 (5-8);
///  - środek klastra >= 3 m od osi ścieżki, >= 4 m od kotwic (wejście, strefy, krata), >= 3 m od źródeł światła;
///  - każdy prop wyższy niż 0,5 m >= 2,5 m od osi ścieżki (korytarz wolny od przeszkód);
///  - poza klastrami rzadkie małe propy: siatka 5 m, 30 % szans na węzeł, >= 3,5 m od klastrów;
///  - rytm: waga kandydata rośnie w salach (daleko od krawędzi) i w "gęstych" strefach szumu 22 m; korytarze i puste strefy zostają puste;
///  - w salach środek klastra przesuwany do ściany (nisza), żeby środek sali został wolny;
///  - pokrycie siatki 10 m: cel 55-80 % komórek chodnych (chodna = >= 8 m² NavMesha), max 10 propów w komórce.
/// </summary>
public static class DecorLayout
{
    public const float CellSize = 10f;
    public const float MinWalkableCellArea = 8f;
    public const int MaxPerCell = 10;
    public const float TallHeight = 0.5f;
    public const float CorridorHalfWidth = 2.5f;
    public const float ClusterMinPath = 3f;
    public const float ClusterMinAnchor = 4f;
    public const float ClusterMinFixture = 3f;
    public const float SingleSpacing = 5f;
    public const float SingleChance = 0.3f;
    public const float ClusterCoverTarget = 0.62f;
    public const float CoverMin = 0.55f;
    public const float CoverMax = 0.80f;
    public const float CoverSinglesCap = 0.76f;
    public const float ClusterRadius = 3f;

    public static DecorBiome BiomeOf(int level) => level <= 2 ? DecorBiome.Forest : level <= 4 ? DecorBiome.Rock : level <= 6 ? DecorBiome.Crystal : DecorBiome.Abyss;

    /// <summary>Minimalny odstęp środków klastrów (m): 7 / 9 / 11.</summary>
    public static float MinClusterSpacing(int level) => level <= 2 ? 7f : level <= 4 ? 9f : 11f;

    // ------------------------------------------------------------ tabele propów

    private struct Entry
    {
        public PropType Type; public float Weight, ScaleMin, ScaleMax;
        public Entry(PropType t, float w, float smin, float smax) { Type = t; Weight = w; ScaleMin = smin; ScaleMax = smax; }
    }

    private static Entry[] Heroes(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return new[] { new Entry(PropType.Tree, 1f, 0.85f, 1.15f) };
            case DecorBiome.Rock: return new[] { new Entry(PropType.Boulder, 0.6f, 0.8f, 1.15f), new Entry(PropType.DeadTree, 0.4f, 0.85f, 1.15f) };
            case DecorBiome.Crystal: return new[] { new Entry(PropType.CrystalBig, 1f, 0.85f, 1.1f) };
            default: return new[] { new Entry(PropType.Fang, 1f, 0.8f, 1.15f) };
        }
    }

    private static Entry[] Mediums(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return new[] { new Entry(PropType.Bush, 0.6f, 0.85f, 1f), new Entry(PropType.Stone, 0.4f, 0.6f, 1f) };
            case DecorBiome.Rock: return new[] { new Entry(PropType.Rubble, 0.35f, 0.8f, 1.2f), new Entry(PropType.Stalagmite, 0.45f, 0.6f, 1f), new Entry(PropType.Stone, 0.2f, 0.7f, 1f) };
            case DecorBiome.Crystal: return new[] { new Entry(PropType.CrystalSmall, 0.7f, 0.8f, 1.1f), new Entry(PropType.Stone, 0.3f, 0.6f, 0.9f) };
            default: return new[] { new Entry(PropType.Cocoon, 0.55f, 0.8f, 1.1f), new Entry(PropType.WebLean, 0.45f, 0.8f, 1.1f) };
        }
    }

    private static Entry[] Smalls(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return new[] { new Entry(PropType.Grass, 0.55f, 0.9f, 1.1f), new Entry(PropType.Pebble, 0.3f, 0.5f, 1f), new Entry(PropType.Stone, 0.15f, 0.4f, 0.55f) };
            case DecorBiome.Rock: return new[] { new Entry(PropType.Pebble, 0.7f, 0.5f, 1f), new Entry(PropType.StalagmiteSmall, 0.3f, 0.7f, 1f) };
            case DecorBiome.Crystal: return new[] { new Entry(PropType.Mushroom, 0.55f, 0.8f, 1.2f), new Entry(PropType.CrystalTiny, 0.3f, 0.7f, 1f), new Entry(PropType.Pebble, 0.15f, 0.5f, 1f) };
            default: return new[] { new Entry(PropType.FangSmall, 0.45f, 0.7f, 0.95f), new Entry(PropType.Pebble, 0.55f, 0.5f, 1f) };
        }
    }

    /// <summary>Pojedyncze rzadkie propy poza klastrami: zawsze niskie (&lt;= 0,5 m).</summary>
    private static Entry[] Singles(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return new[] { new Entry(PropType.Grass, 0.6f, 0.9f, 1.1f), new Entry(PropType.Pebble, 0.4f, 0.5f, 1f) };
            case DecorBiome.Rock: return new[] { new Entry(PropType.Pebble, 0.65f, 0.5f, 1f), new Entry(PropType.StalagmiteSmall, 0.35f, 0.6f, 1f) };
            case DecorBiome.Crystal: return new[] { new Entry(PropType.Mushroom, 0.4f, 0.8f, 1.2f), new Entry(PropType.CrystalTiny, 0.35f, 0.7f, 1f), new Entry(PropType.Pebble, 0.25f, 0.5f, 1f) };
            default: return new[] { new Entry(PropType.Pebble, 0.6f, 0.5f, 1f), new Entry(PropType.FangSmall, 0.4f, 0.7f, 0.95f) };
        }
    }

    private static PropType LandmarkHero(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return PropType.LandmarkTree;
            case DecorBiome.Rock: return PropType.Menhir;
            case DecorBiome.Crystal: return PropType.LandmarkCrystals;
            default: return PropType.LandmarkFang;
        }
    }

    /// <summary>Propy wspierające landmark: (typ, liczba, zakres skali, średni = dalej od hero).</summary>
    private static (PropType type, int count, float smin, float smax, bool medium)[] LandmarkSupport(DecorBiome b)
    {
        switch (b)
        {
            case DecorBiome.Forest: return new[] { (PropType.Stone, 2, 0.7f, 1f, true), (PropType.Bush, 2, 0.85f, 1f, true), (PropType.Grass, 3, 0.9f, 1.1f, false) };
            case DecorBiome.Rock: return new[] { (PropType.Rubble, 2, 0.8f, 1.2f, true), (PropType.Pebble, 3, 0.5f, 1f, false), (PropType.StalagmiteSmall, 1, 0.8f, 1f, false) };
            case DecorBiome.Crystal: return new[] { (PropType.CrystalSmall, 2, 0.8f, 1.1f, true), (PropType.Mushroom, 3, 0.8f, 1.2f, false), (PropType.Pebble, 1, 0.5f, 1f, false) };
            default: return new[] { (PropType.Cocoon, 3, 0.8f, 1.1f, true), (PropType.WebLean, 1, 0.8f, 1.1f, true), (PropType.FangSmall, 2, 0.7f, 0.95f, false) };
        }
    }

    /// <summary>Typy używane przez biom (do rozgrzania siatek przed pomiarem czasu).</summary>
    public static PropType[] TypesFor(DecorBiome b)
    {
        var list = new List<PropType>();
        foreach (var e in Heroes(b)) list.Add(e.Type);
        foreach (var e in Mediums(b)) if (!list.Contains(e.Type)) list.Add(e.Type);
        foreach (var e in Smalls(b)) if (!list.Contains(e.Type)) list.Add(e.Type);
        foreach (var e in Singles(b)) if (!list.Contains(e.Type)) list.Add(e.Type);
        foreach (var s in LandmarkSupport(b)) if (!list.Contains(s.type)) list.Add(s.type);
        list.Add(LandmarkHero(b));
        return list.ToArray();
    }

    public static float NominalHeight(PropType t)
    {
        switch (t)
        {
            case PropType.Tree: return 6.6f;
            case PropType.Bush: return 1f;
            case PropType.Stone: return 0.66f;
            case PropType.Grass: return 0.3f;
            case PropType.Pebble: return 0.15f;
            case PropType.Boulder: return 1.7f;
            case PropType.DeadTree: return 4f;
            case PropType.Rubble: return 0.55f;
            case PropType.Stalagmite: return 1.4f;
            case PropType.StalagmiteSmall: return 0.45f;
            case PropType.CrystalBig: return 3f;
            case PropType.CrystalSmall: return 1.4f;
            case PropType.CrystalTiny: return 0.45f;
            case PropType.Mushroom: return 0.3f;
            case PropType.Fang: return 4f;
            case PropType.FangSmall: return 0.5f;
            case PropType.Cocoon: return 1.3f;
            case PropType.WebLean: return 1.2f;
            case PropType.LandmarkTree: return 8.4f;
            case PropType.Menhir: return 5f;
            case PropType.LandmarkCrystals: return 4.4f;
            default: return 6f;   // LandmarkFang
        }
    }

    public static bool IsLandmarkType(PropType t) => t >= PropType.LandmarkTree;

    private static float Radius(PropType t)
    {
        switch (t)
        {
            case PropType.Tree: return 0.5f;
            case PropType.Bush: return 0.6f;
            case PropType.Stone: return 0.6f;
            case PropType.Grass: return 0.15f;
            case PropType.Pebble: return 0.15f;
            case PropType.Boulder: return 1.2f;
            case PropType.DeadTree: return 0.4f;
            case PropType.Rubble: return 0.5f;
            case PropType.Stalagmite: return 0.4f;
            case PropType.StalagmiteSmall: return 0.2f;
            case PropType.CrystalBig: return 0.6f;
            case PropType.CrystalSmall: return 0.4f;
            case PropType.CrystalTiny: return 0.15f;
            case PropType.Mushroom: return 0.2f;
            case PropType.Fang: return 0.5f;
            case PropType.FangSmall: return 0.15f;
            case PropType.Cocoon: return 0.5f;
            case PropType.WebLean: return 0.6f;
            case PropType.LandmarkCrystals: return 1f;
            default: return 0.8f;   // LandmarkTree, Menhir, LandmarkFang
        }
    }

    // ------------------------------------------------------------ wejście / wyjście

    public sealed class Params
    {
        public int Level;
        public DecorBiome Biome;
        public float LevelY;                 // wysokość LevelEntry; pas +-3 m wokół niej to ten poziom
        public float BandTolerance = 3f;
        public Vector3[] Path;               // oś ścieżki wejście-krata (rogi NavMesha)
        public Vector3[] Anchors;            // wejście, strefy, krata: bez klastrów w 4 m
        public Vector3[] Fixtures;           // źródła światła (pochodnie, grzyby, kryształy): bez klastrów w 3 m
        public Vector3[] LandmarkPositions;
        public System.Func<Vector3, float> Clearance;   // odległość do sufitu nad punktem (>= 1000 = brak)
        public System.Random Rng;
        public System.Diagnostics.Stopwatch Clock;      // wspólny zegar budżetu (startuje wołający)
        public double PlanBudgetMs = 30.0;
    }

    public sealed class Result
    {
        public List<PropPlan> Props = new List<PropPlan>();
        public List<ClusterPlan> Clusters = new List<ClusterPlan>();
        public HashSet<(int, int)> Walkable = new HashSet<(int, int)>();
        public bool NoNavMesh;
        public bool PlanTruncated;
        public int FillAdded;
        public int TriangleCount;
        public int RequestedLandmarks;
        public float WalkableArea;
        public Vector3[] Path;
        public float MinSpacing;
        public int Level;
    }

    public struct Stats
    {
        public int Clusters, Props, Walkable, Occupied, MaxInCell, Landmarks, Singles, Fill, CorridorViolations;
        public float CoveragePct, MinClusterDist, MinClusterPath, MinLandmarkHeight;
    }

    // ------------------------------------------------------------ plan

    private struct Tri { public Vector3 A, B, C; public float Area; }

    private static Vector3[] triVerts;
    private static int[] triIdx;
    private static int triFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { triVerts = null; triIdx = null; triFrame = -1; }

    private static void LoadTriangulation()
    {
        if (triVerts != null && triFrame == Time.frameCount) return;   // 8 poziomów startuje w tej samej klatce: jedna triangulacja
        var t = NavMesh.CalculateTriangulation();
        triVerts = t.vertices;
        triIdx = t.indices;
        triFrame = Time.frameCount;
    }

    /// <summary>Planuje rozkład; nigdy nie rzuca wyjątku (w razie błędu zwraca to, co zdążyło powstać).</summary>
    public static Result Plan(Params p)
    {
        var res = new Result();
        if (p == null || p.Rng == null) return res;
        res.Level = p.Level;
        res.MinSpacing = MinClusterSpacing(p.Level);
        res.Path = p.Path;
        res.RequestedLandmarks = p.LandmarkPositions != null ? p.LandmarkPositions.Length : 0;
        try { new Planner(p, res).Run(); }
        catch (System.Exception e) { Debug.LogError($"[Decor] Level {p.Level}: wyjątek w planowaniu ({e.GetType().Name}: {e.Message}); część propów pominięta"); }
        return res;
    }

    private sealed class Planner
    {
        private readonly Params p;
        private readonly Result res;
        private readonly System.Random rng;
        private readonly List<Tri> tris = new List<Tri>();
        private float[] cdf;
        private float totalArea;
        private readonly Dictionary<(int, int), float> cellArea = new Dictionary<(int, int), float>();
        private readonly Dictionary<(int, int), int> cellProps = new Dictionary<(int, int), int>();
        private int occupied;
        private Vector3 boundsMin, boundsMax;

        public Planner(Params p, Result res) { this.p = p; this.res = res; rng = p.Rng; }

        private bool Over() => p.Clock != null && p.Clock.Elapsed.TotalMilliseconds > p.PlanBudgetMs;
        private float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        private float Coverage => res.Walkable.Count == 0 ? 0f : occupied / (float)res.Walkable.Count;

        public void Run()
        {
            if (!CollectTriangles()) { res.NoNavMesh = true; return; }
            AccumulateCells();

            var heroes = Heroes(p.Biome);
            var mediums = Mediums(p.Biome);
            var smalls = Smalls(p.Biome);

            PlanLandmarks();
            PlanClusters(heroes, mediums, smalls);
            PlanSingles();
            PlanFill();
        }

        // -------------------------------------------------------- trójkąty i komórki

        private bool CollectTriangles()
        {
            LoadTriangulation();
            if (triVerts == null || triIdx == null) return false;
            boundsMin = new Vector3(float.MaxValue, 0f, float.MaxValue);
            boundsMax = new Vector3(float.MinValue, 0f, float.MinValue);
            for (int i = 0; i + 2 < triIdx.Length; i += 3)
            {
                var a = triVerts[triIdx[i]]; var b = triVerts[triIdx[i + 1]]; var c = triVerts[triIdx[i + 2]];
                if (Mathf.Abs(a.y - p.LevelY) > p.BandTolerance || Mathf.Abs(b.y - p.LevelY) > p.BandTolerance || Mathf.Abs(c.y - p.LevelY) > p.BandTolerance) continue;
                float area = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) * 0.5f;
                if (area < 0.05f) continue;
                tris.Add(new Tri { A = a, B = b, C = c, Area = area });
                Grow(a); Grow(b); Grow(c);
            }
            res.TriangleCount = tris.Count;
            if (tris.Count == 0) return false;
            cdf = new float[tris.Count];
            float acc = 0f;
            for (int i = 0; i < tris.Count; i++) { acc += tris[i].Area; cdf[i] = acc; }
            totalArea = acc;
            res.WalkableArea = acc;
            return totalArea > 1f;
        }

        private void Grow(Vector3 v)
        {
            boundsMin.x = Mathf.Min(boundsMin.x, v.x); boundsMin.z = Mathf.Min(boundsMin.z, v.z);
            boundsMax.x = Mathf.Max(boundsMax.x, v.x); boundsMax.z = Mathf.Max(boundsMax.z, v.z);
        }

        /// <summary>Pole NavMesha na komórkę 10 m: każdy trójkąt dzielony na n² podtrójkątów, pole rozdzielane po ich środkach.</summary>
        private void AccumulateCells()
        {
            foreach (var t in tris)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(t.Area / 4f)), 1, 12);
                float each = t.Area / (n * n);
                var ab = (t.B - t.A) / n; var ac = (t.C - t.A) / n;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n - i; j++)
                    {
                        AddArea(t.A + ab * (i + 1f / 3f) + ac * (j + 1f / 3f), each);
                        if (j < n - i - 1) AddArea(t.A + ab * (i + 2f / 3f) + ac * (j + 2f / 3f), each);
                    }
            }
            foreach (var kv in cellArea)
                if (kv.Value >= MinWalkableCellArea) res.Walkable.Add(kv.Key);
        }

        private void AddArea(Vector3 pt, float a)
        {
            var c = Cell(pt);
            cellArea.TryGetValue(c, out float cur);
            cellArea[c] = cur + a;
        }

        private static (int, int) Cell(Vector3 pt) => (Mathf.FloorToInt(pt.x / CellSize), Mathf.FloorToInt(pt.z / CellSize));

        private bool RandomPoint(out Vector3 q)
        {
            q = default;
            if (tris.Count == 0 || totalArea <= 0f) return false;
            float r = (float)rng.NextDouble() * totalArea;
            int lo = 0, hi = tris.Count - 1;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (cdf[mid] < r) lo = mid + 1; else hi = mid; }
            var t = tris[lo];
            float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            q = t.A + (t.B - t.A) * u + (t.C - t.A) * v;
            return true;
        }

        // -------------------------------------------------------- dodawanie propów

        private bool AddProp(PropType type, Vector3 pos, float yaw, float scale, int cluster, int clusterStart, bool checkSeparation)
        {
            float height = NominalHeight(type) * scale;
            if (height > TallHeight && PathDistance(p.Path, pos) < CorridorHalfWidth) return false;
            var cell = Cell(pos);
            cellProps.TryGetValue(cell, out int count);
            if (count >= MaxPerCell) return false;
            if (checkSeparation)
            {
                float r = Radius(type) * scale;
                for (int i = clusterStart; i < res.Props.Count; i++)
                {
                    var o = res.Props[i];
                    float need = r + Radius(o.Type) * o.Scale + 0.1f;
                    float dx = o.Pos.x - pos.x, dz = o.Pos.z - pos.z;
                    if (dx * dx + dz * dz < need * need) return false;
                }
            }
            res.Props.Add(new PropPlan { Type = type, Pos = pos, Yaw = yaw, Scale = scale, Height = height, Variant = rng.Next(DecorPrimitives.Variants), Cluster = cluster });
            cellProps[cell] = count + 1;
            if (count == 0 && res.Walkable.Contains(cell)) occupied++;
            return true;
        }

        private float ClampToCeiling(PropType type, Vector3 pos, float scale)
        {
            float nominal = NominalHeight(type);
            if (nominal < 2.5f || p.Clearance == null) return scale;
            float clear = p.Clearance(pos);
            if (clear >= 1000f) return scale;
            float max = (clear - 0.5f) / nominal;
            float min = IsLandmarkType(type) ? 4.2f / nominal : 0.55f;   // landmark widoczny z 30 m: >= 4 m nawet pod niskim sufitem
            return Mathf.Max(Mathf.Min(scale, max), min);
        }

        private static Entry Pick(Entry[] set, System.Random rng)
        {
            float total = 0f;
            foreach (var e in set) total += e.Weight;
            float r = (float)rng.NextDouble() * total;
            foreach (var e in set) { r -= e.Weight; if (r <= 0f) return e; }
            return set[set.Length - 1];
        }

        /// <summary>Punkt na NavMeshu w pierścieniu [rMin, rMax] wokół środka; do 6 prób na rozmieszczenie.</summary>
        private bool PlaceAround(PropType type, Vector3 center, float rMin, float rMax, float smin, float smax, int cluster, int clusterStart)
        {
            for (int k = 0; k < 6; k++)
            {
                float ang = R(0f, Mathf.PI * 2f), dist = R(rMin, rMax);
                var pt = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (!NavMesh.SamplePosition(pt, out NavMeshHit h, 0.4f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - p.LevelY) > p.BandTolerance) continue;
                if (Near(p.Fixtures, h.position, 0.8f)) continue;   // nie na słupie pochodni
                float scale = ClampToCeiling(type, h.position, R(smin, smax));
                if (AddProp(type, h.position, R(0f, 360f), scale, cluster, clusterStart, true)) return true;
            }
            return false;
        }

        // -------------------------------------------------------- landmarki

        private void PlanLandmarks()
        {
            if (p.LandmarkPositions == null) return;
            var heroType = LandmarkHero(p.Biome);
            var support = LandmarkSupport(p.Biome);
            for (int i = 0; i < p.LandmarkPositions.Length; i++)
            {
                var pos = p.LandmarkPositions[i];
                if (PathDistance(p.Path, pos) < CorridorHalfWidth) pos = PushFromPath(pos, CorridorHalfWidth + 0.3f);
                int id = res.Clusters.Count;
                int start = res.Props.Count;
                float scale = ClampToCeiling(heroType, pos, R(0.95f, 1.1f));
                if (!AddProp(heroType, pos, R(0f, 360f), scale, id, start, false)) continue;
                res.Clusters.Add(new ClusterPlan { Center = pos, Landmark = true });
                foreach (var s in support)
                    for (int k = 0; k < s.count; k++)
                        PlaceAround(s.type, pos, s.medium ? 1.6f : 0.9f, s.medium ? 3.2f : 3f, s.smin, s.smax, id, start);
            }
        }

        /// <summary>Przesuwa punkt od najbliższego odcinka ścieżki do podanej odległości (o ile NavMesh pozwala).</summary>
        private Vector3 PushFromPath(Vector3 pos, float want)
        {
            if (p.Path == null || p.Path.Length < 2) return pos;
            Vector2 q = new Vector2(pos.x, pos.z), best = q;
            float bestD = float.MaxValue;
            for (int i = 1; i < p.Path.Length; i++)
            {
                Vector2 a = new Vector2(p.Path[i - 1].x, p.Path[i - 1].z), b = new Vector2(p.Path[i].x, p.Path[i].z), ab = b - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2);
                var c = a + ab * t;
                float d = Vector2.Distance(q, c);
                if (d < bestD) { bestD = d; best = c; }
            }
            var away = q - best;
            if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
            away.Normalize();
            var target = best + away * want;
            var moved = new Vector3(target.x, pos.y, target.y);
            return NavMesh.SamplePosition(moved, out NavMeshHit h, 1.5f, NavMesh.AllAreas) ? h.position : pos;
        }

        // -------------------------------------------------------- klastry

        private bool FarEnough(Vector3 c)
        {
            float min = res.MinSpacing;
            for (int i = 0; i < res.Clusters.Count; i++)
            {
                float dx = res.Clusters[i].Center.x - c.x, dz = res.Clusters[i].Center.z - c.z;
                if (dx * dx + dz * dz < min * min) return false;
            }
            if (PathDistance(p.Path, c) < ClusterMinPath) return false;
            if (Near(p.Anchors, c, ClusterMinAnchor)) return false;
            if (Near(p.Fixtures, c, ClusterMinFixture)) return false;
            return true;
        }

        private static bool Near(Vector3[] pts, Vector3 c, float r)
        {
            if (pts == null) return false;
            for (int i = 0; i < pts.Length; i++)
            {
                float dx = pts[i].x - c.x, dz = pts[i].z - c.z;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        private float Noise01(float x, float z)
        {
            const float S = 22f;
            float fx = x / S, fz = z / S;
            int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
            float tx = Smooth(fx - ix), tz = Smooth(fz - iz);
            float a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private float Hash(int x, int z)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (z * 19349663) ^ (p.Level * 83492791);
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private void PlanClusters(Entry[] heroes, Entry[] mediums, Entry[] smalls)
        {
            int tries = 0, fails = 0;
            const int maxTries = 2500, maxFails = 450;
            while (Coverage < ClusterCoverTarget && tries < maxTries && fails < maxFails)
            {
                if (Over()) { res.PlanTruncated = true; return; }
                tries++;
                if (!RandomPoint(out Vector3 c)) return;
                if (!FarEnough(c)) { fails++; continue; }

                // rytm: sale (daleko od krawędzi) i "gęste" strefy szumu dostają klastry chętniej niż korytarze i puste strefy
                float edge = 0f;
                Vector3 edgePos = c;
                if (NavMesh.FindClosestEdge(c, out NavMeshHit eh, NavMesh.AllAreas)) { edge = eh.distance; edgePos = eh.position; }
                float roomW = 0.3f + 0.7f * Smooth(Mathf.Clamp01((edge - 2f) / 4f));
                float noiseW = 0.25f + 0.75f * Smooth(Mathf.Clamp01((Noise01(c.x, c.z) - 0.2f) / 0.5f));
                if (rng.NextDouble() > roomW * noiseW * 1.6f) { fails++; continue; }

                // nisza: w dużej sali środek klastra ląduje ~4,5 m od ściany, środek sali zostaje wolny
                if (edge > 5f)
                {
                    var dir = edgePos - c; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        var moved = c + dir.normalized * (edge - 4.5f);
                        if (NavMesh.SamplePosition(moved, out NavMeshHit mh, 0.5f, NavMesh.AllAreas) && FarEnough(mh.position)) c = mh.position;
                    }
                }

                if (!BuildCluster(c, heroes, mediums, smalls)) { fails++; continue; }
                fails = 0;
            }
        }

        private bool BuildCluster(Vector3 c, Entry[] heroes, Entry[] mediums, Entry[] smalls)
        {
            int id = res.Clusters.Count;
            int start = res.Props.Count;
            var hero = Pick(heroes, rng);
            float scale = ClampToCeiling(hero.Type, c, R(hero.ScaleMin, hero.ScaleMax));
            if (!AddProp(hero.Type, c, R(0f, 360f), scale, id, start, false)) return false;
            res.Clusters.Add(new ClusterPlan { Center = c, Landmark = false });
            int med = rng.Next(2, 5), small = rng.Next(4, 9);
            for (int i = 0; i < med; i++)
            {
                var e = Pick(mediums, rng);
                PlaceAround(e.Type, c, 1.2f, ClusterRadius, e.ScaleMin, e.ScaleMax, id, start);
            }
            for (int i = 0; i < small; i++)
            {
                var e = Pick(smalls, rng);
                PlaceAround(e.Type, c, 0.6f, ClusterRadius, e.ScaleMin, e.ScaleMax, id, start);
            }
            return true;
        }

        // -------------------------------------------------------- pojedyncze małe propy i wypełnienie

        private void PlanSingles()
        {
            var set = Singles(p.Biome);
            int nx = Mathf.CeilToInt((boundsMax.x - boundsMin.x) / SingleSpacing), nz = Mathf.CeilToInt((boundsMax.z - boundsMin.z) / SingleSpacing);
            if (nx <= 0 || nz <= 0 || nx * nz > 20000) return;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (Over()) { res.PlanTruncated = true; return; }
                    if (rng.NextDouble() > SingleChance) continue;
                    var pt = new Vector3(boundsMin.x + (ix + R(0.1f, 0.9f)) * SingleSpacing, p.LevelY, boundsMin.z + (iz + R(0.1f, 0.9f)) * SingleSpacing);
                    if (!SamplePoint(pt, 0.6f, out Vector3 pos)) continue;
                    if (TooCloseToCluster(pos, 3.5f) || Near(p.Fixtures, pos, 1.5f)) continue;
                    var cell = Cell(pos);
                    cellProps.TryGetValue(cell, out int cnt);
                    if (cnt == 0 && Coverage >= CoverSinglesCap) continue;   // nie podnosimy pokrycia ponad cel
                    var e = Pick(set, rng);
                    AddProp(e.Type, pos, R(0f, 360f), R(e.ScaleMin, e.ScaleMax), -1, 0, false);
                }
        }

        /// <summary>Gdy klastry nie dały 55 %: po jednym małym propie w pustych komórkach chodnych (od największej), aż do 57 %.</summary>
        private void PlanFill()
        {
            if (Coverage >= CoverMin) return;
            var set = Singles(p.Biome);
            var empty = new List<(int, int)>();
            foreach (var c in res.Walkable) if (!cellProps.ContainsKey(c)) empty.Add(c);
            empty.Sort((a, b) => cellArea[b].CompareTo(cellArea[a]));
            foreach (var cell in empty)
            {
                if (Coverage >= CoverMin + 0.02f || Over()) break;
                for (int k = 0; k < 14; k++)
                {
                    var pt = new Vector3((cell.Item1 + R(0.05f, 0.95f)) * CellSize, p.LevelY, (cell.Item2 + R(0.05f, 0.95f)) * CellSize);
                    if (!SamplePoint(pt, 1.2f, out Vector3 pos) || Cell(pos) != cell) continue;
                    var e = Pick(set, rng);
                    if (AddProp(e.Type, pos, R(0f, 360f), R(e.ScaleMin, e.ScaleMax), -2, 0, false)) { res.FillAdded++; break; }
                }
            }
        }

        private bool SamplePoint(Vector3 pt, float maxDist, out Vector3 pos)
        {
            pos = default;
            if (!NavMesh.SamplePosition(pt, out NavMeshHit h, maxDist, NavMesh.AllAreas)) return false;
            if (Mathf.Abs(h.position.y - p.LevelY) > p.BandTolerance) return false;
            pos = h.position;
            return true;
        }

        private bool TooCloseToCluster(Vector3 pos, float r)
        {
            for (int i = 0; i < res.Clusters.Count; i++)
            {
                float dx = res.Clusters[i].Center.x - pos.x, dz = res.Clusters[i].Center.z - pos.z;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }
    }

    // ------------------------------------------------------------ geometria ścieżki

    /// <summary>Odległość pozioma od łamanej; bez ścieżki float.MaxValue (nic nie jest zabronione).</summary>
    public static float PathDistance(Vector3[] pts, Vector3 p)
    {
        if (pts == null || pts.Length == 0) return float.MaxValue;
        var q = new Vector2(p.x, p.z);
        if (pts.Length == 1) return Vector2.Distance(q, new Vector2(pts[0].x, pts[0].z));
        float best = float.MaxValue;
        for (int i = 1; i < pts.Length; i++)
        {
            var a = new Vector2(pts[i - 1].x, pts[i - 1].z);
            var b = new Vector2(pts[i].x, pts[i].z);
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
            best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
    }

    // ------------------------------------------------------------ raport

    /// <summary>Statystyki policzone z pierwszych `built` propów planu (to, co faktycznie zbudowano).</summary>
    public static Stats Measure(Result r, int built)
    {
        var s = new Stats { MinClusterDist = -1f, MinClusterPath = -1f, MinLandmarkHeight = -1f };
        built = Mathf.Clamp(built, 0, r.Props.Count);
        var cells = new Dictionary<(int, int), int>();
        var used = new HashSet<int>();
        for (int i = 0; i < built; i++)
        {
            var pr = r.Props[i];
            s.Props++;
            var cell = (Mathf.FloorToInt(pr.Pos.x / CellSize), Mathf.FloorToInt(pr.Pos.z / CellSize));
            cells.TryGetValue(cell, out int n);
            cells[cell] = n + 1;
            if (pr.Cluster >= 0) used.Add(pr.Cluster);
            else if (pr.Cluster == -1) s.Singles++;
            else s.Fill++;
            if (pr.Height > TallHeight && PathDistance(r.Path, pr.Pos) < CorridorHalfWidth) s.CorridorViolations++;
            if (IsLandmarkType(pr.Type)) { s.Landmarks++; if (s.MinLandmarkHeight < 0f || pr.Height < s.MinLandmarkHeight) s.MinLandmarkHeight = pr.Height; }
        }
        foreach (var kv in cells)
        {
            s.MaxInCell = Mathf.Max(s.MaxInCell, kv.Value);
            if (r.Walkable.Contains(kv.Key)) s.Occupied++;
        }
        s.Walkable = r.Walkable.Count;
        s.CoveragePct = s.Walkable > 0 ? 100f * s.Occupied / s.Walkable : 0f;
        var centers = new List<Vector3>();
        foreach (int id in used)
        {
            if (id >= r.Clusters.Count) continue;
            centers.Add(r.Clusters[id].Center);
            float pd = PathDistance(r.Path, r.Clusters[id].Center);
            if (pd < float.MaxValue && (s.MinClusterPath < 0f || pd < s.MinClusterPath)) s.MinClusterPath = pd;
        }
        s.Clusters = centers.Count;
        for (int i = 0; i < centers.Count; i++)
            for (int j = i + 1; j < centers.Count; j++)
            {
                float d = Vector2.Distance(new Vector2(centers[i].x, centers[i].z), new Vector2(centers[j].x, centers[j].z));
                if (s.MinClusterDist < 0f || d < s.MinClusterDist) s.MinClusterDist = d;
            }
        return s;
    }

    public static string Report(Result r, Stats s, double ms)
    {
        var ci = CultureInfo.InvariantCulture;
        string dist = s.MinClusterDist < 0f ? "n/d" : s.MinClusterDist.ToString("F1", ci);
        string cpath = s.MinClusterPath < 0f ? "n/d" : s.MinClusterPath.ToString("F1", ci);
        string lh = s.MinLandmarkHeight < 0f ? "n/d" : s.MinLandmarkHeight.ToString("F1", ci);
        return string.Format(ci,
            "[Decor] Level {0}: klastrów {1}, propów {2}, pokrycie siatki 10 m: {3:F0} % komórek z propem, max propów w komórce {4}, min odstęp klastrów {5} m, czas {6:F0} ms" +
            " | komórek chodnych {7} (z propem {8}), wymagany odstęp {9:F0} m, landmarków {10}/{20} (min wys. {11} m), klaster najbliżej osi {12} m, wysokich propów w pasie 2,5 m od osi: {13}, singli {14}, wypełnienie {15}, NavMesh {16:F0} m2 / {17} trójkątów{18}{19}",
            r.Level, s.Clusters, s.Props, s.CoveragePct, s.MaxInCell, dist, ms,
            s.Walkable, s.Occupied, r.MinSpacing, s.Landmarks, lh, cpath, s.CorridorViolations, s.Singles, s.Fill, r.WalkableArea, r.TriangleCount,
            r.NoNavMesh ? " BRAK NAVMESHA POZIOMU" : "", r.PlanTruncated ? " PLAN UCIETY BUDZETEM" : "", r.RequestedLandmarks);
    }
}
