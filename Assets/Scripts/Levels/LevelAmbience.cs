using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Klimat poziomu (gradient glebokosci): ambient, mgla, slonce, lampa gracza oraz post-processing
/// (globalny Volume "Level Volume" z profilem tworzonym w kodzie). Komponent na roocie prefabu poziomu.
/// </summary>
public class LevelAmbience : MonoBehaviour
{
    [System.Serializable]
    public struct Preset
    {
        public Color ambientColor, fogColor, sunColor;
        public float fogDensity, sunIntensity, lampIntensity;
        // post-processing
        public float vignette, bloom, filmGrain, saturation, temperature;
        public bool fogVolumetric;
        public float bloomThreshold, contrast, vignetteSmoothness, chromatic;
        public Color shadows, midtones, highlights;   // LiftGammaGain: tint (biel = bez zmiany)

        public Preset(Color ambient, Color fog, float density, float sun, Color sunCol, float lamp,
                      float vignette, float bloom, float grain, float saturation, float temperature, bool fogVolumetric,
                      float bloomThreshold, float contrast, float vignetteSmoothness, float chromatic,
                      Color shadows, Color midtones, Color highlights)
        {
            ambientColor = ambient; fogColor = fog; fogDensity = density; sunIntensity = sun; sunColor = sunCol; lampIntensity = lamp;
            this.vignette = vignette; this.bloom = bloom; filmGrain = grain; this.saturation = saturation; this.temperature = temperature;
            this.fogVolumetric = fogVolumetric;
            this.bloomThreshold = bloomThreshold; this.contrast = contrast; this.vignetteSmoothness = vignetteSmoothness; this.chromatic = chromatic;
            this.shadows = shadows; this.midtones = midtones; this.highlights = highlights;
        }
    }

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    public static readonly Preset[] Presets =
    {
        //                ambient              fog                 gest.   slonce sloncekol            lampa  winieta bloom ziarno sat    temp  mgla    prog  kontr gladk. chrom  cienie              srodek              swiatla
        new Preset(C(.52f,.50f,.34f), C(0,0,0),          0f,     1.2f,  C(1f,.85f,.65f),  0f,    0.25f, 0.60f, 0.10f,  8f,    8f, false, 0.9f,  10f, 0.40f, 0f,    C(1f,.97f,.92f),    C(1f,.98f,.94f),    C(1f,.96f,.88f)), // 1 las: cieply, lekko zielonkawy
        new Preset(C(.38f,.38f,.26f), C(0,0,0),          0f,     0.9f,  C(.95f,.8f,.6f),  0f,    0.25f, 0.60f, 0.14f,  6f,    6f, false, 0.9f,  10f, 0.40f, 0f,    C(1f,.96f,.9f),     C(1f,.97f,.93f),    C(1f,.95f,.87f)), // 2 las, nieco ciemniej
        new Preset(C(.28f,.25f,.22f), C(0,0,0),          0f,     0.55f, C(.85f,.78f,.7f), 0f,    0.30f, 0.70f, 0.15f,  0f,     2f, false, 0.9f,  15f, 0.45f, 0f,    C(.95f,.95f,.97f), C(1f,1f,1f),        C(1f,.98f,.95f)), // 3 szarobrazowy
        new Preset(C(.24f,.24f,.24f), C(.35f,.35f,.36f), 0.012f, 0.4f,  C(.8f,.8f,.8f),   0f,    0.30f, 0.70f, 0.15f, -2f,    -2f, true,  0.9f,  15f, 0.45f, 0f,    C(.94f,.95f,.98f), C(1f,1f,1f),        C(.98f,.98f,1f)), // 4 szary + mgla
        new Preset(C(.08f,.14f,.11f), C(.05f,.09f,.07f), 0.012f, 0.2f,  C(.4f,.6f,.5f),   0f,    0.35f, 1.00f, 0.22f, -10f,   -6f, true,  0.85f, 15f, 0.50f, 0f,    C(.92f,1f,.97f),    C(.97f,1f,.99f),    C(.95f,1f,1f)),   // 5 chlodny zielonoszary
        new Preset(C(.07f,.1f,.15f),  C(.04f,.06f,.1f),  0.014f, 0.15f, C(.4f,.5f,.7f),   0.6f,  0.35f, 1.00f, 0.24f, -10f,  -10f, true,  0.85f, 15f, 0.50f, 0f,    C(.9f,.95f,1f),     C(.95f,.98f,1f),    C(.93f,.97f,1f)), // 6 niebieskoszary
        new Preset(C(.015f,.02f,.055f), C(.04f,.015f,.08f), 0.02f, 0.05f, C(.3f,.35f,.6f),  1.2f,  0.45f, 1.20f, 0.25f, -10f,  -13f, true,  0.8f,  20f, 0.55f, 0.05f, C(.88f,.9f,1f),     C(.95f,.95f,1f),    C(.95f,.88f,1f)), // 7 ciemny granat, fioletowa mgla, lampa
        new Preset(C(.12f,.06f,.18f), C(.1f,.03f,.15f),  0.02f,  0.15f, C(.6f,.3f,.9f),   1.5f,  0.45f, 1.20f, 0.25f, -10f,  -15f, true,  0.8f,  20f, 0.55f, 0.05f, C(.9f,.85f,1f),     C(.96f,.92f,1f),    C(.95f,.8f,1f)),  // 8 fiolet
    };

