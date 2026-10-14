using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dekoracje poziomu (gradient: pajeczyny, kokony, gniazdo). Komponent na roocie prefabu poziomu; w Start generuje
/// obiekty pod dzieckiem "Decor" z deterministycznego seeda (numer poziomu). Czysto wizualne: bez colliderow,
/// bez EnemyHealth, bez tagu Enemy. Gdy raycasty nic nie znajda (brak colliderow), po prostu pomija element.
/// Pola *Prefab: gdy puste, uzywane sa prymitywy z DecorPrimitives (level artist podmienia na modele).
/// </summary>
public class CaveDecor : MonoBehaviour
{
    [SerializeField, Tooltip("Numer poziomu 1-8 (0 = z LevelEnemyTracker). Seed losowania = numer.")] private int levelNumber;
    [Header("Podmiana placeholderow (puste = prymitywy)")]
    [SerializeField, Tooltip("Pajeczyna: quad/model o boku 1 m, zostanie przeskalowany")] private GameObject webPrefab;
    [SerializeField, Tooltip("Kokon: os dluga = Y, wysokosc ok. 1 m")] private GameObject cocoonPrefab;
    [SerializeField, Tooltip("Gniazdo: pivot na gorze, wisi w dol")] private GameObject nestPrefab;
    [SerializeField, Tooltip("Pajak-prop (bez AI), nogi w plaszczyznie XZ")] private GameObject spiderPropPrefab;
    [SerializeField, Tooltip("Szkielet z mieczem, lezy wzdluz Z, pivot na podlodze")] private GameObject skeletonPrefab;

    private const string DecorName = "Decor";
    private const int TriesPerItem = 12;

    private System.Random rng;
    private readonly List<MeshCollider> colliders = new List<MeshCollider>();
    private readonly List<Vector3> anchors = new List<Vector3>();
    private Transform decor;

