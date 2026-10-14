using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Relikty gracza (komponent na Player.prefab). Trzyma listę posiadanych i aplikuje efekty do PlayerStats (klucz "relic:Nazwa").
/// Echo (szansa na natychmiastowy reset cooldownu) działa przez SkillUser.OnSkillExecuted, bez zmian w SkillUser.
/// Pula do losowania w ofercie skrzyni: pole `pool` (assety z Assets/Relics, wpisuje P12Wiring_Mechanics).
/// </summary>
public class RelicHolder : MonoBehaviour
{
    public static RelicHolder Instance { get; private set; }

    [SerializeField, Tooltip("Wszystkie relikty możliwe do znalezienia.")] private RelicDefinition[] pool;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private SkillUser skillUser;
    [SerializeField] private SkillSlots skillSlots;

    private readonly List<RelicDefinition> owned = new List<RelicDefinition>();

    public IReadOnlyList<RelicDefinition> Owned => owned;
    public IReadOnlyList<RelicDefinition> Pool => pool;

    /// <summary>Lista posiadanych zmieniła się (nowy relikt).</summary>
    public event System.Action OnChanged;

    private void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (skillUser == null) skillUser = GetComponent<SkillUser>();
        if (skillSlots == null) skillSlots = GetComponent<SkillSlots>();
    }

    private void OnEnable()
    {
        Instance = this;
        if (skillUser != null) skillUser.OnSkillExecuted += SkillUser_OnSkillExecuted;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (skillUser != null) skillUser.OnSkillExecuted -= SkillUser_OnSkillExecuted;
    }

    public bool Has(RelicDefinition def) => def != null && owned.Contains(def);

    /// <summary>Do `count` różnych reliktów jeszcze nieposiadanych, w losowej kolejności.</summary>
    public List<RelicDefinition> GetOffer(int count)
    {
        var candidates = new List<RelicDefinition>();
        if (pool != null)
            foreach (var def in pool)
                if (def != null && !owned.Contains(def)) candidates.Add(def);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);
        return candidates;
    }

    /// <summary>Dodaje relikt i aplikuje efekt. Duplikaty ignorowane.</summary>
    public void Add(RelicDefinition def)
    {
        if (def == null || owned.Contains(def)) return;
        owned.Add(def);
        Apply(def);
        OnChanged?.Invoke();
    }

    private void Apply(RelicDefinition def)
    {
        if (stats == null) return;
        string key = "relic:" + def.name;
        float v = def.Value;
        switch (def.Effect)
        {
            case RelicEffect.IronWill: stats.SetModifier(StatKind.Damage, key, 1f + v / 100f); break;
            case RelicEffect.LightFeet: stats.SetModifier(StatKind.MoveSpeed, key, 1f + v / 100f); break;
            case RelicEffect.VampireFang: stats.SetModifier(StatKind.LifestealPercent, key, v); break;
            case RelicEffect.Thorns: stats.SetModifier(StatKind.Thorns, key, v); break;
            case RelicEffect.Luck: stats.SetModifier(StatKind.CritChance, key, v / 100f); break;
            case RelicEffect.SecondWind: stats.SetModifier(StatKind.HeartBonus, key, v); break;
            case RelicEffect.Greed: stats.SetModifier(StatKind.EssenceGain, key, Mathf.Max(1f, v)); break;
            case RelicEffect.EliteHunter: stats.SetModifier(StatKind.EliteDamage, key, 1f + v / 100f); break;
            case RelicEffect.ThickSkin: stats.SetModifier(StatKind.DamageTaken, key, 1f - v / 100f); break;
            case RelicEffect.Echo: stats.SetModifier(StatKind.EchoChance, key, v / 100f); break;
        }
    }

    // ------------------------------------------------------------ Echo

    private void SkillUser_OnSkillExecuted(SkillKind kind, SkillTier tier)
    {
        if (stats == null || skillSlots == null) return;
        float chance = stats.EchoChance;
        if (chance <= 0f || Random.value >= chance) return;

        // skill, który właśnie wszedł na cooldown (SkillUser woła zdarzenie tuż po ustawieniu cooldownu)
        SkillInstance hit = null;
        foreach (var s in skillSlots.Slots)
            if (s.Definition != null && s.Definition.Kind == kind && s.CooldownRemaining > 0f) { hit = s; break; }
        if (hit == null && skillSlots.SpaceSkill != null && skillSlots.SpaceSkill.Definition.Kind == kind)
            hit = skillSlots.SpaceSkill;
        if (hit == null) return;

        hit.CooldownRemaining = 0f;
        var at = transform.position + Vector3.up * 1.2f;
        VoxelFx.Burst(at, new Color(2.2f, 1.2f, 3.2f), 10, 0.12f, 4f, 0.4f);
        VoxelFx.VoxelRing(transform.position + Vector3.up * 0.1f, 1.4f, new Color(2.2f, 1.2f, 3.2f), 0.4f);
    }
}
