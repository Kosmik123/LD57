using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pocisk gracza (atak maga, ogień, strzały kul). Leci prosto bez fizyki, w podkrokach sprawdza OverlapSphere.
/// Bez callbacku: zadaje obrażenia wrogowi (SkillHit.Apply) i znika. Z callbackiem: znika przy trafieniu/ścianie/końcu
/// zasięgu i woła callback z pozycją (wybuch Fireballa). Pula w PlayerBoltPool.
/// Obiekt jest obrócony w kierunku lotu (Z = kierunek). Wygląd low poly:
/// zwykły pocisk = obracający się rdzeń + 2 krążące mniejsze bryły + smuga (TrailRenderer + MageShardTrail) + iskra co 0,1 s;
/// kula ognia (nested) = MageFireCore (6 brył + 3 liżące płomienie), światło pomarańczowe z migotaniem, w locie iskry (0,05 s),
/// dym (0,08 s) i żar na ziemi co 0,5 m, jeśli tor jest niżej niż 1,5 m nad podłogą.
/// </summary>
public class PlayerBolt : MonoBehaviour
{
    private const float ProbeRadius = 0.4f;
    private const float SubStep = 0.25f;
    private const float Glow = 3f;
    private const float FireSparkInterval = 0.05f, FireSmokeInterval = 0.08f, BoltSparkInterval = 0.1f, EmberSpacing = 0.5f;
    private static readonly Color FireLight = new Color(1f, 0.55f, 0.15f);

    private Vector3 direction;
    private float speed, remaining, size;
    private int damage;
    private Color color;
    private Transform owner;
    private System.Action<Vector3> onEnd;
    private bool flying, fire;
    private MageShardTrail cubeTrail;
    private Light glow;
    private Transform core, orbitPivot;
    private MeshRenderer coreRenderer;
    private MeshRenderer[] orbiterRenderers;
    private MageFireCore fireCore;
    private TrailRenderer ribbon;
    private Vector3 spin;
    private float sparkTimer, smokeTimer, emberDistance, flickerSeed;

    /// <summary>Tworzy gotowy obiekt (nieaktywny). Wołane przez pulę.</summary>
    public static PlayerBolt Create(Transform parent)
    {
        var go = new GameObject("Player Bolt");
        go.transform.SetParent(parent, false);
        var bolt = go.AddComponent<PlayerBolt>();

        // zwykły pocisk: rdzeń + 2 krążące bryły na pivocie obracanym wokół osi lotu (Z)
        var coreGo = MageStylizedFx.MakeShape(go.transform, "Core", MageStylizedFx.Shape.Octa, Color.white, 1f, 1f, Glow);
        bolt.core = coreGo.transform;
        bolt.coreRenderer = coreGo.GetComponent<MeshRenderer>();
        var pivot = new GameObject("Orbit Pivot");
        pivot.transform.SetParent(go.transform, false);
        bolt.orbitPivot = pivot.transform;
        bolt.orbiterRenderers = new MeshRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var o = MageStylizedFx.MakeShape(pivot.transform, "Orbiter", i == 0 ? MageStylizedFx.Shape.Octa : MageStylizedFx.Shape.Tetra,
                Color.white, 0.38f, 1f, Glow);
            o.transform.localPosition = new Vector3(i == 0 ? 0.85f : -0.85f, 0f, i == 0 ? 0.15f : -0.15f);
            bolt.orbiterRenderers[i] = o.GetComponent<MeshRenderer>();
        }

        // kula ognia
        bolt.fireCore = MageFireCore.Create(go.transform);
        bolt.fireCore.gameObject.SetActive(false);

        bolt.ribbon = go.AddComponent<TrailRenderer>();
        bolt.ribbon.time = 0.3f;
        bolt.ribbon.minVertexDistance = 0.08f;
        bolt.ribbon.sharedMaterial = SwordTrail.CreateTransparentMaterial("Player Bolt Ribbon (runtime)", new Color(2.5f, 2.5f, 2.5f, 1f));
        bolt.ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.ribbon.emitting = false;
        bolt.ribbon.enabled = false;

        bolt.cubeTrail = go.AddComponent<MageShardTrail>();
        bolt.cubeTrail.enabled = false;

        bolt.glow = go.AddComponent<Light>();
        bolt.glow.type = LightType.Point;
        bolt.glow.shadows = LightShadows.None;
        bolt.glow.enabled = false;

