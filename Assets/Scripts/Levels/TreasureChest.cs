using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Skrzynia z reliktem. Low poly z prymitywów (korpus, wieko na zawiasie, złote opaski i klamra, punktowe światło).
/// Stawiana w runtime przez PlaceOnLevel(levelRoot, count) w salach poza ścieżką wejście-krata (NavMesh poziomu liczony jak
/// w LevelPopulation: trójkąty w pasie wysokości wokół LevelEntry). Otwiera się, gdy gracz podejdzie na `openRadius`:
/// 0,8 s drżenia, wieko odskakuje, błysk, 5 esencji i ekran wyboru reliktu (RelicScreen). Ginie razem z poziomem.
/// </summary>
public class TreasureChest : MonoBehaviour
{
    private const string ChestsName = "Treasure Chests";
    private static readonly Color GoldGlow = new Color(3.2f, 2.2f, 0.6f);

    [SerializeField] private float openRadius = 1.5f;
    [SerializeField] private float shakeSeconds = 0.8f;
    [SerializeField] private int essenceReward = 5;

    private enum State { Closed, Opening, Open }
    private State state;
    private Transform lid, body;
    private Light glow;
    private float flickerSeed;

    public bool IsOpen => state == State.Open;

    // ------------------------------------------------------------ stawianie na poziomie

    /// <summary>
    /// Stawia do `count` skrzyń na poziomie. Idempotentne (drugie wywołanie na tym samym poziomie nic nie robi).
    /// Zwraca liczbę postawionych. Przy EliteDirector.DisabledForTests nic nie robi.
    /// </summary>
    public static int PlaceOnLevel(GameObject levelRoot, int count = 2)
    {
        if (levelRoot == null || count <= 0 || EliteDirector.DisabledForTests) return 0;
        if (levelRoot.transform.Find(ChestsName) != null) return 0;

        var entry = levelRoot.GetComponentInChildren<LevelEntry>();
        if (entry == null) return 0;
        var gate = levelRoot.GetComponentInChildren<ExitGate>();
        float levelY = entry.transform.position.y;

        // trójkąty NavMesha tego poziomu (poziomy stoją jeden pod drugim na wspólnym NavMeshu)
        var tri = NavMesh.CalculateTriangulation();
        var triangles = new List<Vector3[]>();
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            var a = tri.vertices[tri.indices[i]];
            var b = tri.vertices[tri.indices[i + 1]];
            var c = tri.vertices[tri.indices[i + 2]];
            if (Mathf.Abs(a.y - levelY) > 3f || Mathf.Abs(b.y - levelY) > 3f || Mathf.Abs(c.y - levelY) > 3f) continue;
            triangles.Add(new[] { a, b, c });
        }
        if (triangles.Count == 0) return 0;

        // oś ścieżki wejście-krata
        Vector3[] path = null;
        Vector3 entryNav = entry.transform.position;
        if (NavMesh.SamplePosition(entry.transform.position, out var entryHit, 4f, NavMesh.AllAreas))
        {
            entryNav = entryHit.position;
            if (gate != null && NavMesh.SamplePosition(gate.transform.position, out var gateHit, 4f, NavMesh.AllAreas))
            {
                var navPath = new NavMeshPath();
                if (NavMesh.CalculatePath(entryNav, gateHit.position, NavMesh.AllAreas, navPath)
                    && navPath.status == NavMeshPathStatus.PathComplete && navPath.corners.Length >= 2)
                    path = navPath.corners;
            }
        }

        var holder = new GameObject(ChestsName).transform;
        holder.SetParent(levelRoot.transform, true);
        var placed = new List<Vector3>();
        int made = 0;
        // kolejne rundy luzują wymagania: 1) poza ścieżką, przy ścianie; 2) poza ścieżką; 3) byle osiągalne
        for (int round = 0; round < 3 && made < count; round++)
        {
            float minPath = round < 2 ? 4f : 1.5f;
            for (int attempt = 0; attempt < 80 && made < count; attempt++)
            {
                var t = triangles[Random.Range(0, triangles.Count)];
                float u = Random.value, v = Random.value;
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                var p = t[0] + (t[1] - t[0]) * u + (t[2] - t[0]) * v;
                if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - levelY) > 3f) continue;
                p = hit.position;

