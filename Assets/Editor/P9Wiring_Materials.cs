using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Proceduralne tekstury skaly + 4 materialy biomow na shaderze LD57/Triplanar (Cave_Forest/Rock/Crystal/Abyss) przypisane do "Model" w Level 1..8.prefab.
/// Idempotentne. Powrot do materialu level artysty: patrz Docs/WIRING-p9-visual.md (Ground.mat zostaje w repo).
/// </summary>
public static class P9Wiring_Materials
{
    const string TexFolder = "Assets/Graphics/Generated";
    const string MatFolder = "Assets/Materials/Cave";
    const string LevelsFolder = "Assets/Prefabs/Levels";

    const string ShaderPath = "Assets/Shaders/Triplanar.shader";

    // biom: material, kolor bazy, kolor gory, tekstura bazy, tekstura gory, prog gory, gladkosc, poziomy
    struct Biome
    {
        public string name; public Color baseColor, topColor; public string baseTex, topTex; public float threshold, smoothness; public int[] levels;
    }

    static readonly Biome[] Biomes =
    {
        new Biome { name = "Cave_Forest",  baseColor = new Color(0.50f, 0.36f, 0.24f), topColor = new Color(0.62f, 0.95f, 0.45f), baseTex = "rock_albedo",        topTex = "moss_albedo", threshold = 0.55f, smoothness = 0.12f, levels = new[] { 1, 2 } },
        new Biome { name = "Cave_Rock",    baseColor = new Color(0.40f, 0.39f, 0.38f), topColor = new Color(0.52f, 0.51f, 0.50f), baseTex = "rock_albedo",        topTex = "rock_albedo", threshold = 0.7f,  smoothness = 0.15f, levels = new[] { 3, 4 } },
        new Biome { name = "Cave_Crystal", baseColor = new Color(0.50f, 0.60f, 0.60f), topColor = new Color(0.30f, 0.38f, 0.38f), baseTex = "rock_veins_albedo", topTex = "rock_albedo", threshold = 0.7f,  smoothness = 0.30f, levels = new[] { 5, 6 } },
        new Biome { name = "Cave_Abyss",   baseColor = new Color(0.30f, 0.24f, 0.46f), topColor = new Color(0.12f, 0.10f, 0.20f), baseTex = "rock_albedo",        topTex = "rock_albedo", threshold = 0.65f, smoothness = 0.20f, levels = new[] { 7, 8 } },
    };

    [MenuItem("LD57/Wire P9 Materials")]
    public static void Run()
    {
        EnsureFolder("Assets/Graphics", "Generated");
        EnsureFolder("Assets/Materials", "Cave");

        var textures = new System.Collections.Generic.Dictionary<string, Texture2D>
        {
            ["rock_albedo"] = EnsureTexture("rock_albedo", () => ProceduralTextures.RockAlbedo(), srgb: true, normal: false),
            ["moss_albedo"] = EnsureTexture("moss_albedo", () => ProceduralTextures.MossAlbedo(), srgb: true, normal: false),
            ["rock_veins_albedo"] = EnsureTexture("rock_veins_albedo", () => ProceduralTextures.RockVeinsAlbedo(), srgb: true, normal: false),
        };
        var normal = EnsureTexture("rock_normal", () => ProceduralTextures.RockNormal(strength: 2f), srgb: false, normal: true);
        var mask = EnsureTexture("rock_mask", () => ProceduralTextures.RockMask(), srgb: false, normal: false);

        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("LD57/Triplanar");
        if (shader == null) { Debug.LogError("[P9Materials] brak shadera LD57/Triplanar (" + ShaderPath + ")"); return; }

        foreach (var v in Biomes)
        {
            var path = $"{MatFolder}/{v.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = v.name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", v.baseColor);
            mat.SetTexture("_BaseMap", textures[v.baseTex]);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1.2f);
            mat.SetTexture("_OcclusionMap", mask);
            mat.SetFloat("_Occlusion", 0.6f);
            mat.SetTexture("_TopMap", textures[v.topTex]);
            mat.SetColor("_TopColor", v.topColor);
            mat.SetFloat("_TopThreshold", v.threshold);
            mat.SetFloat("_Tiling", 4f);          // metry swiata na jeden kafel tekstury
            mat.SetFloat("_Smoothness", v.smoothness);
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();

        foreach (var v in Biomes)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/{v.name}.mat");
            foreach (var level in v.levels) AssignToLevel(level, mat);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P9Materials] done (triplanar, biomy Forest/Rock/Crystal/Abyss)");
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
