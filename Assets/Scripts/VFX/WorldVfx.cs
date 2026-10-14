using DG.Tweening;
using UnityEngine;

/// <summary>
/// Cząsteczki świata budowane w kodzie (bez assetów): serce, pocisk, krata oraz wspólne klocki dla DeathEffects
/// (iskry, pył, pierścienie, kałuża, światło, odłamki). Trzy współdzielone ParticleSystemy emitowane przez Emit(EmitParams).
/// </summary>
public static class WorldVfx
{
    public enum Kind { Spark, Float, Puff }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly ParticleSystem[] systems = new ParticleSystem[3];
    private static Texture2D ringTex, discTex;
    private static Material ringMat, discMat, shardMat;

    public static readonly Color HeartPink = new Color(1f, 0.35f, 0.55f);
    public static readonly Color ProjectileYellow = new Color(1f, 0.85f, 0.2f);

    // ------------------------------------------------------------ serce

    /// <summary>Wołać co klatkę z Update serca: ok. 2 różowe iskierki na sekundę, unoszące się.</summary>
    public static void HeartSparkle(Vector3 position, float deltaTime, ref float timer)
    {
        timer += deltaTime;
        const float interval = 0.5f;
        if (timer < interval) return;
        timer -= interval;
        var offset = Random.insideUnitSphere * 0.25f;
        Emit(Kind.Float, position + offset, new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.6f, 1.1f), Random.Range(-0.1f, 0.1f)),
             Color.Lerp(HeartPink, Color.white, Random.Range(0f, 0.4f)), Random.Range(0.06f, 0.12f), Random.Range(0.8f, 1.2f));
    }

    /// <summary>Zebranie serca: 16 iskier w miejscu serca, różowy pierścień i iskry przy graczu.</summary>
    public static void HeartPickupBurst(Vector3 heartPosition, Vector3 playerPosition)
    {
        for (int i = 0; i < 16; i++)
        {
            var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
            Emit(Kind.Spark, heartPosition, dir.normalized * Random.Range(1.5f, 3.5f),
                 Color.Lerp(HeartPink, Color.white, Random.Range(0f, 0.5f)), Random.Range(0.07f, 0.16f), Random.Range(0.4f, 0.7f));
        }
        Ring(playerPosition + Vector3.up * 0.1f, 1.1f, HeartPink, 0.4f, 0.8f, false);
        for (int i = 0; i < 6; i++)
            Emit(Kind.Float, playerPosition + Random.insideUnitSphere * 0.4f + Vector3.up * 0.6f,
                 new Vector3(0f, Random.Range(0.8f, 1.6f), 0f), HeartPink, Random.Range(0.06f, 0.12f), Random.Range(0.6f, 0.9f));
    }

    // ------------------------------------------------------------ pocisk

    /// <summary>Żółta smuga 0,12 s (szerokość 0,15 do 0). Tworzona raz na pocisk; Clear() przy wystrzale po stronie wołającego.</summary>
    public static TrailRenderer AddProjectileTrail(GameObject projectile)
    {
        var trail = projectile.GetComponent<TrailRenderer>();
        if (trail == null) trail = projectile.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 0f));
        trail.minVertexDistance = 0.05f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = SwordTrail.CreateTransparentMaterial("Projectile Trail (runtime)", Color.white);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(ProjectileYellow, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        return trail;
    }

    public static void ProjectileHit(Vector3 position, Color color)
    {
        for (int i = 0; i < 8; i++)
            Emit(Kind.Spark, position, Random.onUnitSphere * Random.Range(2f, 4.5f), color, Random.Range(0.05f, 0.11f), Random.Range(0.2f, 0.35f));
        Ring(position, 0.5f, color, 0.15f, 0.9f, true);
    }

    // ------------------------------------------------------------ krata

    /// <summary>10 szarych drobin sypiących się z góry kraty.</summary>
    public static void GateDust(Vector3 top, float width)
    {
        for (int i = 0; i < 10; i++)
        {
            var p = top + new Vector3(Random.Range(-0.5f, 0.5f) * width, Random.Range(-0.2f, 0f), Random.Range(-0.3f, 0.3f));
            float g = Random.Range(0.4f, 0.6f);
            Emit(Kind.Puff, p, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-1.6f, -0.8f), 0f),
                 new Color(g, g, g * 0.95f, 0.8f), Random.Range(0.12f, 0.25f), Random.Range(0.6f, 1f));
        }
    }

    /// <summary>Obłoczek przy ziemi na końcu zjazdu kraty.</summary>
    public static void GatePuff(Vector3 ground, float width)
    {
        for (int i = 0; i < 8; i++)
        {
            var dir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            float g = Random.Range(0.45f, 0.6f);
            Emit(Kind.Puff, ground + new Vector3(Random.Range(-0.5f, 0.5f) * width, 0.1f, 0f),
                 dir * Random.Range(0.4f, 1.2f) + Vector3.up * 0.3f, new Color(g, g, g, 0.7f), Random.Range(0.3f, 0.5f), Random.Range(0.5f, 0.8f));
        }
    }

    // ------------------------------------------------------------ klocki

    public static void Emit(Kind kind, Vector3 position, Vector3 velocity, Color color, float size, float life)
    {
        var ps = System(kind);
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams
        {
            position = position, velocity = velocity, startColor = color, startSize = size, startLifetime = life
        };
        ps.Emit(ep, 1);
    }

    /// <summary>Pierścień: na ziemi (płaski) albo zwrócony do kamery; rośnie do średnicy 2·radius i gaśnie.</summary>
    public static void Ring(Vector3 position, float radius, Color color, float seconds, float alpha, bool facingCamera)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "World Ring";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        var cam = Camera.main;
        go.transform.rotation = facingCamera && cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * radius * 0.3f;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = RingMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.AddComponent<RotateAndFade>().Init(new Color(color.r, color.g, color.b, alpha), 0f, seconds, seconds);
        go.transform.DOScale(Vector3.one * radius * 2f, seconds).SetEase(Ease.OutCubic);
    }

    /// <summary>Płaska kałuża: miękkie koło, rośnie 0,1 do 2·radius w 0,15 s, trwa lifetime i gaśnie.</summary>
    public static void Puddle(Vector3 position, float radius, Color color, float lifetime)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Puddle";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * 0.1f;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = DiscMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.AddComponent<RotateAndFade>().Init(new Color(color.r, color.g, color.b, 0.6f), 0f, lifetime, 0.8f);
        go.transform.DOScale(Vector3.one * radius * 2f, 0.15f).SetEase(Ease.OutQuad);
    }

    /// <summary>Krótkie światło punktowe, gasnące liniowo do zera.</summary>
    public static void FlashLight(Vector3 position, Color color, float intensity, float range, float seconds)
    {
        var go = new GameObject("Death Light");
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color; light.intensity = intensity; light.range = range;
        light.shadows = LightShadows.None;
        go.AddComponent<WorldVfxLight>().Init(light, intensity, seconds);
    }

    /// <summary>Wydłużony odłamek (cylinder) lecący na boki z obrotem i opadający.</summary>
    public static void Shard(Vector3 position, Vector3 velocity, Color color, float length, float seconds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Shard";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.rotation = Random.rotation;
        var scale = new Vector3(0.05f, length * 0.5f, 0.05f);
        go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = ShardMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        mr.SetPropertyBlock(block);
        go.AddComponent<WorldVfxShard>().Init(velocity, Random.onUnitSphere * Random.Range(360f, 900f), scale, seconds);
    }

    public static Vector3 GroundBelow(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.04f;
        return position + Vector3.up * 0.04f;
    }

    // ------------------------------------------------------------ zasoby

    private static ParticleSystem System(Kind kind)
    {
        int i = (int)kind;
        if (systems[i] != null) return systems[i];
        var go = new GameObject("World Vfx " + kind);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f; main.startLifetime = 0.5f; main.startSize = 0.1f;
        main.gravityModifier = kind == Kind.Spark ? 1f : kind == Kind.Float ? -0.05f : 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, kind == Kind.Puff ? 0.3f : 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, kind == Kind.Puff ? AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f)
                                                      : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        if (kind == Kind.Puff) { var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.dampen = 0.3f; }
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SwordTrail.CreateTransparentMaterial("World Vfx " + kind + " (runtime)", Color.white);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        systems[i] = ps;
        return ps;
    }

    private static Material RingMaterial()
    {
        if (ringMat != null) return ringMat;
        const int n = 128;
        ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "World Ring" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float band = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.82f) / 0.14f);
                px[y * n + x] = new Color(1f, 1f, 1f, band * band * (r < 0.98f ? 1f : 0f));
            }
        ringTex.SetPixels(px); ringTex.Apply();
        ringMat = SwordTrail.CreateTransparentMaterial("World Ring (runtime)", Color.white);
        ringMat.mainTexture = ringTex;
        return ringMat;
    }

    private static Material DiscMaterial()
    {
        if (discMat != null) return discMat;
        const int n = 64;
        discTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "World Disc" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((r - 0.45f) / 0.55f)));
            }
        discTex.SetPixels(px); discTex.Apply();
        discMat = SwordTrail.CreateTransparentMaterial("World Disc (runtime)", Color.white);
        discMat.mainTexture = discTex;
        return discMat;
    }

    private static Material ShardMaterial()
    {
        if (shardMat != null) return shardMat;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        shardMat = new Material(shader) { name = "World Shard (runtime)" };
        return shardMat;
    }
}