                if (Flat(p, entryNav) < 8f) continue;
                if (PathDistance(path, p) < minPath) continue;
                bool far = true;
                foreach (var other in placed)
                    if (Flat(p, other) < 10f) { far = false; break; }
                if (!far) continue;

                // przy ścianie: krawędź NavMesha blisko, skrzynia przodem do środka
                Vector3 facing = Vector3.zero;
                bool nearEdge = NavMesh.FindClosestEdge(p, out var edge, NavMesh.AllAreas) && edge.distance < 3.5f && edge.distance > 0.7f;
                if (nearEdge)
                {
                    facing = p - edge.position;
                    facing.y = 0f;
                }
                if (round == 0 && !nearEdge) continue;
                if (facing.sqrMagnitude < 0.01f) facing = Random.insideUnitSphere;
                facing.y = 0f;
                if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;

                var navPathToChest = new NavMeshPath();
                if (!NavMesh.CalculatePath(entryNav, p, NavMesh.AllAreas, navPathToChest) || navPathToChest.status != NavMeshPathStatus.PathComplete)
                    continue;

                Create(holder, p, Quaternion.LookRotation(facing.normalized, Vector3.up));
                placed.Add(p);
                made++;
            }
        }
        if (made == 0) Destroy(holder.gameObject);
        return made;
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static float PathDistance(Vector3[] path, Vector3 point)
    {
        if (path == null || path.Length == 0) return float.MaxValue;
        var q = new Vector2(point.x, point.z);
        float best = float.MaxValue;
        for (int i = 1; i < path.Length; i++)
        {
            var a = new Vector2(path[i - 1].x, path[i - 1].z);
            var b = new Vector2(path[i].x, path[i].z);
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
            best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
    }

    // ------------------------------------------------------------ budowa

    private static Mesh cube;
    private static Material woodMat, darkMat, goldMat;

    public static TreasureChest Create(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Treasure Chest");
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(position, rotation);
        var chest = root.AddComponent<TreasureChest>();
        chest.Build();
        return chest;
    }

    private void Build()
    {
        // korpus 1,1 x 0,55 x 0,7, wieko 1,1 x 0,22 x 0,7 na zawiasie z tyłu
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        Box(body, "Base", new Vector3(0f, 0.275f, 0f), new Vector3(1.1f, 0.55f, 0.7f), Wood());
        Box(body, "Band L", new Vector3(-0.35f, 0.28f, 0f), new Vector3(0.1f, 0.58f, 0.74f), Gold());
        Box(body, "Band R", new Vector3(0.35f, 0.28f, 0f), new Vector3(0.1f, 0.58f, 0.74f), Gold());
        Box(body, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(1.16f, 0.08f, 0.76f), Dark());

        var hinge = new GameObject("Lid Hinge").transform;
        hinge.SetParent(body, false);
        hinge.localPosition = new Vector3(0f, 0.55f, -0.35f);
        lid = hinge;
        Box(hinge, "Lid", new Vector3(0f, 0.11f, 0.35f), new Vector3(1.1f, 0.22f, 0.7f), Wood());
        Box(hinge, "Lid Band L", new Vector3(-0.35f, 0.115f, 0.35f), new Vector3(0.1f, 0.25f, 0.74f), Gold());
        Box(hinge, "Lid Band R", new Vector3(0.35f, 0.115f, 0.35f), new Vector3(0.1f, 0.25f, 0.74f), Gold());
        Box(hinge, "Clasp", new Vector3(0f, 0.0f, 0.72f), new Vector3(0.16f, 0.22f, 0.06f), Gold());

        var lightGo = new GameObject("Chest Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.0f, 0.2f);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.78f, 0.35f);
        glow.intensity = 1.6f;
        glow.range = 5f;
        glow.shadows = LightShadows.None;
        flickerSeed = Random.value * 100f;
    }

    private static void Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private static Mesh CubeMesh()
    {
        if (cube != null) return cube;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        return cube;
    }

    private static Material Wood() => woodMat != null ? woodMat : woodMat = MakeMaterial("Chest Wood", new Color(0.32f, 0.18f, 0.09f), false, 0.15f);
    private static Material Dark() => darkMat != null ? darkMat : darkMat = MakeMaterial("Chest Dark", new Color(0.12f, 0.1f, 0.09f), false, 0.1f);
    private static Material Gold() => goldMat != null ? goldMat : goldMat = MakeMaterial("Chest Gold", new Color(2.6f, 1.8f, 0.45f), true, 0f);

    private static Material MakeMaterial(string name, Color color, bool unlit, float smoothness)
    {
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = name + " (runtime)" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    // ------------------------------------------------------------ zachowanie

    private void Update()
    {
        if (glow != null && state != State.Open)
            glow.intensity = 1.6f * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 3f, flickerSeed));

        if (state != State.Closed) return;
        if (Time.timeScale <= 0.5f) return; // pauza, ekran kart, hit stop
        var stats = PlayerStats.Instance;
        if (stats == null) return;
        var d = stats.transform.position - transform.position;
        if (d.y > 3f || d.y < -3f) return;
        d.y = 0f;
        if (d.sqrMagnitude <= openRadius * openRadius)
            Open();
    }

    public void Open()
    {
        if (state != State.Closed) return;
        state = State.Opening;
        Sfx.Play(SfxKind.Windup, transform.position, 0.6f);

        // drżenie narastające, potem wieko
        body.DOKill();
        var shake = body.DOShakePosition(shakeSeconds, new Vector3(0.05f, 0.015f, 0.05f), 28, 90f, false, false)
            .SetEase(Ease.InQuad).SetTarget(body).SetLink(gameObject);
        body.DOShakeRotation(shakeSeconds, new Vector3(0f, 4f, 3f), 22, 90f, false).SetTarget(body).SetLink(gameObject);
        glow.DOIntensity(4f, shakeSeconds).SetLink(gameObject);
        DOVirtual.DelayedCall(shakeSeconds * 0.5f, () =>
        {
            if (this != null) VoxelFx.Burst(transform.position + Vector3.up * 0.6f, GoldGlow, 8, 0.1f, 2.5f, 0.5f);
        }).SetLink(gameObject);
        shake.OnComplete(Burst);
    }

    private void Burst()
    {
        state = State.Open;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        lid.DOLocalRotate(new Vector3(-115f, 0f, 0f), 0.35f).SetEase(Ease.OutBack).SetLink(gameObject);

        var at = transform.position + Vector3.up * 0.7f;
        VoxelFx.Flash(at, new Color(4.5f, 3.4f, 1.2f), 2.2f);
        VoxelFx.Burst(at, GoldGlow, 26, 0.16f, 7f, 0.7f, 1f);
        VoxelFx.Pillar(transform.position, GoldGlow, 3.5f, 0.9f);
        VoxelFx.VoxelRing(transform.position + Vector3.up * 0.1f, 2.2f, GoldGlow, 0.5f);
        Sfx.Play(SfxKind.GateOpen, transform.position, 0.7f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.1f);

        EssencePickup.Drop(transform.position + Vector3.up * 0.4f, essenceReward);
        glow.DOIntensity(0.6f, 1.5f).SetLink(gameObject);
        DOVirtual.DelayedCall(0.55f, () => RelicScreen.Request()).SetLink(gameObject);
    }
}
