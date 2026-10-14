using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public enum EliteAffix { Fast, Armored, Explosive, Frost, Leader }

/// <summary>
/// Mnożniki prędkości NavMeshAgenta z wielu źródeł (Szybki, aura Przywódcy). Kontrolery wrogów (slime, pająk) ustawiają
/// agent.speed przy każdej zmianie stanu, więc nie ustawiamy mnożnika raz: w LateUpdate odczytujemy prędkość, a gdy różni
/// się od tego, co sami zapisaliśmy, uznajemy ją za nową bazę i zapisujemy bazę × mnożnik.
/// </summary>
public class EnemySpeedModifier : MonoBehaviour
{
    private readonly Dictionary<string, float> sources = new Dictionary<string, float>();
    private NavMeshAgent agent;
    private float baseSpeed, lastWritten;
    private bool initialized;

    public float Multiplier
    {
        get
        {
            float m = 1f;
            foreach (var kv in sources) m *= kv.Value;
            return m;
        }
    }

    private void Awake() => agent = GetComponent<NavMeshAgent>();

    public void SetSource(string key, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f)) sources.Remove(key);
        else sources[key] = multiplier;
    }

    public bool HasSource(string key) => sources.ContainsKey(key);

    private void LateUpdate()
    {
        if (agent == null) return;
        float current = agent.speed;
        if (!initialized || !Mathf.Approximately(current, lastWritten))
        {
            baseSpeed = current;
            initialized = true;
        }
        float target = baseSpeed * Multiplier;
        if (!Mathf.Approximately(current, target))
            agent.speed = target;
        lastWritten = agent.speed;
    }

    private void OnDisable()
    {
        // wyłączenie (śmierć, restart): oddaj bazę
        if (agent != null && initialized && Mathf.Approximately(agent.speed, lastWritten))
            agent.speed = baseSpeed;
        initialized = false;
    }
}

/// <summary>
/// Elita: wróg z jednym afiksem. Dodawany przez EliteDirector na istniejącego wroga, potem Apply(affix).
/// Wspólne: bryła ×1,35 (przez VisualRig.visualScale, żeby animacje nie zjadały skali), 4 bryły krążące i poświata
/// w kolorze afiksu, nazwa afiksu nad głową (2 s po spawnie i po pierwszym trafieniu), XP ×3. Drop esencji i serca
/// robi EliteDirector (słucha OnEnemyDied).
/// </summary>
[DefaultExecutionOrder(150)]
public class EliteModifier : MonoBehaviour
{
    public const float VisualScale = 1.35f;
    public const int XpMultiplier = 3;
    public static readonly string[] AffixNames = { "SZYBKI", "PANCERNY", "WYBUCHOWY", "LODOWY", "PRZYWÓDCA" };

    private static readonly Color[] AffixColors =
    {
        new Color(2.4f, 3.0f, 0.4f),  // Szybki: żółtozielony
        new Color(1.6f, 1.9f, 2.6f),  // Pancerny: stal
        new Color(3.2f, 1.5f, 0.4f),  // Wybuchowy: pomarańcz
        new Color(0.6f, 2.2f, 3.4f),  // Lodowy: błękit
        new Color(2.8f, 0.6f, 3.0f),  // Przywódca: fiolet
    };

    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string SpeedKey = "elite";
    private static readonly List<EliteModifier> alive = new List<EliteModifier>();
    private static readonly Collider[] buffer = new Collider[64];

    public static int AliveCount => alive.Count;

    public EliteAffix Affix { get; private set; }
    public Color AffixColor => AffixColors[(int)Affix];
    public string AffixName => AffixNames[(int)Affix];
    public bool IsApplied { get; private set; }

    [SerializeField] private float leaderRadius = 6f;
    [SerializeField] private float leaderSpeedBonus = 1.3f;
    [SerializeField] private float fastSpeedMultiplier = 1.6f;
    [SerializeField] private float armoredHealthMultiplier = 3f;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private int explosionDamage = 8;
    [SerializeField] private float explosionDelay = 0.6f;
    [SerializeField] private float iceSlow = 0.6f;
    [SerializeField] private float iceSeconds = 1.5f;

