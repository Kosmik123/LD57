using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reżyser nowych mechanik w scenie (jeden obiekt "Elite Director"):
/// - co `scanInterval` skanuje nowych wrogów (WaveZone i LevelPopulation nie mają wspólnego zdarzenia spawnu) i z szansą rosnącą
///   z poziomem (6% na poziomie 1, 18% na 8) nadaje im EliteModifier z jednym afiksem; nigdy bossowi;
/// - słucha EnemyHealth.OnEnemyDied i wyrzuca esencję (zwykły wróg 35% na 1 szt., elita 3 szt. + 20% szans na serce);
/// - po OnLevelStarted stawia na poziomie 1-2 skrzynie z reliktami (TreasureChest.PlaceOnLevel).
/// Testy dymne ustawiają DisabledForTests = true: bez elit i bez skrzyń (skrzynia pauzuje grę ekranem wyboru).
/// </summary>
public class EliteDirector : MonoBehaviour
{
    /// <summary>Testy dymne: true = brak elit i skrzyń (esencja nadal wypada, nie wpływa na liczenie wrogów).</summary>
    public static bool DisabledForTests;

    public static EliteDirector Instance { get; private set; }

    [SerializeField, Tooltip("Prefab serca (HeartPickup) wypadającego z elity.")] private GameObject heartPrefab;
    [SerializeField] private float scanInterval = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("Szansa elity na poziomie 1.")] private float eliteChanceLevel1 = 0.06f;
    [SerializeField, Range(0f, 1f), Tooltip("Szansa elity na poziomie 8.")] private float eliteChanceLevel8 = 0.18f;
    [SerializeField, Tooltip("Maks. elit żywych naraz.")] private int maxAliveElites = 3;
    [SerializeField, Range(0f, 1f)] private float essenceDropChance = 0.35f;
    [SerializeField] private int eliteEssence = 3;
    [SerializeField, Range(0f, 1f)] private float eliteHeartChance = 0.2f;
    [SerializeField, Tooltip("Skrzyń na poziom: min..max (włącznie).")] private Vector2Int chestsPerLevel = new Vector2Int(1, 2);
    [SerializeField, Tooltip("Opóźnienie stawiania skrzyń po starcie poziomu (poziom i NavMesh muszą być gotowe).")] private float chestDelay = 1.2f;

    private readonly HashSet<EnemyHealth> known = new HashSet<EnemyHealth>();
    private readonly List<EnemyHealth> toForget = new List<EnemyHealth>();
    private float scanTimer;

    /// <summary>Szansa elity na danym poziomie (1–8), liniowo.</summary>
    public float EliteChance(int level)
    {
        float t = Mathf.Clamp01((Mathf.Max(1, level) - 1) / 7f);
        return Mathf.Lerp(eliteChanceLevel1, eliteChanceLevel8, t);
    }

    private void OnEnable()
    {
        Instance = this;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
        GameEvents.OnLevelStarted += GameEvents_OnLevelStarted;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
        GameEvents.OnLevelStarted -= GameEvents_OnLevelStarted;
    }

    // ------------------------------------------------------------ elity

    private void Update()
    {
        if (DisabledForTests) return;
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;
        Scan();
    }

    private void Scan()
    {
        // zapomnij zniszczonych
        toForget.Clear();
        foreach (var e in known)
            if (e == null) toForget.Add(e);
        foreach (var e in toForget) known.Remove(e);

        var all = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 1;
        float chance = EliteChance(level);
        foreach (var enemy in all)
        {
            if (enemy == null || !known.Add(enemy)) continue;
            if (enemy.IsBoss || enemy.Health == null || enemy.Health.IsDead) continue;
            if (enemy.GetComponent<EliteModifier>() != null) continue;
            if (EliteModifier.AliveCount >= maxAliveElites) continue;
            if (Random.value >= chance) continue;
            MakeElite(enemy);
        }
    }

    /// <summary>Nadaje wrogowi losowy afiks (też wołane ręcznie, np. z testów: affix = null losuje).</summary>
    public static EliteModifier MakeElite(EnemyHealth enemy, EliteAffix? affix = null)
    {
        if (enemy == null || enemy.IsBoss) return null;
        var existing = enemy.GetComponent<EliteModifier>();
        if (existing != null) return existing;
        var chosen = affix ?? (EliteAffix)Random.Range(0, System.Enum.GetValues(typeof(EliteAffix)).Length);
        var mod = enemy.gameObject.AddComponent<EliteModifier>();
        mod.Apply(chosen);
        return mod;
    }

    // ------------------------------------------------------------ drop

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsBoss) return;
        var pos = enemy.transform.position;
        bool elite = enemy.TryGetComponent<EliteModifier>(out var mod) && mod.IsApplied;
        if (elite)
        {
            EssencePickup.Drop(pos, eliteEssence);
            if (heartPrefab != null && Random.value < eliteHeartChance)
                Instantiate(heartPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
            VoxelFx.Burst(pos + Vector3.up, mod.AffixColor, 14, 0.16f, 6f, 0.5f, 1f);
        }
        else if (Random.value < essenceDropChance)
        {
            EssencePickup.Drop(pos, 1);
        }
    }

    // ------------------------------------------------------------ skrzynie

    private void GameEvents_OnLevelStarted(int level)
    {
        if (DisabledForTests) return;
        StartCoroutine(PlaceChests(level));
    }

    private IEnumerator PlaceChests(int level)
    {
        yield return new WaitForSeconds(chestDelay);
        if (DisabledForTests) yield break;
        var manager = LevelManager.Instance;
        if (manager == null || manager.CurrentLevel != level || manager.CurrentInstance == null) yield break;
        int count = Random.Range(chestsPerLevel.x, chestsPerLevel.y + 1);
        TreasureChest.PlaceOnLevel(manager.CurrentInstance, count);
    }
}