        go.SetActive(false);
        return bolt;
    }

    /// <param name="owner">Korzeń gracza (jego collidery są ignorowane).</param>
    /// <param name="light">Mocniejsze światło (kula ognia).</param>
    /// <param name="onEnd">Null = zwykły pocisk z obrażeniami; inaczej wybuch obsługuje wołający.</param>
    /// <param name="nested">Kula ognia (kupa płonących brył) zamiast zwykłego rdzenia.</param>
    public void Launch(Transform owner, Vector3 origin, Vector3 dir, float speed, float range, int damage,
        Color color, float size = 0.25f, bool light = false, System.Action<Vector3> onEnd = null, bool nested = false)
    {
        this.owner = owner;
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        this.speed = speed;
        remaining = range;
        this.damage = damage;
        this.color = color;
        this.size = size;
        this.onEnd = onEnd;
        fire = nested;
        sparkTimer = smokeTimer = 0f;
        emberDistance = 0f;
        flickerSeed = Random.value * 100f;
        transform.SetParent(null, true);
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction);
        transform.localScale = Vector3.one * size;
        spin = Random.onUnitSphere * 360f;
        gameObject.SetActive(true);

        core.gameObject.SetActive(!nested);
        orbitPivot.gameObject.SetActive(!nested);
        fireCore.gameObject.SetActive(nested);
        ribbon.Clear();
        ribbon.enabled = !nested;
        ribbon.emitting = !nested;
        cubeTrail.enabled = false;

        if (nested)
        {
            glow.color = FireLight;
            glow.intensity = 3f;
            glow.range = 6f;
        }
        else
        {
            MageStylizedFx.SetColor(coreRenderer, color, 1f, Glow);
            var accent = Color.Lerp(color, Color.white, 0.55f);
            foreach (var r in orbiterRenderers) MageStylizedFx.SetColor(r, accent, 1f, Glow + 0.5f);
            ribbon.widthMultiplier = size * 0.9f;
            var rg = new Gradient();
            rg.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f), new GradientColorKey(color, 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            ribbon.colorGradient = rg;
            cubeTrail.Configure(color, Mathf.Max(0.08f, size * 0.45f), 0.5f, false);
            cubeTrail.enabled = true;
            cubeTrail.Restart();
            glow.color = color;
            glow.intensity = light ? 2.5f : 1.8f;
            glow.range = light ? 4.5f : 3f;
        }
        glow.enabled = true;
        flying = true;
    }

    private void Update()
    {
        if (!flying) return;
        float dt = Time.deltaTime;
        if (fire) UpdateFire(dt);
        else
        {
            core.Rotate(spin * dt, Space.Self);
            orbitPivot.Rotate(0f, 0f, 640f * dt, Space.Self);
            sparkTimer += dt;
            if (sparkTimer >= BoltSparkInterval)
            {
                sparkTimer = 0f;
                MageStylizedFx.Spark(transform.position - direction * 0.15f, -direction * 0.5f, color);
            }
        }

        float move = Mathf.Min(speed * dt, remaining);
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
        emberDistance += move;
        if (fire && emberDistance >= EmberSpacing)
        {
            emberDistance = 0f;
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                MageStylizedFx.Ember(hit.point, 0.55f, 0.9f);
        }
        if (remaining <= 0.0001f) End(null);
    }

    private void UpdateFire(float dt)
    {
        glow.intensity = 3f * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 18f, flickerSeed));
        var behind = transform.position - direction * (size * 0.8f);
        sparkTimer += dt;
        while (sparkTimer >= FireSparkInterval)
        {
            sparkTimer -= FireSparkInterval;
            MageStylizedFx.FireSpark(behind, -direction);
        }
        smokeTimer += dt;
        while (smokeTimer >= FireSmokeInterval)
        {
            smokeTimer -= FireSmokeInterval;
            MageStylizedFx.SmokePuff(behind - direction * 0.2f);
        }
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
            MageStylizedFx.Flash(pos, color, 0.7f);
            MageStylizedFx.Burst(pos, color, 10, 0.16f, 5f, 0.5f);
            MageStylizedFx.Ring(WorldVfx.GroundBelow(pos), 0.9f, color, 10);
        }
        else
        {
            MageStylizedFx.Burst(pos, color, 4, 0.14f, 3f, 0.5f); // pocisk rozsypuje się na końcu zasięgu / na ścianie
        }
        PlayerBoltPool.Release(this);
    }

    public void Deactivate()
    {
        flying = false;
        cubeTrail.enabled = false;
        ribbon.emitting = false;
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