    const string VolumeName = "Level Volume";
    static Volume levelVolume;

    /// <summary>Wolane po kazdym zastosowaniu post-processingu (profil z wartosciami presetu), np. dla PostFxPulse.</summary>
    public static event System.Action<VolumeProfile> PostApplied;

    [SerializeField] private Color ambientColor = Color.gray;
    [SerializeField] private Color fogColor = Color.black;
    [SerializeField] private float fogDensity;
    [SerializeField] private float sunIntensity = 1f;
    [SerializeField] private Color sunColor = Color.white;
    [SerializeField, Tooltip("0 = lampa gracza wylaczona")] private float lampIntensity;

    [Header("Post-processing")]
    [SerializeField, Range(0f, 0.5f)] private float vignette = 0.15f;
    [SerializeField, Range(0f, 2f)] private float bloom = 0.3f;
    [SerializeField, Range(0f, 1f)] private float filmGrain = 0.1f;
    [SerializeField, Range(-30f, 10f)] private float saturation;
    [SerializeField, Range(-20f, 20f)] private float temperature;
    [SerializeField, Tooltip("Mgla wolumetryczna (paczka Volumetric Fog), od poziomu 4")] private bool fogVolumetric;
    [SerializeField, Range(0f, 2f)] private float bloomThreshold = 0.9f;
    [SerializeField, Range(-30f, 40f)] private float contrast = 10f;
    [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.4f;
    [SerializeField, Range(0f, 1f)] private float chromatic;
    [SerializeField] private Color shadows = Color.white;
    [SerializeField] private Color midtones = Color.white;
    [SerializeField] private Color highlights = Color.white;
    [SerializeField, HideInInspector, Tooltip("False = prefab sprzed rozszerzenia; wartosci post-processingu brane z presetu po numerze w nazwie obiektu")] private bool postFromPreset;
    [SerializeField, HideInInspector, Tooltip("False = prefab sprzed pol kontrast/chromatic/LGG; post brany z presetu")] private bool postV2;

    /// <summary>Kopiuje wartosci z Presets do pol (dla skryptu edytorowego).</summary>
    public void ApplyPresetValues(int level)
    {
        var p = Presets[Mathf.Clamp(level, 1, Presets.Length) - 1];
        ambientColor = p.ambientColor; fogColor = p.fogColor; fogDensity = p.fogDensity;
        sunIntensity = p.sunIntensity; sunColor = p.sunColor; lampIntensity = p.lampIntensity;
        CopyPost(p);
        postFromPreset = true;
        postV2 = true;
    }

    private void CopyPost(Preset p)
    {
        vignette = p.vignette; bloom = p.bloom; filmGrain = p.filmGrain;
        saturation = p.saturation; temperature = p.temperature; fogVolumetric = p.fogVolumetric;
        bloomThreshold = p.bloomThreshold; contrast = p.contrast; vignetteSmoothness = p.vignetteSmoothness; chromatic = p.chromatic;
        shadows = p.shadows; midtones = p.midtones; highlights = p.highlights;
    }

    /// <summary>Ustawia wartosci z tabeli i od razu stosuje.</summary>
    public void ApplyPreset(int level)
    {
        ApplyPresetValues(level);
        Apply();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelStarted += GameEvents_OnLevelStarted;
        if (IsCurrentLevel()) Apply();
    }

    private void OnDisable() => GameEvents.OnLevelStarted -= GameEvents_OnLevelStarted;

    private void GameEvents_OnLevelStarted(int level)
    {
        if (OwnLevel() == level) Apply();
    }

    private int OwnLevel()
    {
        var tracker = GetComponentInParent<LevelEnemyTracker>();
        return tracker != null ? tracker.LevelNumber : 0;
    }

    /// <summary>Wszystkie poziomy stoją w scenie naraz: RenderSettings ustawia tylko ten, na którym jest gracz.</summary>
    private bool IsCurrentLevel()
    {
        int own = OwnLevel();
        if (own == 0 || LevelManager.Instance == null) return true;
        return LevelManager.Instance.CurrentLevel == own;
    }

    private void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.intensity = sunIntensity;
            l.color = sunColor;
            break;
        }

