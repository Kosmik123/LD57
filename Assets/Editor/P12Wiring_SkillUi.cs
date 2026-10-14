using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Podpięcie UI skilli P12 (Docs/WIRING-p12-skillui.md): PNG ikon do Assets/Graphics/Icons, pole Icon w assetach
/// SkillDefinition, obiekt `Skill Wheel` (koło szybkiego wyboru pod PPM) w `Game UI`.
/// Menu: LD57 > Wire P12 Skill UI. Idempotentne: istniejące PNG i ręcznie wstawione Icon zostają, wariant NADPISZ
/// generuje ikony od nowa.
/// </summary>
public static class P12Wiring_SkillUi
{
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string IconDir = "Assets/Graphics/Icons";

    static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    [MenuItem("LD57/Wire P12 Skill UI")]
    public static void Run() => Execute(false);

    [MenuItem("LD57/Wire P12 Skill UI (NADPISZ ikony)")]
    public static void RunOverwriteIcons() => Execute(true);

    static void Execute(bool overwrite)
    {
        var sprites = GenerateIcons(overwrite);
        AssignIcons(sprites, overwrite);
        WireScene();
    }

    // ---------------------------------------------------------------- ikony

    static Sprite[] GenerateIcons(bool overwrite)
    {
        Directory.CreateDirectory(IconDir);
        var kinds = (SkillKind[])System.Enum.GetValues(typeof(SkillKind));
        var result = new Sprite[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            string path = $"{IconDir}/{kinds[i]}.png";
            bool exists = File.Exists(path);
            if (overwrite || !exists)
            {
                var tex = SkillIcons.CreateTexture(kinds[i]);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.ImportAsset(path);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && NeedsSetup(importer))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 128;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result[i] == null) Debug.LogError($"[P12Wiring_SkillUi] nie wczytano sprite'a {path}");
        }
        Debug.Log($"[P12Wiring_SkillUi] ikony: {kinds.Length} w {IconDir} (nadpisane: {overwrite})");
        return result;
    }

    static bool NeedsSetup(TextureImporter t) =>
        t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single
        || t.mipmapEnabled || !t.alphaIsTransparency || t.textureCompression != TextureImporterCompression.Uncompressed;

    static void AssignIcons(Sprite[] sprites, bool overwrite)
    {
        var kinds = (SkillKind[])System.Enum.GetValues(typeof(SkillKind));
        int assigned = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/Skills" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (def == null) continue;
            int index = System.Array.IndexOf(kinds, def.Kind);
            if (index < 0 || sprites[index] == null) continue;

            // ręcznie wstawiona ikona (spoza Assets/Graphics/Icons) zostaje
            bool ours = def.Icon != null && AssetDatabase.GetAssetPath(def.Icon).StartsWith(IconDir);
            if (def.Icon != null && !(overwrite && ours)) continue;

            var so = new SerializedObject(def);
            so.FindProperty("Icon").objectReferenceValue = sprites[index];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            assigned++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[P12Wiring_SkillUi] Icon wpięte w {assigned} assetach SkillDefinition");
    }

    // ---------------------------------------------------------------- scena

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        var canvasGo = GameObject.Find("Game UI");
        if (player == null || canvasGo == null)
        {
            Debug.LogError("[P12Wiring_SkillUi] brak 'Player' lub 'Game UI' (uruchom najpierw LD57 > Wire P0)");
            return;
        }

        // Pod `Skill HUD`, żeby menu startowe (ukrywa Skill HUD) chowało też koło.
        var hud = canvasGo.transform.Find("Skill HUD");
        var parent = hud != null ? hud : canvasGo.transform;

        var existing = parent.Find("Skill Wheel");
        GameObject go;
        bool created = existing == null;
        if (created)
        {
            go = new GameObject("Skill Wheel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
        }
        else go = existing.gameObject;

        var rt = (RectTransform)go.transform;
        if (created)
        {
            // Na prawo od slotów Q/E/R/SPC, na wysokości ich środka (układ z P0Wiring: slot 80 px, rząd 368 px).
            float y = 85f;
            if (hud != null)
            {
                var row = hud.Find("Slots") as RectTransform;
                if (row != null) y = row.anchoredPosition.y + row.sizeDelta.y * 0.5f;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 72f);
            rt.anchoredPosition = new Vector2(330f, y);
        }

        var wheel = go.GetComponent<SkillWheelHud>();
        if (wheel == null) wheel = go.AddComponent<SkillWheelHud>();
        wheel.BuildIfNeeded(UiSprite, KnobSprite);
        SetRef(wheel, "slots", player.GetComponent<SkillSlots>());
        SetRef(wheel, "user", player.GetComponent<SkillUser>());
        EditorUtility.SetDirty(wheel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[P12Wiring_SkillUi] scena OK (Skill Wheel {(created ? "utworzony" : "istniał")} pod '{parent.name}')");
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P12Wiring_SkillUi] brak pola '{field}' w {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