    private void Start() => Generate();

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        var old = transform.Find(DecorName);
        if (old != null) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }

        int level = levelNumber;
        if (level <= 0) { var tr = GetComponent<LevelEnemyTracker>(); if (tr != null) level = tr.LevelNumber; }
        if (level < 1) return;

        Physics.SyncTransforms();
        CollectColliders();
        if (colliders.Count == 0) return;
        CollectAnchors(out Vector3 arena);
        if (anchors.Count == 0) return;

        rng = new System.Random(level);
        decor = new GameObject(DecorName).transform;
        decor.SetParent(transform, false);

        switch (level)
        {
            case 1:
                break;   // poziom 1: tylko propy ogolne (nizej)
            case 2:
                Skeleton();
                for (int i = 0; i < 4; i++) Strand();
                break;
            case 3:
                for (int i = 0; i < 6; i++) CornerWeb(Rand(1.5f, 2.2f));
                WallSpider();
                break;
            case 4:
                for (int i = 0; i < 8; i++) CeilingWeb(Rand(1.6f, 2.4f));
                WallCocoon(anchors.GetRange(0, 1), 1.1f, 5f);
                break;
            case 5:
                for (int i = 0; i < 6; i++) WallCocoon(anchors, Rand(0.9f, 1.4f));
                break;
            case 6:
                for (int i = 0; i < 5; i++) CorridorWeb(Rand(2f, 3f));
                for (int i = 0; i < 5; i++) { if (i % 2 == 0) CornerWeb(Rand(2f, 3f)); else CeilingWeb(Rand(2f, 3f)); }
                for (int i = 0; i < 6; i++) WallCocoon(anchors, Rand(0.9f, 1.5f));
                break;
            case 7:
                for (int i = 0; i < 16; i++)
                {
                    int k = i % 3;
                    if (k == 0) CeilingWeb(Rand(1.8f, 3f)); else if (k == 1) CornerWeb(Rand(1.8f, 3f)); else CorridorWeb(Rand(2f, 3f));
                }
                for (int i = 0; i < 4; i++) WallCocoon(anchors, Rand(0.9f, 1.4f));
                for (int i = 0; i < 3; i++) CeilingSpider();
                break;
            default:
                Nest(arena);
                var arenaAnchors = new List<Vector3> { arena };
                for (int i = 0; i < 6; i++) WallCocoon(arenaAnchors, Rand(1f, 1.6f), 14f);
                break;
        }

        // Propy ogolne po konkretnych elementach poziomu (nie zmieniaja sekwencji losowania tamtych).
        for (int i = 0; i < (level <= 4 ? 10 : 6); i++) Stalagmite();
        for (int i = 0; i < 6; i++) RubblePile();
        if (level == 8) NestFangs(arena);
    }

    // ------------------------------------------------------------ zbieranie danych poziomu

    private void CollectColliders()
    {
        colliders.Clear();
        foreach (var c in GetComponentsInChildren<MeshCollider>(false))
        {
            if (!c.enabled || c.sharedMesh == null || c.isTrigger) continue;
            if (c.GetComponentInParent<ExitGate>() != null) continue;
            if (decor != null && c.transform.IsChildOf(decor)) continue;
            colliders.Add(c);
        }
    }

    /// <summary>Kotwice: wejscie, strefy fal, krata. `arena` = srodek ostatniej strefy (najwiekszy numer), a bez stref krata.</summary>
    private void CollectAnchors(out Vector3 arena)
    {
        anchors.Clear();
        var entry = GetComponentInChildren<LevelEntry>(true);
        if (entry != null) anchors.Add(entry.transform.position);
        var gate = GetComponentInChildren<ExitGate>(true);
        arena = gate != null ? gate.transform.position : transform.position;
        int best = int.MinValue;
        foreach (var z in GetComponentsInChildren<WaveZone>(true))
        {
            anchors.Add(z.transform.position);
            int n = TrailingNumber(z.name);
            if (n >= best) { best = n; arena = z.transform.position; }
        }
        if (gate != null) anchors.Add(gate.transform.position);
        if (anchors.Count == 0) anchors.Add(transform.position);
    }

    private static int TrailingNumber(string s)
    {
        int end = s.Length;
        int i = end;
        while (i > 0 && char.IsDigit(s[i - 1])) i--;
        return i < end && int.TryParse(s.Substring(i), out int n) ? n : 0;
    }

    // ------------------------------------------------------------ losowanie punktow

    private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    private bool Cast(Vector3 origin, Vector3 dir, float max, out RaycastHit best)
    {
        best = default;
        float bestD = float.MaxValue;
        var ray = new Ray(origin, dir);
        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null && colliders[i].Raycast(ray, out RaycastHit h, max) && h.distance < bestD)
            {
                bestD = h.distance; best = h;
            }
        }
        return bestD < float.MaxValue;
    }

    private bool FloorPoint(List<Vector3> from, float radius, out Vector3 p)
    {
        var a = from[rng.Next(from.Count)];
        float ang = Rand(0f, Mathf.PI * 2f), dist = Rand(0f, radius);
        var q = a + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
        if (NavMesh.SamplePosition(q, out NavMeshHit nh, 3f, NavMesh.AllAreas)) { p = nh.position; return true; }
        if (Cast(q + Vector3.up * 3f, Vector3.down, 10f, out RaycastHit h) && h.normal.y > 0.5f) { p = h.point; return true; }
        p = default;
        return false;
    }

    private bool TryCeiling(List<Vector3> from, float radius, out Vector3 pos, out Vector3 normal)
    {
        for (int t = 0; t < TriesPerItem; t++)
        {
            if (!FloorPoint(from, radius, out Vector3 p)) continue;
            if (Cast(p + Vector3.up * 0.5f, Vector3.up, 14f, out RaycastHit h) && h.distance + 0.5f > 2.5f && h.normal.y < -0.3f)
            { pos = h.point; normal = h.normal; return true; }
        }
        pos = normal = default;
        return false;
    }

    private bool TryWall(List<Vector3> from, float radius, float minDist, out Vector3 pos, out Vector3 normal)
    {
        for (int t = 0; t < TriesPerItem; t++)
        {
            if (!FloorPoint(from, radius, out Vector3 p)) continue;
            int start = rng.Next(8);
            for (int k = 0; k < 8; k++)
            {
                float a = (start + k) * 45f * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Cast(p + Vector3.up * 1.3f, d, 6f, out RaycastHit h) && h.distance >= minDist && Mathf.Abs(h.normal.y) < 0.5f)
                { pos = h.point; normal = h.normal; return true; }
            }
        }
        pos = normal = default;
        return false;
    }

    // ------------------------------------------------------------ elementy

    private GameObject Make(GameObject prefab, System.Func<GameObject> primitive)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);   // dekoracje nigdy nie blokuja
        }
        else go = primitive();
        go.transform.SetParent(decor, false);
        return go;
    }

    private void CeilingWeb(float size)
    {
        if (!TryCeiling(anchors, 12f, out Vector3 pos, out Vector3 n)) return;
        var w = Make(webPrefab, () => DecorPrimitives.Web(size));
        if (webPrefab != null) w.transform.localScale = Vector3.one * size;
        w.transform.SetPositionAndRotation(pos + n * 0.05f, Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, Rand(0f, 360f)));
    }

    private void CornerWeb(float size)
    {
        for (int t = 0; t < TriesPerItem; t++)
        {
            if (!TryWall(anchors, 12f, 1f, out Vector3 wp, out Vector3 wn)) return;
            if (!Cast(wp + wn * 0.25f + Vector3.up * 0.5f, Vector3.up, 5f, out RaycastHit c) || c.distance > 4f || c.normal.y > -0.3f) continue;
            var n = (wn + Vector3.down).normalized;
            var w = Make(webPrefab, () => DecorPrimitives.Web(size));
            if (webPrefab != null) w.transform.localScale = Vector3.one * size;
            w.transform.SetPositionAndRotation(c.point + wn * (size * 0.3f) + Vector3.down * (size * 0.3f), Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, Rand(0f, 360f)));
            return;
        }
    }

    /// <summary>Pajeczyna wpieta w poprzek korytarza: pionowy quad miedzy dwiema scianami.</summary>
    private void CorridorWeb(float size)
    {
        for (int t = 0; t < TriesPerItem; t++)
        {
            if (!FloorPoint(anchors, 12f, out Vector3 p)) return;
            float a = Rand(0f, Mathf.PI);
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var o = p + Vector3.up * 1.5f;
            if (!Cast(o, d, 8f, out RaycastHit h1) || !Cast(o, -d, 8f, out RaycastHit h2)) continue;
            float width = h1.distance + h2.distance;
            if (width < 2f || width > 9f) continue;
            float s = Mathf.Min(size, width * 0.9f);
            var mid = o + d * ((h1.distance - h2.distance) * 0.5f);
            var w = Make(webPrefab, () => DecorPrimitives.Web(s));
            if (webPrefab != null) w.transform.localScale = Vector3.one * s;
            w.transform.SetPositionAndRotation(new Vector3(mid.x, p.y + s * 0.5f + 0.1f, mid.z), Quaternion.LookRotation(d));
            return;
        }
    }

    /// <summary>Nitka zwisajaca z sufitu.</summary>
    private void Strand()
    {
        if (!TryCeiling(anchors, 12f, out Vector3 pos, out Vector3 n)) return;
        var a = pos + n * 0.02f;
        var b = a + Vector3.down * Rand(0.8f, 2f);
        var th = DecorPrimitives.Thread(a, b);
        th.transform.SetParent(decor, false);
    }

    private void Skeleton()
    {
        var entry = anchors[0];
        for (int t = 0; t < TriesPerItem; t++)
        {
            float ang = Rand(0f, Mathf.PI * 2f), dist = Rand(3f, 6f);
            var q = entry + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
            if (!NavMesh.SamplePosition(q, out NavMeshHit nh, 2f, NavMesh.AllAreas)) continue;
            var go = Make(skeletonPrefab, DecorPrimitives.Skeleton);
            go.transform.SetPositionAndRotation(nh.position + Vector3.up * 0.02f, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
            return;
        }
    }

    private void WallCocoon(List<Vector3> from, float size, float radius = 12f)
    {
        if (!TryWall(from, radius, 1f, out Vector3 pos, out Vector3 n)) return;
        var go = Make(cocoonPrefab, () => DecorPrimitives.Cocoon(size, rng));
        if (cocoonPrefab != null) go.transform.localScale = Vector3.one * size;
        go.transform.SetPositionAndRotation(pos + n * (size * 0.3f) + Vector3.up * (size * 0.1f), Quaternion.Euler(Rand(-8f, 8f), Rand(0f, 360f), Rand(-8f, 8f)));
    }

    /// <summary>Statyczny pajak na scianie w oddali (sam prop, bez AI).</summary>
    private void WallSpider()
    {
        if (!TryWall(anchors, 12f, 3f, out Vector3 pos, out Vector3 n)) return;
        var go = Make(spiderPropPrefab, DecorPrimitives.SpiderProp);
        var tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
        go.transform.SetPositionAndRotation(pos + n * 0.3f + Vector3.up * 0.5f, Quaternion.LookRotation(tangent.normalized, n) * Quaternion.Euler(0f, Rand(-30f, 30f), 0f));
    }

    /// <summary>Pajak wiszacy na nitce z sufitu, kolyszacy sie (DecorSway na uchwycie w punkcie zawieszenia).</summary>
    private void CeilingSpider()
    {
        if (!TryCeiling(anchors, 12f, out Vector3 pos, out Vector3 n)) return;
        float drop = Rand(0.8f, 1.4f);
        var holder = new GameObject("Hanging Spider").transform;
        holder.SetParent(decor, false);
        holder.SetPositionAndRotation(pos + n * 0.02f, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
        var thread = DecorPrimitives.Thread(Vector3.zero, Vector3.down * drop);
        thread.transform.SetParent(holder, false);
        var spider = Make(spiderPropPrefab, DecorPrimitives.SpiderProp);
        spider.transform.SetParent(holder, false);
        spider.transform.localPosition = Vector3.down * drop;
        spider.transform.localRotation = Quaternion.identity;
        holder.gameObject.AddComponent<DecorSway>().Configure(Rand(4f, 8f), Rand(0.8f, 1.4f), Rand(0f, 6.28f));
    }

    /// <summary>Stalagmit przy scianie: stozek 0,5-1,8 m, 0,4-0,9 m od najblizszej sciany (8 kierunkow poziomych, do 3 m).</summary>
    private void Stalagmite()
    {
        for (int t = 0; t < TriesPerItem; t++)
        {
            if (!FloorPoint(anchors, 14f, out Vector3 p)) return;
            if (!NearestWallDir(p, 3f, out Vector3 d, out float dist) || dist < 0.9f) continue;
            float h = Rand(0.5f, 1.8f);
            var go = Make(null, () => DecorPrimitives.Stalagmite(h, rng));
            var pos = p + d * (dist - Rand(0.4f, 0.9f));
            go.transform.SetPositionAndRotation(new Vector3(pos.x, p.y, pos.z), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
            return;
        }
    }

    private void RubblePile()
    {
        if (!FloorPoint(anchors, 14f, out Vector3 p)) return;
        var go = Make(null, () => DecorPrimitives.Rubble(rng));
        go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
    }

    /// <summary>8 kielkow gniazda w pierscieniu wokol areny (przy scianie, gdy jest w zasiegu 10 m).</summary>
    private void NestFangs(Vector3 arena)
    {
        if (!NavMesh.SamplePosition(arena, out NavMeshHit nh, 4f, NavMesh.AllAreas)) return;
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45f + Rand(-12f, 12f)) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float r = Rand(5.5f, 8f);
            if (Cast(nh.position + Vector3.up * 1.3f, d, 10f, out RaycastHit h)) r = Mathf.Min(r, h.distance - 0.5f);
            if (r < 2.5f) continue;
            var q = nh.position + d * r;
            if (NavMesh.SamplePosition(q, out NavMeshHit fh, 2.5f, NavMesh.AllAreas)) q = fh.position; else q.y = nh.position.y;
            float height = Rand(2f, 3f), width = Rand(0.25f, 0.4f);
            var go = Make(null, () => DecorPrimitives.Fang(height, width));
            go.transform.SetPositionAndRotation(q, Quaternion.Euler(Rand(-8f, 8f), Rand(0f, 360f), Rand(-8f, 8f)));
        }
    }

    private bool NearestWallDir(Vector3 floor, float max, out Vector3 dir, out float dist)
    {
        dir = Vector3.forward; dist = float.MaxValue;
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            if (Cast(floor + Vector3.up * 1f, d, max, out RaycastHit h) && Mathf.Abs(h.normal.y) < 0.5f && h.distance < dist)
            { dist = h.distance; dir = d; }
        }
        return dist < float.MaxValue;
    }

    private void Nest(Vector3 arena)
    {
        const float size = 4f;
        if (!NavMesh.SamplePosition(arena, out NavMeshHit nh, 4f, NavMesh.AllAreas)) { nh.position = arena; }
        if (!Cast(nh.position + Vector3.up * 0.5f, Vector3.up, 20f, out RaycastHit h) || h.normal.y > -0.3f) return;
        var go = Make(nestPrefab, () => DecorPrimitives.Nest(size));
        if (nestPrefab != null) go.transform.localScale = Vector3.one * size;
        go.transform.SetPositionAndRotation(h.point + h.normal * 0.1f, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
    }
}
