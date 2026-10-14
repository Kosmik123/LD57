using System.Collections.Generic;
using UnityEngine;

/// <summary>Posiadany skill: definicja + aktualny tier (0–3) + cooldown.</summary>
public class SkillInstance
{
    public SkillDefinition Definition;
    public int Tier;
    public float CooldownRemaining;

    public SkillTier Current => Definition.GetTier(Tier);
    public bool IsMaxTier => Tier >= SkillDefinition.MaxTier;
    public bool IsReady => CooldownRemaining <= 0f;
}

/// <summary>
/// Sloty skilli gracza: 3 sloty Q/E/R (w kolejności zdobycia) + osobny slot spacji dla GroundSlam.
/// KONTRAKT (właściciel: agent Progression). Sygnatury publiczne nie zmieniają się; ciała do implementacji.
/// Używany przez LevelUpScreen (oferta kart) i HUD (ikony, cooldowny).
/// </summary>
public class SkillSlots : MonoBehaviour
{
    public const int MaxSlots = 3;
    public static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.E, KeyCode.R };
    public const KeyCode SpaceSlotKey = KeyCode.Space;

    [SerializeField, Tooltip("Pełna pula skilli do losowania kart (5 assetów).")]
    private SkillDefinition[] pool;
    public IReadOnlyList<SkillDefinition> Pool => pool;

    /// <summary>Podmienia pulę skilli (wybór klasy) i zeruje sloty Q/E/R oraz spację. Bez zdarzeń: wołać przed startem runu.</summary>
    public void SetPool(SkillDefinition[] newPool)
    {
        pool = newPool;
        slots.Clear();
        SpaceSkill = null;
        activeWheelIndex = 0;
    }

    private readonly List<SkillInstance> slots = new List<SkillInstance>(MaxSlots);
    /// <summary>Skille w slotach Q, E, R w kolejności zdobycia. Rozmiar 0–3.</summary>
    public IReadOnlyList<SkillInstance> Slots => slots;

    /// <summary>GroundSlam, jeśli zdobyty; inaczej null.</summary>
    public SkillInstance SpaceSkill { get; private set; }

    /// <summary>Wywoływane po Grant (nowy skill albo wyższy tier).</summary>
    public event System.Action<SkillInstance> OnSkillChanged;

    public bool HasFreeSlot => slots.Count < MaxSlots;

    // --- Koło szybkiego wyboru (PPM): wszystkie posiadane skille poza spacją, w kolejności zdobycia.
    private int activeWheelIndex;

    /// <summary>Skille koła (PPM): to samo co sloty Q/E/R, bez spacji. Jedna instancja = jeden cooldown.</summary>
    public IReadOnlyList<SkillInstance> Wheel => slots;

    /// <summary>Indeks aktywnego skilla koła, zawsze w granicach koła (0 gdy puste).</summary>
    public int ActiveWheelIndex
    {
        get
        {
            if (slots.Count == 0) return 0;
            return Mathf.Clamp(activeWheelIndex, 0, slots.Count - 1);
        }
    }

    /// <summary>Skill odpalany przez PPM; null gdy koło puste.</summary>
    public SkillInstance WheelSkill => slots.Count == 0 ? null : slots[ActiveWheelIndex];

    /// <summary>Zmiana aktywnego skilla koła; argument = nowy indeks.</summary>
    public event System.Action<int> OnWheelChanged;

    /// <summary>Przesuwa aktywny skill koła o dir (+1 następny, -1 poprzedni), z zawijaniem. Zwraca true, gdy indeks się zmienił.</summary>
    public bool CycleWheel(int dir)
    {
        int n = slots.Count;
        if (n <= 1 || dir == 0) return false;
        int next = ((ActiveWheelIndex + dir) % n + n) % n;
        if (next == activeWheelIndex) return false;
        activeWheelIndex = next;
        OnWheelChanged?.Invoke(next);
        return true;
    }

    public SkillInstance Find(SkillDefinition definition)
    {
        if (definition == null) return null;
        if (SpaceSkill != null && SpaceSkill.Definition == definition) return SpaceSkill;
        foreach (var s in slots)
            if (s.Definition == definition) return s;
        return null;
    }

    public bool Owns(SkillDefinition definition) => Find(definition) != null;

    /// <summary>-1 gdy nieposiadany, inaczej aktualny tier 0–3.</summary>
    public int TierOf(SkillDefinition definition) => Find(definition)?.Tier ?? -1;

    /// <summary>
    /// Daje skill: jeśli nieposiadany, zajmuje slot (spacja dla UsesSpaceSlot, inaczej pierwszy wolny Q/E/R);
    /// jeśli posiadany i poniżej MaxTier, podnosi tier o 1. Zwraca instancję albo null gdy nie da się dać.
    /// </summary>
    public SkillInstance Grant(SkillDefinition definition)
    {
        var existing = Find(definition);
        if (existing != null)
        {
            if (existing.IsMaxTier) return null;
            existing.Tier++;
            OnSkillChanged?.Invoke(existing);
            return existing;
        }

        var instance = new SkillInstance { Definition = definition, Tier = 0 };
        if (definition.UsesSpaceSlot)
        {
            if (SpaceSkill != null) return null;
            SpaceSkill = instance;
        }
        else
        {
            if (!HasFreeSlot) return null;
            slots.Add(instance);
        }
        OnSkillChanged?.Invoke(instance);
        return instance;
    }

    /// <summary>
    /// Karty możliwe do zaoferowania teraz: nieposiadane skille (gdy jest wolny odpowiedni slot)
    /// oraz posiadane poniżej MaxTier. Z tej listy LevelUpScreen losuje 3 różne (lub mniej, gdy brakuje).
    /// </summary>
    public List<SkillDefinition> GetOfferableSkills()
    {
        var result = new List<SkillDefinition>();
        if (pool == null) return result;
        foreach (var def in pool)
        {
            if (def == null) continue;
            var owned = Find(def);
            if (owned == null)
            {
                bool slotAvailable = def.UsesSpaceSlot ? SpaceSkill == null : HasFreeSlot;
                if (slotAvailable) result.Add(def);
            }
            else if (!owned.IsMaxTier)
            {
                result.Add(def);
            }
        }
        return result;
    }
}