        var lamp = FindFirstObjectByType<PlayerLamp>();
        if (lamp != null) lamp.SetIntensity(lampIntensity);

        ApplyPostProcessing();
    }

    // ------------------------------------------------------------ post-processing

    private void ApplyPostProcessing()
    {
        if (!postFromPreset || !postV2)
        {
            // prefab sprzed rozszerzenia: numer poziomu z nazwy ("Level 3", "Level 3(Clone)")
            var m = Regex.Match(gameObject.name, @"\d+");
            int level = m.Success && int.TryParse(m.Value, out var n) ? n : 1;
            CopyPost(Presets[Mathf.Clamp(level, 1, Presets.Length) - 1]);
        }

        var volume = GetVolume();
        var profile = volume.sharedProfile;

        var vig = GetOrAdd<Vignette>(profile);
        vig.active = true;
        vig.intensity.Override(vignette);
        vig.smoothness.Override(vignetteSmoothness);
        vig.color.Override(Color.black);

        var blm = GetOrAdd<Bloom>(profile);
        blm.active = true;
        blm.intensity.Override(bloom);
        blm.threshold.Override(bloomThreshold);
        blm.scatter.Override(0.65f);

        var ca = GetOrAdd<ColorAdjustments>(profile);
        ca.active = true;
        ca.saturation.Override(saturation);
        ca.contrast.Override(contrast);

        var wb = GetOrAdd<WhiteBalance>(profile);
        wb.active = true;
        wb.temperature.Override(temperature);

        var grain = GetOrAdd<FilmGrain>(profile);
        grain.active = true;
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(filmGrain);
        grain.response.Override(0.8f);

        var chrom = GetOrAdd<ChromaticAberration>(profile);
        chrom.active = true;
        chrom.intensity.Override(chromatic);

        var lgg = GetOrAdd<LiftGammaGain>(profile);
        lgg.active = true;
        lgg.lift.Override(new Vector4(shadows.r, shadows.g, shadows.b, 0f));
        lgg.gamma.Override(new Vector4(midtones.r, midtones.g, midtones.b, 0f));
        lgg.gain.Override(new Vector4(highlights.r, highlights.g, highlights.b, 0f));

        var dof = GetOrAdd<DepthOfField>(profile);
        dof.active = false;

        try { ApplyVolumetricFog(profile); }
        catch (System.Exception e) { Debug.LogWarning("[LevelAmbience] mgla wolumetryczna: " + e.Message); }

        PostApplied?.Invoke(profile);
    }

    // Komponent z paczki Volumetric Fog (asmdef autoReferenced). Dziala tylko, gdy w rendererze jest VolumetricFogRendererFeature.
    private void ApplyVolumetricFog(VolumeProfile profile)
    {
        var fog = GetOrAdd<VolumetricFogVolumeComponent>(profile);
        fog.active = true;
        fog.enabled.Override(fogVolumetric);
        if (!fogVolumetric) return;
        fog.distance.Override(60f);
        fog.baseHeight.Override(-5f);
        fog.maximumHeight.Override(12f);
        fog.density.Override(Mathf.Lerp(0.15f, 0.3f, Mathf.InverseLerp(0.012f, 0.02f, fogDensity)));
        fog.attenuationDistance.Override(40f);
        fog.enableMainLightContribution.Override(false);
        fog.enableAdditionalLightsContribution.Override(true);
        fog.maxSteps.Override(64);
        fog.blurIterations.Override(2);
        fog.tint.Override(Color.Lerp(Color.white, fogColor * 4f, 0.5f));
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        return profile.TryGet<T>(out var c) ? c : profile.Add<T>(true);
    }

    private static Volume GetVolume()
    {
        if (levelVolume != null) return levelVolume;
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.name == VolumeName) { levelVolume = v; break; }
        if (levelVolume == null)
        {
            var go = new GameObject(VolumeName);
            levelVolume = go.AddComponent<Volume>();
        }
        levelVolume.isGlobal = true;
        levelVolume.priority = 10f;
        levelVolume.weight = 1f;
        if (levelVolume.sharedProfile == null)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Level Volume Profile";
            levelVolume.sharedProfile = profile;
        }
        return levelVolume;
    }
}
