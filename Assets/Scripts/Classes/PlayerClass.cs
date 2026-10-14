using UnityEngine;

/// <summary>Klasa postaci (Wojownik, Mag): statystyki, pula skilli, model. Assety w Assets/Classes.</summary>
[CreateAssetMenu(menuName = "LD57/Player Class", fileName = "Class")]
public class PlayerClass : ScriptableObject
{
    public string DisplayName = "Klasa";
    [TextArea] public string Description;
    [Tooltip("True: Fire1 strzela pociskiem (RangedAttack) zamiast machać mieczem.")]
    public bool Ranged;
    [Tooltip("Obrażenia ataku podstawowego dystansowego.")]
    public int AttackDamage = 1;
    [Tooltip("Odstęp między strzałami w sekundach.")]
    public float AttackCooldown = 0.35f;
    public float MoveSpeedMultiplier = 1f;
    public int MaxHealth = 50;
    [Tooltip("Pula skilli do kart awansu (ustawiana w SkillSlots).")]
    public SkillDefinition[] SkillPool;
    [Tooltip("Prefab modelu postaci pod Body. Null = zostaw obecny.")]
    public GameObject ModelPrefab;
    [Tooltip("Tint modelu (MaterialPropertyBlock _BaseColor/_Color). Biały = bez zmian.")]
    public Color Tint = Color.white;
    [Tooltip("Wyłącz obiekt SWORD w modelu.")]
    public bool HideSword;
}