/// <summary>Odłamek: lot balistyczny z obrotem, kurczenie pod koniec, potem Destroy.</summary>
public class WorldVfxShard : MonoBehaviour
{
    private Vector3 velocity, spin, baseScale;
    private float life, elapsed;

    public void Init(Vector3 v, Vector3 spinDegrees, Vector3 scale, float seconds)
    {
        velocity = v; spin = spinDegrees; baseScale = scale; life = Mathf.Max(0.05f, seconds);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;
        velocity.y -= 12f * dt;
        transform.position += velocity * dt;
        transform.Rotate(spin * dt, Space.Self);
        float t = elapsed / life;
        transform.localScale = baseScale * Mathf.Clamp01((1f - t) / 0.4f);
        if (t >= 1f) Destroy(gameObject);
    }
}

/// <summary>Gasnące światło punktowe, potem Destroy.</summary>
public class WorldVfxLight : MonoBehaviour
{
    private Light lightRef;
    private float start, life, elapsed;

    public void Init(Light l, float intensity, float seconds)
    {
        lightRef = l; start = intensity; life = Mathf.Max(0.02f, seconds);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (lightRef != null) lightRef.intensity = start * Mathf.Clamp01(1f - elapsed / life);
        if (elapsed >= life) Destroy(gameObject);
    }
}
