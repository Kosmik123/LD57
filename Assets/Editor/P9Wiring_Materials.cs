using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Proceduralne tekstury skaly + 4 materialy URP/Lit (Cave_Warm/Grey/Cold/Violet) przypisane do "Model" w Level 1..8.prefab.
/// Idempotentne. Powrot do materialu level artysty: patrz Docs/WIRING-p9-visual.md (Ground.mat zostaje w repo).
/// </summary>
public static class P9Wiring_Materials
{
    const string TexFolder = "Assets/Graphics/Generated";
    const string MatFolder = "Assets/Materials/Cave";
    const string LevelsFolder = "Assets/Prefabs/Levels";

    static readonly (string name, Color tint, int[] levels)[] Variants =
    {
        ("Cave_Warm",   new Color(0.45f, 0.32f, 0.22f), new[] { 1, 2 }),
        ("Cave_Grey",   new Color(0.33f, 0.31f, 0.29f), new[] { 3, 4 }),
        ("Cave_Cold",   new Color(0.22f, 0.28f, 0.30f), new[] { 5, 6 }),
        ("Cave_Violet", new Color(0.20f, 0.16f, 0.28f), new[] { 7, 8 }),
    };

    [MenuItem("LD57/Wire P9 Materials")]
    public static void Run()
    {
        EnsureFolder("Assets/Graphics", "Generated");
        EnsureFolder("Assets/Materials", "Cave");

        var albedo = EnsureTexture("rock_albedo", () => ProceduralTextures.RockAlbedo(), srgb: true, normal: false);
        var normal = EnsureTexture("rock_normal", () => ProceduralTextures.RockNormal(strength: 2f), srgb: false, normal: true);
        var mask = EnsureTexture("rock_mask", () => ProceduralTextures.RockMask(), srgb: false, normal: false);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("[P9Materials] brak shadera URP/Lit"); return; }

        foreach (var v in Variants)
        {
            var path = $"{MatFolder}/{v.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = v.name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", v.tint);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1.2f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_OcclusionMap", mask);
            mat.SetFloat("_OcclusionStrength", 0.6f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap" })
            {
                mat.SetTextureScale(prop, new Vector2(6f, 6f));
                mat.SetTextureOffset(prop, Vector2.zero);
            }
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();

        foreach (var v in Variants)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/{v.name}.mat");
            foreach (var level in v.levels) AssignToLevel(level, mat);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P9Materials] done");
    }

    static Texture2D EnsureTexture(string name, System.Func<Color32[]> generate, bool srgb, bool normal)
    {
        var path = $"{TexFolder}/{name}.png";
        bool fresh = !File.Exists(path);
        if (fresh)
        {
            int size = ProceduralTextures.DefaultSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels32(generate());
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool changed = false;
            var wantType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != wantType) { imp.textureType = wantType; changed = true; }
            if (!normal && imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; changed = true; }
            if (changed || fresh) imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void AssignToLevel(int level, Material mat)
    {
        var path = $"{LevelsFolder}/Level {level}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[P9Materials] brak {path}"); return; }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var model = root.transform.Find("Model");
            var mr = model != null ? model.GetComponent<MeshRenderer>() : null;
            if (mr == null) { Debug.LogWarning($"[P9Materials] Level {level}: brak Model/MeshRenderer"); return; }
            var mats = mr.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != mat) { mats[i] = mat; changed = true; }
            if (!changed) return;
            mr.sharedMaterials = mats;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
