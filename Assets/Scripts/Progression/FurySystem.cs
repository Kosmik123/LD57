using TMPro;
using UnityEngine;

/// <summary>
/// Furia (komponent na Player.prefab). Zabójstwa liczą się w oknie 4 s (każde odświeża okno); przy 8 zabójstwach w oknie
/// włącza się Furia na 6 s: +35% obrażeń, +20% prędkości (modyfikatory PlayerStats), czerwono-złota aura, napis „FURIA”.
/// Po Furii 10 s przerwy, w której zabójstwa się nie liczą. HUD: FuryMeter. Czas gry (Time.time), więc pauza zatrzymuje liczniki.
/// </summary>
public class FurySystem : MonoBehaviour
{
    public static FurySystem Instance { get; private set; }

    private const string Key = "fury";
    private static readonly Color Gold = new Color(3.2f, 2.0f, 0.5f);
    private static readonly Color Red = new Color(3.6f, 0.55f, 0.3f);

    [SerializeField] private PlayerStats stats;
    [SerializeField] private int killsRequired = 8;
    [SerializeField, Tooltip("Okno zabójstw w sekundach (każde zabójstwo je odświeża).")] private float windowSeconds = 4f;
    [SerializeField] private float duration = 6f;
    [SerializeField] private float cooldown = 10f;
    [SerializeField, Tooltip("Mnożnik obrażeń w Furii (1,35 = +35%).")] private float damageMultiplier = 1.35f;
    [SerializeField, Tooltip("Mnożnik prędkości w Furii (1,2 = +20%).")] private float speedMultiplier = 1.2f;

    private int kills;
    private float lastKillTime = -999f;
    private float activeUntil = -1f, cooldownUntil = -1f;
    private float sparkTimer;
    private bool active;

    public int Kills => kills;
    public int KillsRequired => killsRequired;
    public bool IsActive => active;
    public bool OnCooldown => !active && Time.time < cooldownUntil;
    /// <summary>Ile okna zabójstw zostało (1 = świeże zabójstwo, 0 = okno minęło).</summary>
    public float WindowRemaining01 => kills <= 0 ? 0f : Mathf.Clamp01(1f - (Time.time - lastKillTime) / windowSeconds);
    public float ActiveRemaining01 => active ? Mathf.Clamp01((activeUntil - Time.time) / duration) : 0f;
    /// <summary>Postęp przerwy po Furii: 0 = dopiero się skończyła, 1 = gotowa.</summary>
    public float CooldownProgress01 => OnCooldown ? 1f - Mathf.Clamp01((cooldownUntil - Time.time) / cooldown) : 1f;

    /// <summary>Zmienił się licznik zabójstw lub stan (HUD).</summary>
    public event System.Action OnChanged;
    public event System.Action OnActivated;
    public event System.Action OnEnded;

    private void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
    }

    private void OnEnable()
    {
        Instance = this;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
        GameEvents.OnPlayerDied += GameEvents_OnPlayerDied;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
        GameEvents.OnPlayerDied -= GameEvents_OnPlayerDied;
        if (active) End(false);
    }

    private void GameEvents_OnPlayerDied()
    {
        kills = 0;
        if (active) End(false);
        OnChanged?.Invoke();
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (active || OnCooldown) return;
        if (Time.time - lastKillTime > windowSeconds) kills = 0;
        kills++;
        lastKillTime = Time.time;
        if (kills >= killsRequired) Activate();
        OnChanged?.Invoke();
    }

    private void Update()
    {
        if (active)
        {
            if (Time.time >= activeUntil)
            {
                End(true);
                return;
            }
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.12f;
                var p = transform.position + Vector3.up * 0.2f + Random.insideUnitSphere * 0.5f;
                VoxelFx.Emit(p, Vector3.up * Random.Range(1.5f, 3f), Random.value < 0.5f ? Gold : Red, Random.Range(0.08f, 0.16f), 0.5f);
            }
        }
        else if (kills > 0 && Time.time - lastKillTime > windowSeconds)
        {
            kills = 0;
            OnChanged?.Invoke();
        }
    }

    private void Activate()
    {
        active = true;
        kills = 0;
        activeUntil = Time.time + duration;
        sparkTimer = 0f;
        if (stats != null)
        {
            stats.SetModifier(StatKind.Damage, Key, damageMultiplier, duration);
            stats.SetModifier(StatKind.MoveSpeed, Key, speedMultiplier, duration);
        }

        VoxelOrbit.Attach(transform, Gold, 10, 1.15f, 0.9f, duration, 0.17f, 1.6f);
        VoxelOrbit.Attach(transform, Red, 6, 0.8f, 1.3f, duration, 0.14f, -2.1f);
        VoxelFx.GroundGlow(transform, 2.2f, Red, duration);
        var pos = transform.position;
        VoxelFx.Burst(pos + Vector3.up, Gold, 24, 0.16f, 7f, 0.55f);
        VoxelFx.VoxelRing(pos + Vector3.up * 0.1f, 3f, Red, 0.5f);
        VoxelFx.Pillar(pos, Gold, 3.5f, 0.6f);
        WorldPopup.Show(pos + Vector3.up * 2.4f, "FURIA", new Color(1f, 0.55f, 0.12f), 11f, 1.4f);
        if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.45f, 0.1f, 1f));
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f, 0.2f);
        Sfx.Play(SfxKind.LevelUp, pos, 0.8f);
        OnActivated?.Invoke();
    }

    private void End(bool withCooldown)
    {
        active = false;
        cooldownUntil = withCooldown ? Time.time + cooldown : -1f;
        if (stats != null)
        {
            stats.RemoveModifier(StatKind.Damage, Key);
            stats.RemoveModifier(StatKind.MoveSpeed, Key);
        }
        OnEnded?.Invoke();
        OnChanged?.Invoke();
    }
}

/// <summary>Napis w świecie (TMP 3D): unosi się, patrzy w kamerę i gaśnie. Niszczy się sam.</summary>
public class WorldPopup : MonoBehaviour
{
    private TextMeshPro text;
    private float life, elapsed, rise;
    private Vector3 start;

    public static void Show(Vector3 position, string message, Color color, float fontSize = 8f, float seconds = 1.2f, float rise = 1.2f)
    {
        var go = new GameObject("World Popup");
        go.transform.position = position;
        var p = go.AddComponent<WorldPopup>();
        p.text = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) p.text.font = TMP_Settings.defaultFontAsset;
        p.text.text = message;
        p.text.fontSize = fontSize;
        p.text.fontStyle = FontStyles.Bold;
        p.text.alignment = TextAlignmentOptions.Center;
        p.text.color = color;
        p.life = Mathf.Max(0.2f, seconds);
        p.rise = rise;
        p.start = position;
        p.Face();
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= life) { Destroy(gameObject); return; }
        float t = elapsed / life;
        transform.position = start + Vector3.up * (rise * (1f - (1f - t) * (1f - t)));
        float scale = t < 0.15f ? Mathf.Lerp(0.4f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
        transform.localScale = Vector3.one * scale;
        var c = text.color;
        c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        text.color = c;
        Face();
    }

    private void Face()
    {
        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