    private Health health;
    private EnemyHealth enemyHealth;
    private EnemySpeedModifier speed;
    private VoxelOrbit orbit;
    private TextMeshPro label;
    private float labelUntil = -1f;
    private float glowTimer;
    private float leaderTimer;
    private bool shown, dead;
    private readonly HashSet<EnemySpeedModifier> buffed = new HashSet<EnemySpeedModifier>();
    private readonly List<EnemySpeedModifier> toClear = new List<EnemySpeedModifier>();
    private string leaderKey;

    private void Awake()
    {
        health = GetComponent<Health>();
        enemyHealth = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        alive.Add(this);
        if (health != null)
        {
            health.OnHealthChanged += Health_OnHealthChanged;
            health.OnDied += Health_OnDied;
        }
        PlayerStats.OnPlayerHurt += PlayerStats_OnPlayerHurt;
    }

    private void OnDisable()
    {
        alive.Remove(this);
        if (health != null)
        {
            health.OnHealthChanged -= Health_OnHealthChanged;
            health.OnDied -= Health_OnDied;
        }
        PlayerStats.OnPlayerHurt -= PlayerStats_OnPlayerHurt;
        ClearLeaderAura();
    }

    private void OnDestroy()
    {
        if (orbit != null) Destroy(orbit.gameObject);
        if (label != null) Destroy(label.gameObject);
    }

    // ------------------------------------------------------------ nadanie afiksu

    public void Apply(EliteAffix affix)
    {
        if (IsApplied) return;
        IsApplied = true;
        Affix = affix;
        leaderKey = "leader:" + GetInstanceID();
        var color = AffixColor;

        // XP ×3
        if (enemyHealth != null)
        {
            var f = typeof(EnemyHealth).GetField("experienceReward", Priv);
            if (f != null) f.SetValue(enemyHealth, enemyHealth.ExperienceReward * XpMultiplier);
        }

        ScaleVisual();

        switch (affix)
        {
            case EliteAffix.Fast:
                GetSpeed().SetSource(SpeedKey, fastSpeedMultiplier);
                VoxelTrailEmitter.Attach(gameObject, color, 0.16f, 0.28f, 0.45f);
                break;
            case EliteAffix.Armored:
                MultiplyHealth(armoredHealthMultiplier);
                MakeMetallic();
                break;
            case EliteAffix.Leader:
                GetSpeed(); // upewnij się, że lider sam nie jest szybszy; aura działa na innych
                break;
        }

        orbit = VoxelOrbit.Attach(transform, color, 4, 1.15f, 1.0f, 3600f, 0.22f, 0.6f);
        CreateLabel();
        ShowLabel(2f);
        VoxelFx.Flash(transform.position + Vector3.up, color, 1.2f);
        VoxelFx.GroundGlow(transform, 1.6f, color * 0.55f, 8f);
        glowTimer = 6.5f;
    }

    private EnemySpeedModifier GetSpeed()
    {
        if (speed != null) return speed;
        speed = GetComponent<EnemySpeedModifier>();
        if (speed == null) speed = gameObject.AddComponent<EnemySpeedModifier>();
        return speed;
    }

    private void ScaleVisual()
    {
        var rig = GetComponent<VisualRig>();
        if (rig == null) return;
        rig.EnsureInit();
        if (!rig.HasVisual) return;
        // VisualRig składa skalę z pola visualScale co klatkę; mnożymy bazę, więc Inflate/Pulse/Collapse jej nie zgubią
        var f = typeof(VisualRig).GetField("visualScale", Priv);
        if (f == null) return;
        var s = (Vector3)f.GetValue(rig);
        f.SetValue(rig, s * VisualScale);
    }

