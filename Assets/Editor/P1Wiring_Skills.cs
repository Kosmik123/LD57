using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Tworzy skille AxeThrow i WarCry i dopisuje je do puli SkillSlots w Player.prefab. Idempotentne.</summary>
public static class P1Wiring_Skills
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string SkillsFolder = "Assets/Skills";

    [MenuItem("LD57/Wire P1 Skills")]
    public static void Run()
    {
        EnsureFolder(SkillsFolder);
        var axe = CreateSkill("AxeThrow", "Rzut toporem", SkillKind.AxeThrow, new[]
        {
            Tier("Baza", "Rzuć topór na 10 m: 3 obrażeń w drodze tam i 3 w drodze z powrotem.", 3, 3, 10, 0),
            Tier("Ulepszenie I", "Zasięg 13 m.", 3, 3, 13, 0),
            Tier("Ulepszenie II", "4 obrażeń.", 3, 4, 13, 0),
            Tier("Ewolucja: Dwa topory", "Rzucasz dwa topory pod kątem 15 stopni.", 3, 4, 13, 0),
        });
        var cry = CreateSkill("WarCry", "Krzyk bojowy", SkillKind.WarCry, new[]
        {
            Tier("Baza", "Odrzuca wrogów o 4 m i ogłusza na 1 s w promieniu 4 m.", 8, 0, 4, 1, 4, 1),
            Tier("Ulepszenie I", "Promień 5 m.", 8, 0, 5, 1, 4, 1),
            Tier("Ulepszenie II", "Ogłuszenie 1,5 s.", 8, 0, 5, 1, 4, 1.5f),
            Tier("Ewolucja: Ryk", "Promień 6 m i 3 obrażeń.", 8, 3, 6, 1, 4, 1.5f),
        });
        AssetDatabase.SaveAssets();

        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var slots = root.GetComponent<SkillSlots>();
            if (slots == null) { Debug.LogError("[P1Wiring_Skills] Player.prefab nie ma SkillSlots (uruchom Wire P0)"); return; }
            var so = new SerializedObject(slots);
            var pool = so.FindProperty("pool");
            if (pool == null) { Debug.LogError("[P1Wiring_Skills] brak pola 'pool'"); return; }
            foreach (var def in new[] { axe, cry })
            {
                bool has = false;
                for (int i = 0; i < pool.arraySize; i++)
                    if (pool.GetArrayElementAtIndex(i).objectReferenceValue == def) { has = true; break; }
                if (has) continue;
                pool.arraySize++;
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = def;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[P1Wiring_Skills] OK, pool: " + pool.arraySize);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.Refresh();
    }

    static SkillTier Tier(string name, string desc, float cd, int dmg, float range, float dur, float kb = 0, float stun = 0)
        => new SkillTier { TierName = name, Description = desc, Cooldown = cd, Damage = dmg, Range = range, Duration = dur, Knockback = kb, Stun = stun };

    static SkillDefinition CreateSkill(string file, string display, SkillKind kind, SkillTier[] tiers)
    {
        var path = $"{SkillsFolder}/{file}.asset";
        var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        def.DisplayName = display;
        def.Kind = kind;
        def.UsesSpaceSlot = false;
        def.Tiers = tiers;
        EditorUtility.SetDirty(def);
        return def;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
