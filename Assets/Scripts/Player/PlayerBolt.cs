using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pocisk gracza (atak maga, ogień, strzały kul). Leci prosto bez fizyki, w podkrokach sprawdza OverlapSphere.
/// Bez callbacku: zadaje obrażenia wrogowi (SkillHit.Apply) i znika. Z callbackiem: znika przy trafieniu/ścianie/końcu
/// zasięgu i woła callback z pozycją (wybuch Fireballa). Pula w PlayerBoltPool.
/// </summary>
public class PlayerBolt : MonoBehaviour
{
    private const float ProbeRadius = 0.4f;
    private const float SubStep = 0.25f;

    private Vector3 direction;
    private float speed, remaining;
    private int damage;
    private Color color;
    private Transform owner;
    private System.Action<Vector3> onEnd;
    private bool flying;
    private TrailRenderer trail;
    private Light glow;
    private MeshRenderer visual;
    private MaterialPropertyBlock block;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>Tworzy gotowy obiekt (nieaktywny). Wołane przez pulę.</summary>
    public static PlayerBolt Create(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Player Bolt";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        var bolt = go.AddComponent<PlayerBolt>();
        bolt.visual = go.GetComponent<MeshRenderer>();
        bolt.visual.sharedMaterial = SwordTrail.CreateTransparentMaterial("Player Bolt (runtime)", Color.white);
        bolt.visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.visual.receiveShadows = false;
        bolt.block = new MaterialPropertyBlock();

        bolt.trail = go.AddComponent<TrailRenderer>();
        bolt.trail.time = 0.25f;
        bolt.trail.minVertexDistance = 0.05f;
        bolt.trail.sharedMaterial = SwordTrail.CreateTransparentMaterial("Player Bolt Trail (runtime)", Color.white);
        bolt.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.trail.emitting = false;

        bolt.glow = go.AddComponent<Light>();
        bolt.glow.type = LightType.Point;
        bolt.glow.shadows = LightShadows.None;
        bolt.glow.enabled = false;

        go.SetActive(false);
        return bolt;
    }

    /// <param name="owner">Korzeń gracza (jego collidery są ignorowane).</param>
    /// <param name="onEnd">Null = zwykły pocisk z obrażeniami; inaczej wybuch obsługuje wołający.</param>
    public void Launch(Transform owner, Vector3 origin, Vector3 dir, float speed, float range, int damage,
        Color color, float size = 0.25f, bool light = false, System.Action<Vector3> onEnd = null)
    {
        this.owner = owner;
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        this.speed = speed;
        remaining = range;
        this.damage = damage;
        this.color = color;
        this.onEnd = onEnd;
        transform.SetParent(null, true);
        transform.position = origin;
        transform.localScale = Vector3.one * size;
        gameObject.SetActive(true);

        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, 1f));
        visual.SetPropertyBlock(block);

        trail.Clear();
        trail.widthMultiplier = size * 0.9f;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.emitting = true;

        glow.enabled = light;
        if (light) { glow.color = color; glow.intensity = 2.5f; glow.range = 4.5f; }
        flying = true;
    }

    private void Update()
    {
        if (!flying) return;
        float move = Mathf.Min(speed * Time.deltaTime, remaining);
        int steps = Mathf.Max(1, Mathf.CeilToInt(move / SubStep));
        float step = move / steps;
        for (int i = 0; i < steps; i++)
        {
            transform.position += direction * step;
            if (BoltHit.Probe(transform.position, ProbeRadius, owner, out var enemy, out _))
            {
                End(enemy);
                return;
            }
        }
        remaining -= move;
        if (remaining <= 0.0001f) End(null);
    }

    private void End(Collider enemy)
    {
        if (!flying) return;
        flying = false;
        var pos = transform.position;
        if (onEnd != null)
        {
            var cb = onEnd;
            onEnd = null;
            cb(pos);
        }
        else if (enemy != null)
        {
            var ctx = new SkillContext { AimDirection = direction, Origin = owner };
            SkillHit.Apply(enemy, ctx, pos - direction, damage, 0f, 0f, null);
            HitSplash.Burst(pos, color, 9);
        }
        else
        {
            HitSplash.Burst(pos, color, 5, 0.6f);
        }
        PlayerBoltPool.Release(this);
    }

    public void Deactivate()
    {
        flying = false;
        trail.emitting = false;
        glow.enabled = false;
        gameObject.SetActive(false);
    }
}

/// <summary>Wspólne rozpoznanie trafienia pocisków gracza: wróg (jak SkillHit.Apply) albo ściana.</summary>
public static class BoltHit
{
    private static readonly Collider[] buffer = new Collider[24];

    /// <summary>True, gdy w promieniu jest żywy wróg (enemy) albo pełna ściana (wall).</summary>
    public static bool Probe(Vector3 pos, float radius, Transform owner, out Collider enemy, out bool wall)
    {
        enemy = null;
        wall = false;
        int n = Physics.OverlapSphereNonAlloc(pos, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null) continue;
            if (owner != null && col.transform.IsChildOf(owner)) continue;
            if (ColliderTags.IsEnemy(col))
            {
                var health = col.GetComponentInParent<Health>();
                if (health != null && !health.IsDead) { enemy = col; return true; }
                continue;
            }
            if (col.isTrigger) continue;
            if (col.GetComponentInParent<PlayerBolt>() != null) continue;
            wall = true;
            return true;
        }
        return false;
    }
}

/// <summary>Pula pocisków gracza (16 na start, rośnie w razie potrzeby). Odporna na przeładowanie sceny.</summary>
public static class PlayerBoltPool
{
    private const int Initial = 16;
    private static readonly Stack<PlayerBolt> free = new Stack<PlayerBolt>();
    private static Transform root;

    public static PlayerBolt Get()
    {
        EnsureRoot();
        while (free.Count > 0)
        {
            var b = free.Pop();
            if (b != null) return b;
        }
        return PlayerBolt.Create(root);
    }

    public static void Release(PlayerBolt bolt)
    {
        if (bolt == null) return;
        bolt.Deactivate();
        EnsureRoot();
        bolt.transform.SetParent(root, false);
        free.Push(bolt);
    }

    private static void EnsureRoot()
    {
        if (root != null) return;
        free.Clear();
        root = new GameObject("Player Bolt Pool").transform;
        for (int i = 0; i < Initial; i++)
            free.Push(PlayerBolt.Create(root));
    }
}
