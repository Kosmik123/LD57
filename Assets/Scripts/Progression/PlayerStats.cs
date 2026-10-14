using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum StatKind
{
    // mnożniki (iloczyn źródeł, 1 = bez zmian)
    Damage, MoveSpeed, DamageTaken, EliteDamage, EssenceGain,
    // addytywne (suma źródeł, 0 = bez zmian)
    CritChance, LifestealPercent, Thorns, HeartBonus, EchoChance,
}

/// <summary>
/// Hub statystyk gracza (komponent na Player.prefab, singleton `Instance`). Modyfikatory są kluczowane źródłem
/// (relikt, Furia, spowolnienie lodowej elity), więc się nie nadpisują; chwilowe wygasają po czasie gry (Time.time,
/// pauza je zatrzymuje). Wszystko liczone w kodzie, bez zmian w cudzych plikach:
/// - prędkość: refleksją na PlayerController.moveSpeed (jak PlayerClassApplier), z odtwarzaniem bazy, gdy ktoś inny
///   nadpisze pole (zmiana klasy w menu);
/// - obrażenia gracza: SkillHit.Apply i DamagingBlade wołają ScaleDamage (kryt, mnożnik, Łowca elit);
/// - lifesteal: z EnemyHealth.OnEnemyDamaged (akumulator ułamków);
/// - kolce, redukcja obrażeń, bonus serca: z Health.OnHealthChanged gracza.
/// Obrażenia są liczbami całkowitymi, więc ułamki zaokrąglane są losowo (średnio wychodzi dokładnie).
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    /// <summary>Gracz stracił HP (wartość dodatnia). Dla lodowej elity itp.</summary>
    public static event System.Action<int> OnPlayerHurt;

    private class Mod
    {
        public StatKind Kind;
        public string Key;
        public float Value;
        public float Expires; // < 0 = stały
    }

    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField, Tooltip("Mnożnik krytyka.")] private float critMultiplier = 2f;
    [SerializeField, Tooltip("Promień szukania napastnika dla kolców (m).")] private float thornsRadius = 3f;
    [SerializeField, Tooltip("Maks. odległość serca od gracza przy wykryciu podniesienia (m).")] private float heartDetectRadius = 3f;
    [SerializeField, Tooltip("XP za jedną sztukę esencji.")] private int xpPerEssence = 2;

    private readonly List<Mod> mods = new List<Mod>();
    private Health health;
    private PlayerController controller;
    private Experience experience;
    private FieldInfo moveField;
    private float baseMoveSpeed, lastWrittenSpeed;
    private bool moveInitialized;
    private float lifestealBuffer;
    private bool inRefund;
    private static readonly Collider[] buffer = new Collider[32];

    public int Essence { get; private set; }
    public float CritMultiplier => critMultiplier;

    /// <summary>Wartość zmieniła się (modyfikator, esencja).</summary>
    public event System.Action OnChanged;
    /// <summary>Zebrano esencję (ilość po mnożniku).</summary>
    public event System.Action<int> OnEssenceCollected;

    // ------------------------------------------------------------ odczyt

    public float DamageMultiplier => Product(StatKind.Damage);
    public float MoveSpeedMultiplier => Mathf.Max(0.25f, Product(StatKind.MoveSpeed));
    public float DamageTakenMultiplier => Mathf.Clamp(Product(StatKind.DamageTaken), 0.1f, 2f);
    public float EliteDamageMultiplier => Product(StatKind.EliteDamage);
    public float EssenceMultiplier => Product(StatKind.EssenceGain);
    public float CritChance => Mathf.Clamp01(Sum(StatKind.CritChance));
    public float LifestealPercent => Sum(StatKind.LifestealPercent);
    public float ThornsDamage => Sum(StatKind.Thorns);
    public float HeartBonusHeal => Sum(StatKind.HeartBonus);
    public float EchoChance => Mathf.Clamp01(Sum(StatKind.EchoChance));

    public float Get(StatKind kind) => IsMultiplicative(kind) ? Product(kind) : Sum(kind);

    private static bool IsMultiplicative(StatKind kind) =>
        kind == StatKind.Damage || kind == StatKind.MoveSpeed || kind == StatKind.DamageTaken
        || kind == StatKind.EliteDamage || kind == StatKind.EssenceGain;

    private float Product(StatKind kind)
    {
        float result = 1f;
        for (int i = 0; i < mods.Count; i++)
            if (mods[i].Kind == kind) result *= mods[i].Value;
        return result;
    }

    private float Sum(StatKind kind)
    {
        float result = 0f;
        for (int i = 0; i < mods.Count; i++)
            if (mods[i].Kind == kind) result += mods[i].Value;
        return result;
    }

    // ------------------------------------------------------------ zapis

    /// <summary>Ustawia (lub zastępuje) modyfikator źródła `key`. seconds &gt; 0 = chwilowy.</summary>
    public void SetModifier(StatKind kind, string key, float value, float seconds = 0f)
    {
        Mod mod = null;
        for (int i = 0; i < mods.Count; i++)
            if (mods[i].Kind == kind && mods[i].Key == key) { mod = mods[i]; break; }
        if (mod == null)
        {
            mod = new Mod { Kind = kind, Key = key };
            mods.Add(mod);
        }
        mod.Value = value;
        mod.Expires = seconds > 0f ? Time.time + seconds : -1f;
        OnChanged?.Invoke();
    }

    public void RemoveModifier(StatKind kind, string key)
    {
        if (mods.RemoveAll(m => m.Kind == kind && m.Key == key) > 0)
            OnChanged?.Invoke();
    }

    /// <summary>Usuwa wszystkie modyfikatory, których klucz zaczyna się od prefiksu (np. "relic:").</summary>
    public void RemoveByPrefix(string prefix)
    {
        if (mods.RemoveAll(m => m.Key.StartsWith(prefix)) > 0)
            OnChanged?.Invoke();
    }

    public bool HasModifier(StatKind kind, string key)
    {
        for (int i = 0; i < mods.Count; i++)
            if (mods[i].Kind == kind && mods[i].Key == key) return true;
        return false;
    }

    // ------------------------------------------------------------ obrażenia gracza

    /// <summary>Losowe zaokrąglenie: 1,3 daje 1 (70%) lub 2 (30%), więc średnia jest dokładna.</summary>
    public static int StochasticRound(float value)
    {
        if (value <= 0f) return 0;
        int whole = Mathf.FloorToInt(value);
        return whole + (Random.value < value - whole ? 1 : 0);
    }

    /// <summary>
    /// Obrażenia gracza po modyfikatorach: DamageMultiplier, Łowca elit (cel z EliteModifier), kryt. Zawsze ≥ 1 dla baseDamage &gt; 0.
    /// Bez PlayerStats w scenie zwraca baseDamage.
    /// </summary>
    public static int Scale(int baseDamage, Health target)
    {
        if (Instance == null) return baseDamage;
        return Instance.ScaleDamage(baseDamage, target, out _);
    }

    public int ScaleDamage(int baseDamage, Health target, out bool crit)
    {
        crit = false;
        if (baseDamage <= 0) return baseDamage;

        float mult = DamageMultiplier;
        if (target != null && target.TryGetComponent<EliteModifier>(out _))
            mult *= EliteDamageMultiplier;
        float chance = CritChance;
        if (chance > 0f && Random.value < chance)
        {
            crit = true;
            mult *= critMultiplier;
        }
        int result = Mathf.Max(1, StochasticRound(baseDamage * mult));
        if (crit && target != null)
        {
            var at = target.transform.position + Vector3.up * 0.9f;
            VoxelFx.Burst(at, new Color(3.2f, 2.3f, 0.7f), 8, 0.12f, 4.5f, 0.35f);
            VoxelFx.Flash(at, new Color(3.2f, 2.6f, 1f), 0.5f);
        }
        return result;
    }

    // ------------------------------------------------------------ esencja

    /// <summary>Zebranie esencji: licznik (po mnożniku Zachłanności) i XP. Zwraca ilość po mnożniku.</summary>
    public int CollectEssence(int amount)
    {
        int gained = Mathf.Max(0, Mathf.RoundToInt(amount * EssenceMultiplier));
        if (gained <= 0) return 0;
        Essence += gained;
        if (experience == null) experience = Object.FindFirstObjectByType<Experience>();
        if (experience != null) experience.AddXp(gained * xpPerEssence);
        OnEssenceCollected?.Invoke(gained);
        OnChanged?.Invoke();
        return gained;
    }

    // ------------------------------------------------------------ cykl życia

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        moveField = typeof(PlayerController).GetField("moveSpeed", Priv);
    }

    private void OnEnable()
    {
        Instance = this;
        EnemyHealth.OnEnemyDamaged += EnemyHealth_OnEnemyDamaged;
        if (health != null) health.OnHealthChanged += Health_OnHealthChanged;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        EnemyHealth.OnEnemyDamaged -= EnemyHealth_OnEnemyDamaged;
        if (health != null) health.OnHealthChanged -= Health_OnHealthChanged;
        RestoreMoveSpeed();
    }

    private void Start()
    {
        experience = Object.FindFirstObjectByType<Experience>();
    }

    private void Update()
    {
        if (mods.Count > 0)
        {
            bool expired = false;
            for (int i = mods.Count - 1; i >= 0; i--)
            {
                if (mods[i].Expires >= 0f && Time.time >= mods[i].Expires)
                {
                    mods.RemoveAt(i);
                    expired = true;
                }
            }
            if (expired) OnChanged?.Invoke();
        }
        ApplyMoveSpeed();
    }

    // ------------------------------------------------------------ prędkość

    private void ApplyMoveSpeed()
    {
        if (controller == null || moveField == null) return;
        float current = (float)moveField.GetValue(controller);
        // ktoś inny (PlayerClassApplier po zmianie klasy) zapisał pole: to jest nowa baza
        if (!moveInitialized || !Mathf.Approximately(current, lastWrittenSpeed))
        {
            baseMoveSpeed = current;
            moveInitialized = true;
        }
        float target = baseMoveSpeed * MoveSpeedMultiplier;
        if (!Mathf.Approximately(current, target))
            moveField.SetValue(controller, target);
        lastWrittenSpeed = target;
    }

    private void RestoreMoveSpeed()
    {
        if (controller == null || moveField == null || !moveInitialized) return;
        float current = (float)moveField.GetValue(controller);
        if (Mathf.Approximately(current, lastWrittenSpeed))
            moveField.SetValue(controller, baseMoveSpeed);
        moveInitialized = false;
    }

    // ------------------------------------------------------------ zdarzenia walki

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth enemy, int amount)
    {
        float percent = LifestealPercent;
        if (percent <= 0f || health == null || health.IsDead) return;
        lifestealBuffer += amount * percent / 100f;
        if (lifestealBuffer < 1f) return;
        int heal = Mathf.FloorToInt(lifestealBuffer);
        lifestealBuffer -= heal;
        if (health.CurrentHealth < health.MaxHealth)
        {
            bool was = inRefund;
            inRefund = true;
            health.ChangeHealth(heal);
            inRefund = was;
            VoxelFx.Burst(transform.position + Vector3.up, new Color(3.2f, 0.4f, 0.5f), 4, 0.1f, 2.5f, 0.35f);
        }
    }

    private void Health_OnHealthChanged(int change)
    {
        if (inRefund) return;

        if (change < 0)
        {
            int damage = -change;
            // redukcja obrażeń: zwrot części HP (działa też na cios, który zabiłby gracza: OnDied sprawdza HP po zdarzeniu)
            float taken = DamageTakenMultiplier;
            if (taken < 1f)
            {
                int refund = StochasticRound(damage * (1f - taken));
                if (refund > 0)
                {
                    inRefund = true;
                    health.ChangeHealth(refund);
                    inRefund = false;
                }
            }
            ApplyThorns();
            OnPlayerHurt?.Invoke(damage);
        }
        else if (change > 0 && HeartBonusHeal > 0f && HeartNearby())
        {
            int bonus = StochasticRound(HeartBonusHeal);
            if (bonus > 0)
            {
                inRefund = true;
                health.ChangeHealth(bonus);
                inRefund = false;
            }
        }
    }

    private void ApplyThorns()
    {
        int thorns = StochasticRound(ThornsDamage);
        if (thorns <= 0) return;
        int n = Physics.OverlapSphereNonAlloc(transform.position, thornsRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
        Health nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null || !ColliderTags.IsEnemy(col)) continue;
            var h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || h == health) continue;
            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = h; }
        }
        if (nearest == null) return;
        nearest.ChangeHealth(-thorns);
        var at = nearest.transform.position + Vector3.up * 0.8f;
        VoxelFx.Burst(at, new Color(3.2f, 1.5f, 0.4f), 6, 0.1f, 4f, 0.3f);
    }

    /// <summary>Serce w pobliżu gracza w chwili leczenia (HeartPickup niszczy się dopiero po zdarzeniu).</summary>
    private bool HeartNearby()
    {
        var hearts = Object.FindObjectsByType<HeartPickup>(FindObjectsSortMode.None);
        foreach (var heart in hearts)
        {
            if (heart == null) continue;
            var d = heart.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= heartDetectRadius * heartDetectRadius) return true;
        }
        return false;
    }
}
