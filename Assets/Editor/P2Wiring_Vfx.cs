using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Buduje prefaby VFX z ParticleSystem w kodzie i przypisuje je do SkillDefinition.Tiers[i].EffectPrefab. Idempotentne.</summary>
public static class P2Wiring_Vfx
{
    const string Folder = "Assets/Prefabs/VFX";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [MenuItem("LD57/Wire P2 VFX")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "VFX");
        mats.Clear();

        // Spin: Range 2,5/3/3,5/3,5
        float[] spinR = { 2.5f, 3f, 3.5f, 3.5f };
        var spin = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            int t = i;
            bool evo = t == 3;
            spin[t] = Save("Spin_T" + t, () => evo
                ? Fx("Spin_T3", new Color(0.55f, 0.85f, 1f), 2f, ps =>
                {
                    SetShape(ps, ParticleSystemShapeType.Cone, spinR[t] * 0.4f);
                    var sh = ps.shape; sh.angle = 25f; sh.rotation = new Vector3(-90, 0, 0);
                    Emit(ps, 80);
                    Cfg(ps, 1.5f, 3.5f, 0.25f);
                    var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                    v.orbitalY = 6f;
                })
                : Fx("Spin_T" + t, new Color(0.8f, 0.9f, 1f), 1f, ps =>
                {
                    SetShape(ps, ParticleSystemShapeType.Circle, spinR[t]);
                    var sh = ps.shape; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0f;
                    Burst(ps, 40 + t * 10);
                    Cfg(ps, 0.8f, 0.5f, 0.2f);
                }), true);
        }

        // Charge
        var charge = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            int t = i;
            bool evo = t == 3;
            charge[t] = Save("Charge_T" + t, () =>
            {
                var root = Fx("Charge_T" + t, new Color(1f, 0.55f, 0.1f), 1f, ps =>
                {
                    SetShape(ps, ParticleSystemShapeType.Box, 1f);
                    var sh = ps.shape; sh.scale = new Vector3(0.6f, 0.2f, 4f);
                    Burst(ps, 30);
                    Cfg(ps, 0.8f, 0.3f, 0.3f);
                });
                if (evo)
                {
                    var fire = Child(root, "Fire", new Color(1f, 0.3f, 0.05f), 1f, ps =>
                    {
                        SetShape(ps, ParticleSystemShapeType.Box, 1f);
                        var sh = ps.shape; sh.scale = new Vector3(0.8f, 0.2f, 5f);
                        Burst(ps, 90);
                        Cfg(ps, 3f, 0.8f, 0.4f);
                        var m = ps.main; m.gravityModifier = -0.3f;
                    });
                    Fade(fire, new Color(1f, 0.9f, 0.2f), new Color(0.9f, 0.1f, 0.05f, 0f));
                }
                return root;
            }, true);
        }

        // GroundSlam: 3/4/4/4
        float[] slamR = { 3f, 4f, 4f, 4f };
        var slam = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            int t = i;
            bool evo = t == 3;
            slam[t] = Save("GroundSlam_T" + t, () =>
            {
                var root = Fx("GroundSlam_T" + t, new Color(0.5f, 0.42f, 0.35f), 1f, ps =>
                {
                    SetShape(ps, ParticleSystemShapeType.Circle, slamR[t]);
                    var sh = ps.shape; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0f;
                    Burst(ps, 40);
                    Cfg(ps, 1f, 0.5f, 0.5f);
                    var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                    vel.radial = 1f;
                });
                if (evo)
                {
                    Child(root, "Cracks", new Color(0.25f, 0.2f, 0.18f), 1f, ps =>
                    {
                        SetShape(ps, ParticleSystemShapeType.Circle, 0.3f);
                        var sh = ps.shape; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0f;
                        sh.arc = 360f; sh.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread; sh.arcSpread = 0f;
                        // 6 cząstek na wybuch, równo co 60 st. = 6 linii; 12 wybuchów tworzy linię cząstek
                        var e = ps.emission; e.rateOverTime = 0;
                        e.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 6, 12, 0.04f) });
                        Cfg(ps, 1.2f, 6f, 0.15f);
                    });
                    Child(root, "Sparks", new Color(1f, 0.9f, 0.2f), 1f, ps =>
                    {
                        SetShape(ps, ParticleSystemShapeType.Circle, slamR[t] * 0.6f);
                        var sh = ps.shape; sh.rotation = new Vector3(-90, 0, 0);
                        Burst(ps, 30);
                        Cfg(ps, 0.8f, 4f, 0.08f);
                        var m = ps.main; m.gravityModifier = 0.5f;
                    });
                }
                return root;
            }, true);
        }

        // AxeThrow: obiekt topora (sam skill go rusza i niszczy), bez autodestrukcji
        var axe = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            int t = i;
            bool evo = t == 3;
            axe[t] = Save("AxeThrow_T" + t, () =>
            {
                var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                root.name = "AxeThrow_T" + t;
                root.transform.localScale = Vector3.one * (evo ? 0.4f : 0.3f);
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
                root.GetComponent<Renderer>().sharedMaterial = Mat("Axe_Metal", new Color(0.55f, 0.57f, 0.6f), true);
                var trail = Child(root, "Trail", evo ? new Color(1f, 0.45f, 0.1f) : new Color(0.85f, 0.85f, 0.9f), 1f, ps =>
                {
                    SetShape(ps, ParticleSystemShapeType.Sphere, 0.05f);
                    Emit(ps, evo ? 80 : 40);
                    Cfg(ps, 0.3f, 0.1f, evo ? 0.25f : 0.12f);
                    var m = ps.main; m.loop = true;
                });
                if (evo) Fade(trail, new Color(1f, 0.9f, 0.2f), new Color(0.9f, 0.1f, 0.05f, 0f));
                return root;
            }, false);
        }

        // WarCry: fala pierścieniowa
        var cry = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            int t = i;
            bool evo = t == 3;
            cry[t] = Save("WarCry_T" + t, () => Fx("WarCry_T" + t, evo ? new Color(0.95f, 0.15f, 0.1f) : new Color(1f, 0.9f, 0.2f), 1f, ps =>
            {
                SetShape(ps, ParticleSystemShapeType.Circle, 0.3f);
                var sh = ps.shape; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0f;
                Burst(ps, evo ? 120 : 60);
                Cfg(ps, 1f, 0f, evo ? 0.4f : 0.3f);
                // prędkość radialna: zasięg ~ promień tieru (4/5/5/6 m) przy życiu 1 s
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.radial = evo ? 6f : (t == 0 ? 4f : 5f);
            }), true);
        }

        // Śmierć wroga
        var death = Save("EnemyDeath", () => Fx("EnemyDeath", new Color(0.3f, 0.85f, 0.2f), 1f, ps =>
        {
            SetShape(ps, ParticleSystemShapeType.Sphere, 0.2f);
            Burst(ps, 25);
            Cfg(ps, 0.8f, 3f, 0.2f);
            var m = ps.main; m.gravityModifier = 1f;
        }), true);

        Assign("Spin", spin); Assign("Charge", charge); Assign("GroundSlam", slam);
        Assign("AxeThrow", axe); Assign("WarCry", cry);

        const string enemyBase = "Assets/Prefabs/Enemies/_Enemy Base.prefab";
        var root2 = PrefabUtility.LoadPrefabContents(enemyBase);
        try
        {
            var d = root2.GetComponent<EnemyDeath>();
            if (d == null) Debug.LogWarning("[P2Wiring_Vfx] _Enemy Base nie ma EnemyDeath");
            else
            {
                var so = new SerializedObject(d);
                so.FindProperty("deathEffect").objectReferenceValue = death;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root2, enemyBase);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root2); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P2Wiring_Vfx] OK");
    }

    // ---------- helpers ----------

    static void Assign(string skill, GameObject[] prefabs)
    {
        var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/" + skill + ".asset");
        if (def == null) { Debug.LogWarning("[P2Wiring_Vfx] brak assetu " + skill); return; }
        for (int i = 0; i < 4 && i < def.Tiers.Length; i++)
        {
            // Nie nadpisuj efektu wstawionego ręcznie (inny prefab niż nasz placeholder).
            var current = def.Tiers[i].EffectPrefab;
            if (current != null && !AssetDatabase.GetAssetPath(current).StartsWith("Assets/Prefabs/VFX/")) continue;
            def.Tiers[i].EffectPrefab = prefabs[i];
        }
        EditorUtility.SetDirty(def);
    }

    static GameObject Save(string name, Func<GameObject> build, bool autoDestroy)
    {
        var go = build();
        if (autoDestroy)
        {
            var ad = go.AddComponent<EffectAutoDestroy>();
            float life = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var m = ps.main;
                life = Mathf.Max(life, m.duration + m.startLifetime.constantMax + 0.5f);
            }
            var so = new SerializedObject(ad);
            so.FindProperty("lifetime").floatValue = Mathf.Max(3f, life);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject Fx(string name, Color color, float duration, Action<ParticleSystem> setup)
    {
        var go = new GameObject(name);
        Init(go.AddComponent<ParticleSystem>(), color, duration);
        setup(go.GetComponent<ParticleSystem>());
        return go;
    }

    static GameObject Child(GameObject parent, string name, Color color, float duration, Action<ParticleSystem> setup)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Init(go.AddComponent<ParticleSystem>(), color, duration);
        setup(go.GetComponent<ParticleSystem>());
        return go;
    }

    static void Init(ParticleSystem ps, Color color, float duration)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.duration = duration; m.loop = false; m.playOnAwake = true;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startColor = Color.white; // kolor niesie materiał
        m.stopAction = ParticleSystemStopAction.None; // niszczy EffectAutoDestroy (topór: skill)
        var e = ps.emission; e.rateOverTime = 0;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("Particle_" + ColorUtility.ToHtmlStringRGB(color), color, false);
    }

    static void Fade(GameObject go, Color from, Color to)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(to, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(to.a, 1) });
        col.color = g;
        var m = ps.main; m.startColor = Color.white;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("Particle_FFFFFF", Color.white, false);
    }

    static void SetShape(ParticleSystem ps, ParticleSystemShapeType type, float radius)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = type; s.radius = radius;
    }

    static void Emit(ParticleSystem ps, float rate) { var e = ps.emission; e.rateOverTime = rate; }

    static void Burst(ParticleSystem ps, int count)
    {
        var e = ps.emission; e.rateOverTime = 0;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Cfg(ParticleSystem ps, float life, float speed, float size)
    {
        var m = ps.main; m.startLifetime = life; m.startSpeed = speed; m.startSize = size;
    }

    static Material Mat(string name, Color color, bool lit)
    {
        if (mats.TryGetValue(name, out var cached) && cached) return cached;
        var path = Folder + "/" + name + ".mat";
        var shader = lit
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        mats[name] = mat;
        return mat;
    }
}
