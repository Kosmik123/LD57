using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Esencja: mały świecący kryształ low poly wypadający z wrogów. Leci po łuku, po 0,5 s zaczyna lecieć do gracza w promieniu
/// magnesu, przy zebraniu daje esencję i XP (PlayerStats.CollectEssence) oraz iskry. Pula 32 sztuk (więcej tworzone na żądanie),
/// znika po `Lifetime` sekundach niezebrana. Nie wymaga prefabu ani collidera: gracza szuka przez PlayerStats.Instance.
/// </summary>
public class EssencePickup : MonoBehaviour
{
    private const int PoolSize = 32;
    private const float ArcSeconds = 0.5f;
    private const float Gravity = 22f;
    private const float Lifetime = 30f;
    private static readonly Color Glow = new Color(0.55f, 2.4f, 3.0f);

    public static float MagnetRadius = 4f;

    private static readonly Stack<EssencePickup> free = new Stack<EssencePickup>();
    private static Transform root;

    private Vector3 velocity;
    private float age, rest, flySpeed;
    private float groundY;
    private bool magnet;
    private Transform crystal;

    // ------------------------------------------------------------ API statyczne

    /// <summary>Wyrzuca `count` sztuk esencji z punktu (każda po własnym łuku).</summary>
    public static void Drop(Vector3 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var e = Get();
            e.Launch(position);
        }
    }

    private static EssencePickup Get()
    {
        EnsureRoot();
        while (free.Count > 0)
        {
            var e = free.Pop();
            if (e != null) return e;
        }
        return Create();
    }

    private static void EnsureRoot()
    {
        if (root != null) return;
        free.Clear(); // po przeładowaniu sceny stare obiekty już nie istnieją
        root = new GameObject("Essence Pool").transform;
        for (int i = 0; i < PoolSize; i++)
        {
            var e = Create();
            e.gameObject.SetActive(false);
            free.Push(e);
        }
    }

    private static EssencePickup Create()
    {
        var go = new GameObject("Essence");
        go.transform.SetParent(root, false);
        var e = go.AddComponent<EssencePickup>();
        // kryształ: wydłużony rdzeń + dwie małe bryły
        var core = MageStylizedFx.MakeShape(go.transform, "Core", MageStylizedFx.Shape.CrystalY, new Color(0.35f, 0.9f, 1f), 0.34f, 1f, 3f);
        core.transform.localScale = new Vector3(0.2f, 0.36f, 0.2f);
        e.crystal = core.transform;
        var a = MageStylizedFx.MakeShape(go.transform, "Chip A", MageStylizedFx.Shape.Octa, new Color(0.7f, 1f, 1f), 0.1f, 1f, 3.5f);
        a.transform.localPosition = new Vector3(0.12f, -0.05f, 0.05f);
        var b = MageStylizedFx.MakeShape(go.transform, "Chip B", MageStylizedFx.Shape.Tetra, new Color(0.7f, 1f, 1f), 0.08f, 1f, 3.5f);
        b.transform.localPosition = new Vector3(-0.1f, 0.08f, -0.06f);
        return e;
    }

    private static void Release(EssencePickup e)
    {
        if (e == null) return;
        EnsureRoot();
        e.gameObject.SetActive(false);
        e.transform.SetParent(root, false);
        free.Push(e);
    }

    // ------------------------------------------------------------ życie

    private void Launch(Vector3 position)
    {
        transform.SetParent(null, true);
        transform.position = position + Vector3.up * 0.6f;
        var dir = Random.insideUnitCircle.normalized * Random.Range(1.2f, 3.2f);
        velocity = new Vector3(dir.x, Random.Range(5f, 7.5f), dir.y);
        age = 0f;
        rest = 0f;
        flySpeed = 4f;
        magnet = false;
        groundY = position.y + 0.45f;
        if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y + 0.45f;
        gameObject.SetActive(true);
        crystal.localRotation = Random.rotation;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        crystal.Rotate(0f, 200f * dt, 0f, Space.World);

        if (age >= Lifetime)
        {
            Release(this);
            return;
        }

        var stats = PlayerStats.Instance;
        Vector3 playerPos = stats != null ? stats.transform.position + Vector3.up * 0.9f : transform.position;

        // magnes po 0,5 s
        if (!magnet && age >= ArcSeconds && stats != null)
        {
            if ((playerPos - transform.position).sqrMagnitude <= MagnetRadius * MagnetRadius)
                magnet = true;
        }

        if (magnet)
        {
            flySpeed += 30f * dt;
            transform.position = Vector3.MoveTowards(transform.position, playerPos, flySpeed * dt);
            if ((playerPos - transform.position).sqrMagnitude < 0.5f * 0.5f)
                Collect(stats);
            return;
        }

        // łuk, potem leżenie z lekkim bujaniem
        if (velocity.sqrMagnitude > 0.0001f)
        {
            velocity.y -= Gravity * dt;
            var next = transform.position + velocity * dt;
            if (next.y <= groundY && velocity.y < 0f)
            {
                next.y = groundY;
                velocity = Vector3.zero;
            }
            transform.position = next;
        }
        else
        {
            rest += dt;
            var p = transform.position;
            p.y = groundY + Mathf.Sin(rest * 3f) * 0.06f;
            transform.position = p;
        }
    }

    private void Collect(PlayerStats stats)
    {
        if (stats == null) { Release(this); return; }
        var at = transform.position;
        stats.CollectEssence(1);
        VoxelFx.Burst(at, Glow, 6, 0.1f, 3.5f, 0.3f);
        Sfx.Play(SfxKind.CardPick, at, 0.3f);
        Release(this);
    }
}