    private void MultiplyHealth(float multiplier)
    {
        if (health == null) return;
        var maxField = typeof(Health).GetField("maxHealth", Priv);
        var curField = typeof(Health).GetField("currentHealth", Priv);
        if (maxField == null || curField == null) return;
        int max = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * multiplier));
        maxField.SetValue(health, max);
        curField.SetValue(health, max);
        health.ChangeHealth(0); // odśwież paski (zmiana 0 nie jest obrażeniem)
    }

    /// <summary>Stalowy wygląd: kopie materiałów z wysokim metalicznym i gładkością (zmiana trwała; DamageFlash zapamiętuje materiały dopiero przy błysku).</summary>
    private void MakeMetallic()
    {
        var steel = new Color(0.55f, 0.6f, 0.68f);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            var shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                var m = new Material(shared[i]) { name = shared[i].name + " (elite steel)" };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), steel, 0.6f));
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.9f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.75f);
                copies[i] = m;
            }
            r.sharedMaterials = copies;
        }
    }

    // ------------------------------------------------------------ etykieta

    private void CreateLabel()
    {
        var go = new GameObject("Elite Label");
        go.transform.SetParent(transform, false);
        label = go.AddComponent<TextMeshPro>();
        if (TMPro.TMP_Settings.defaultFontAsset != null) label.font = TMPro.TMP_Settings.defaultFontAsset;
        label.text = AffixName;
        label.fontSize = 6f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        var c = AffixColor;
        float m = Mathf.Max(0.01f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
        label.color = new Color(c.r / m, c.g / m, c.b / m, 0f);
        label.raycastTarget = false;
    }

    private void ShowLabel(float seconds)
    {
        labelUntil = Time.time + seconds;
    }

    private void Health_OnHealthChanged(int change)
    {
        if (change >= 0 || shown) return;
        shown = true;
        ShowLabel(2f);
    }

    // ------------------------------------------------------------ śmierć

    private void Health_OnDied()
    {
        if (dead) return;
        dead = true;
        ClearLeaderAura();
        if (speed != null) speed.SetSource(SpeedKey, 1f);
        var trail = GetComponent<VoxelTrailEmitter>();
        if (trail != null) trail.Emitting = false;
        if (orbit != null) { Destroy(orbit.gameObject); orbit = null; }
        if (label != null) { Destroy(label.gameObject); label = null; }

        if (Affix == EliteAffix.Explosive)
            EliteExplosion.Spawn(transform.position, explosionRadius, explosionDamage, explosionDelay, AffixColor);
    }

    // ------------------------------------------------------------ lodowy

    private void PlayerStats_OnPlayerHurt(int damage)
    {
        if (!IsApplied || dead || Affix != EliteAffix.Frost || PlayerStats.Instance == null) return;
        // trafienie w zwarciu albo pociskiem strzelca w zasięgu: w pobliżu gracza
        float reach = GetComponent<ShooterController>() != null ? 10f : 3.5f;
        var d = PlayerStats.Instance.transform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > reach * reach) return;
        PlayerStats.Instance.SetModifier(StatKind.MoveSpeed, "elite-ice", iceSlow, iceSeconds);
        var at = PlayerStats.Instance.transform.position + Vector3.up * 0.8f;
        VoxelFx.Burst(at, AffixColor, 10, 0.12f, 3.5f, 0.45f);
        VoxelFx.Ring(PlayerStats.Instance.transform.position + Vector3.up * 0.1f, 1.1f, AffixColor, 14);
    }

    // ------------------------------------------------------------ Update

    private void Update()
    {
        if (!IsApplied || dead) return;

        glowTimer -= Time.deltaTime;
        if (glowTimer <= 0f)
        {
            glowTimer = 6.5f;
            VoxelFx.GroundGlow(transform, 1.6f, AffixColor * 0.55f, 8f);
        }

        if (Affix == EliteAffix.Leader)
        {
            leaderTimer -= Time.deltaTime;
            if (leaderTimer <= 0f)
            {
                leaderTimer = 0.25f;
                LeaderTick();
            }
        }
    }

    private void LateUpdate()
    {
        if (label == null) return;
        float remaining = labelUntil - Time.time;
        float alpha = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / 0.4f);
        var c = label.color;
        c.a = alpha;
        label.color = c;
        label.enabled = alpha > 0.01f;
        var cam = Camera.main;
        float top = 2.6f * VisualScale;
        label.transform.position = transform.position + Vector3.up * top;
        if (cam != null) label.transform.rotation = cam.transform.rotation;
    }

    // ------------------------------------------------------------ przywódca

    private void LeaderTick()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, leaderRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
        var inRange = new HashSet<EnemySpeedModifier>();
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null || !ColliderTags.IsEnemy(col)) continue;
            var eh = col.GetComponentInParent<EnemyHealth>();
            if (eh == null || eh == enemyHealth || eh.IsBoss || eh.Health.IsDead) continue;
            var mod = eh.GetComponent<EnemySpeedModifier>();
            if (mod == null) mod = eh.gameObject.AddComponent<EnemySpeedModifier>();
            if (!inRange.Add(mod)) continue;
            if (buffed.Add(mod))
            {
                mod.SetSource(leaderKey, leaderSpeedBonus);
                VoxelFx.Burst(eh.transform.position + Vector3.up * 1.2f, AffixColor, 4, 0.1f, 2f, 0.4f);
            }
        }
        toClear.Clear();
        foreach (var mod in buffed)
            if (mod == null || !inRange.Contains(mod)) toClear.Add(mod);
        foreach (var mod in toClear)
        {
            buffed.Remove(mod);
            if (mod != null) mod.SetSource(leaderKey, 1f);
        }
        // widoczny zasięg aury co 1,5 s
        if (Time.time - lastAuraRing > 1.5f)
        {
            lastAuraRing = Time.time;
            VoxelFx.VoxelRing(transform.position + Vector3.up * 0.1f, leaderRadius, AffixColor * 0.5f, 0.7f);
        }
    }

    private float lastAuraRing;

    private void ClearLeaderAura()
    {
        foreach (var mod in buffed)
            if (mod != null) mod.SetSource(leaderKey, 1f);
        buffed.Clear();
    }
}

