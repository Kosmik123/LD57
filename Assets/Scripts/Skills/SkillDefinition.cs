using UnityEngine;

public enum SkillKind
{
    Spin,       // Wirowanie: obszarowe wokół gracza, komora
    Charge,     // Szarża: w linii, korytarz
    GroundSlam, // Uderzenie w ziemię: spacja, skok + lądowanie
    AxeThrow,   // Rzut toporem: zasięgowy, wraca
    WarCry,     // Krzyk bojowy: odrzut + ogłuszenie
    Fireball,       // Mag: kula ognia, wybuch AoE
    FrostNova,      // Mag: lodowy pierścień wokół gracza, spowolnienie
    ArcaneOrbs,     // Mag: krążące kule
    Blink,          // Mag: teleport (spacja)
    ChainLightning, // Mag: łańcuch błyskawic
}

/// <summary>Parametry jednego poziomu skilla. Tiers[0] baza, [1] i [2] ulepszenia, [3] ewolucja.</summary>
[System.Serializable]
public class SkillTier
{
    public string TierName = "Baza";
    [TextArea] public string Description;
    public float Cooldown = 5f;
    public int Damage = 2;
    [Tooltip("Promień (Spin, GroundSlam, WarCry) albo dystans (Charge, AxeThrow) w metrach.")]
    public float Range = 3f;
    [Tooltip("Czas trwania efektu w sekundach (Spin: obrót, Charge: ślad ognia, WarCry: ogłuszenie).")]
    public float Duration = 1f;
    [Tooltip("Odrzut w metrach, 0 = brak.")]
    public float Knockback = 0f;
    [Tooltip("Czas ogłuszenia w sekundach, 0 = brak.")]
    public float Stun = 0f;
    [Tooltip("Prefab efektu wizualnego spawnowany przy użyciu. Ewolucja ma inny prefab albo inny kolor.")]
    public GameObject EffectPrefab;
}

/// <summary>
/// Definicja skilla jako asset. Jeden asset na skill, 4 tiery. Liczby z Docs/ARCHITECTURE.md sekcja Skille.
/// </summary>
[CreateAssetMenu(menuName = "LD57/Skill Definition", fileName = "Skill")]
public class SkillDefinition : ScriptableObject
{
    public string DisplayName = "Skill";
    public Sprite Icon;
    public SkillKind Kind;
    [Tooltip("True tylko dla GroundSlam: zajmuje spację, nie slot Q/E/R.")]
    public bool UsesSpaceSlot;
    [Tooltip("Dokładnie 4 elementy: baza, ulepszenie 1, ulepszenie 2, ewolucja.")]
    public SkillTier[] Tiers = new SkillTier[4];

    public const int MaxTier = 3;

    public SkillTier GetTier(int tier) => Tiers[Mathf.Clamp(tier, 0, MaxTier)];

    private void OnValidate()
    {
        if (Tiers == null || Tiers.Length != MaxTier + 1)
            System.Array.Resize(ref Tiers, MaxTier + 1);
    }
}
