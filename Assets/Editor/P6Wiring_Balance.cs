using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Balans po playteście (14 października 2026): hordy wrogów, niższe HP mięsa armatniego, XP przeskalowane,
/// mocniejszy odrzut i komponent Knockback na wrogach. Idempotentne; nie rusza pozycji stref ani punktów spawnu,
/// tylko pola Count w istniejących wpisach WaveZone. Menu: LD57 > Wire P6 Balance.
/// </summary>
public static class P6Wiring_Balance
{
    const string SlimePath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string SpiderPath = "Assets/Prefabs/Enemies/Spider.prefab";
    const string ShooterPath = "Assets/Prefabs/Enemies/Shooter.prefab";
    const string BossPath = "Assets/Prefabs/Enemies/Boss Spider Mother.prefab";
    const string EnemyBasePath = "Assets/Prefabs/Enemies/_Enemy Base.prefab";
    const string PlayerPath = "Assets/Prefabs/Player.prefab";

    // (slime, pająk, strzelec) na poziom; rozdzielane po strefach jak w P1Wiring_Levels (reszta do ostatnich stref)
    static readonly Dictionary<int, (int slime, int spider, int shooter)> Composition = new Dictionary<int, (int, int, int)>
    {
        { 1, (24, 2, 0) }, { 2, (32, 0, 0) }, { 3, (30, 9, 0) }, { 4, (33, 12, 0) },
        { 5, (30, 12, 6) }, { 6, (33, 15, 6) }, { 7, (36, 18, 9) }, { 8, (36, 18, 9) },
    };

    [MenuItem("LD57/Wire P6 Balance")]
    public static void Run()
    {
        Debug.Log("[P6Balance] start");
        WireEnemyStats();
        WireWaveCounts();
        WirePlayerKnockback();
        AssetDatabase.SaveAssets();
        Debug.Log("[P6Balance] done");
    }

    static void WireEnemyStats()
    {
        // HP i XP: slime 3 HP / 4 XP, pająk 4 HP / 9 XP, strzelec 4 HP / 12 XP; boss bez zmian
        SetStats(EnemyBasePath, -1, 4, 1f);
        SetStats(SlimePath, 3, 5, 1f);
        WireSlimeLeap();
        SetStats(SpiderPath, 4, 9, 0.8f);
        SetStats(ShooterPath, 4, 12, 1f);
        SetStats(BossPath, -1, -1, 0.15f);
    }

    static void SetStats(string path, int hp, int xp, float knockbackResistance)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var health = root.GetComponent<Health>();
            var eh = root.GetComponent<EnemyHealth>();
            if (health != null && hp > 0) SetInt(health, "maxHealth", hp);
            if (eh != null && xp >= 0) SetInt(eh, "experienceReward", xp);
            var kb = root.GetComponent<Knockback>();
            if (kb == null) kb = root.AddComponent<Knockback>();
            SetFloat(kb, "resistance", knockbackResistance);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[P6Balance] {System.IO.Path.GetFileNameWithoutExtension(path)}: HP {(hp > 0 ? hp.ToString() : "bez zmian")}, XP {(xp >= 0 ? xp.ToString() : "bez zmian")}, Knockback {knockbackResistance}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WireSlimeLeap()
    {
        var root = PrefabUtility.LoadPrefabContents(SlimePath);
        try
        {
            var melee = root.GetComponent<EnemyMeleeAttack>();
            if (melee != null)
            {
                SetBool(melee, "leap", true);
                SetFloat(melee, "windup", 0.5f);
                SetFloat(melee, "range", 3.5f);
                SetFloat(melee, "cooldown", 1.6f);
                SetFloat(melee, "leapSeconds", 0.35f);
                SetFloat(melee, "landRadius", 1.3f);
                SetInt(melee, "leapDamage", 10);
            }
            var ctrl = root.GetComponent<SlimeController>();
            if (ctrl != null) SetFloat(ctrl, "lungeDistance", 3.5f);
            PrefabUtility.SaveAsPrefabAsset(root, SlimePath);
            Debug.Log("[P6Balance] Slime Enemy: skok (leap) z 3,5 m, windup 0,5, cooldown 1,6");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WireWaveCounts()
    {
        var slime = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePath);
        var spider = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);
        var shooter = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPath);
        for (int level = 1; level <= 8; level++)
        {
            string path = $"Assets/Prefabs/Levels/Level {level}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var zones = root.GetComponentsInChildren<WaveZone>(true).OrderBy(z => z.name).ToArray();
                if (zones.Length == 0) { Debug.LogWarning($"[P6Balance] Level {level}: brak stref, pomijam"); continue; }
                var comp = Composition[level];
                int total = 0;
                for (int z = 0; z < zones.Length; z++)
                {
                    var so = new SerializedObject(zones[z]);
                    var maxAlive = so.FindProperty("maxAlive");
                    if (maxAlive != null) maxAlive.intValue = 7 + level; // 8 na poziomie 1 … 15 na poziomie 8
                    var arr = so.FindProperty("entries");
                    for (int i = 0; i < arr.arraySize; i++)
                    {
                        var e = arr.GetArrayElementAtIndex(i);
                        var prefab = e.FindPropertyRelative("EnemyPrefab").objectReferenceValue as GameObject;
                        var count = e.FindPropertyRelative("Count");
                        if (prefab == slime) count.intValue = Share(comp.slime, zones.Length, z);
                        else if (prefab == spider) count.intValue = Share(comp.spider, zones.Length, z);
                        else if (prefab == shooter) count.intValue = Share(comp.shooter, zones.Length, z);
                        total += count.intValue;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[P6Balance] Level {level}: {zones.Length} strefy, wrogów łącznie {total}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static void WirePlayerKnockback()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            foreach (var blade in root.GetComponentsInChildren<DamagingBlade>(true))
                SetFloat(blade, "knockback", 0.9f);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log("[P6Balance] Player.prefab: knockback 0.9");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static int Share(int n, int zones, int z)
    {
        int basePart = n / zones, rest = n % zones;
        return basePart + (z >= zones - rest ? 1 : 0);
    }

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P6Balance] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetInt(Object target, string field, int value) => SetProp(target, field, p => p.intValue = value);
    static void SetBool(Object target, string field, bool value) => SetProp(target, field, p => p.boolValue = value);
    static void SetFloat(Object target, string field, float value) => SetProp(target, field, p => p.floatValue = value);
}