/// <summary>
/// Wybuch zmarłej elity „Wybuchowy”: telegraf 0,6 s (okrąg na ziemi + pulsująca plama), potem obrażenia graczowi w promieniu.
/// Samodzielny obiekt (EnemyDeath niszczy wroga po 0,5 s, krócej niż telegraf), więc nie zależy od sceny ani dyrektora.
/// </summary>
public class EliteExplosion : MonoBehaviour
{
    private Vector3 center;
    private float radius, delay, elapsed;
    private int damage;
    private Color color;

    public static void Spawn(Vector3 position, float radius, int damage, float delay, Color color)
    {
        var go = new GameObject("Elite Explosion");
        go.transform.position = position;
        var e = go.AddComponent<EliteExplosion>();
        e.center = position;
        e.radius = radius;
        e.damage = damage;
        e.delay = Mathf.Max(0.1f, delay);
        e.color = color;
        Telegraph.Circle(position, radius, e.delay);
        VoxelFx.GroundGlow(position, radius, color * 0.7f, e.delay + 0.2f);
        Sfx.Play(SfxKind.Windup, position, 0.7f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < delay) return;
        Explode();
        Destroy(gameObject);
    }

    private void Explode()
    {
        var at = center + Vector3.up * 0.4f;
        VoxelFx.Flash(at, new Color(4f, 2.2f, 0.6f), 2.2f);
        VoxelFx.Burst(at, color, 28, 0.2f, 8f, 0.6f, 1f);
        VoxelFx.VoxelRing(center + Vector3.up * 0.1f, radius, color, 0.45f);
        VoxelFx.Pillar(center, color, 3f, 0.5f);
        Sfx.Play(SfxKind.Hit, center, 1f);
        if (CameraShake.Instance != null) CameraShake.Instance.ShakeScaled(damage);

        var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        Health target = null;
        foreach (var col in hits)
        {
            if (!ColliderTags.IsPlayer(col)) continue;
            target = col.GetComponentInParent<Health>();
            if (target != null) break;
        }
        if (target != null && !target.IsDead)
            target.ChangeHealth(-damage);
    }
}
