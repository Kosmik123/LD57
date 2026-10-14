using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Chwilowe impulsy post-processingu na globalnym Volume z LevelAmbience (czas rzeczywisty):
/// obrazenia gracza (chromatic + winieta), level up (blysk ekspozycji), smierc (desaturacja).
/// Wartosci bazowe pobiera z profilu po kazdym LevelAmbience.Apply, wiec powrot idzie do presetu poziomu.
/// </summary>
public class PostFxPulse : MonoBehaviour
{
    [SerializeField] private float hitChromatic = 0.35f;
    [SerializeField] private float hitVignetteAdd = 0.2f;
    [SerializeField] private float hitDuration = 0.25f;
    [SerializeField] private float levelUpExposure = 0.4f;
    [SerializeField] private float levelUpDuration = 0.4f;
    [SerializeField] private float deathSaturation = -80f;
    [SerializeField] private float deathDuration = 0.6f;

    private VolumeProfile profile;
    private ChromaticAberration chrom;
    private Vignette vig;
    private ColorAdjustments adj;
    private float baseChrom, baseVig, baseSat;

    private Health health;
    private float hitT, levelT, deathT;   // czas, ktory pozostal / uplynal
    private bool dead, dirty;

    private void OnEnable()
    {
        LevelAmbience.PostApplied += Rebase;
        GameEvents.OnLevelUp += GameEvents_OnLevelUp;
        GameEvents.OnPlayerDied += GameEvents_OnPlayerDied;
    }

    private void OnDisable()
    {
        LevelAmbience.PostApplied -= Rebase;
        GameEvents.OnLevelUp -= GameEvents_OnLevelUp;
        GameEvents.OnPlayerDied -= GameEvents_OnPlayerDied;
        Unbind();
    }

    private void GameEvents_OnLevelUp(int _) => levelT = levelUpDuration;

    private void GameEvents_OnPlayerDied()
    {
        dead = true;
        deathT = 0f;
    }

    private void Health_OnHealthChanged(int delta)
    {
        if (delta < 0) hitT = hitDuration;
    }

    private void Unbind()
    {
        if (health != null) health.OnHealthChanged -= Health_OnHealthChanged;
        health = null;
    }

    private void Rebase(VolumeProfile p)
    {
        profile = p;
        profile.TryGet(out chrom);
        profile.TryGet(out vig);
        profile.TryGet(out adj);
        // wartosci z presetu (bez naszych offsetow): przy trwajacym impulsie zapis ponizej i tak nadpisze
        if (chrom != null) baseChrom = chrom.intensity.value;
        if (vig != null) baseVig = vig.intensity.value;
        if (adj != null) { baseSat = adj.saturation.value; adj.postExposure.Override(0f); }
        dirty = true;
    }

    private void Update()
    {
        if (health == null)
        {
            var player = Player.Instance;
            if (player != null)
            {
                health = player.GetComponentInChildren<Health>();
                if (health != null) health.OnHealthChanged += Health_OnHealthChanged;
            }
        }

        if (profile == null) return;
        float dt = Time.unscaledDeltaTime;
        bool active = hitT > 0f || levelT > 0f || dead || dirty;
        if (!active) return;

        try
        {
            hitT = Mathf.Max(0f, hitT - dt);
            levelT = Mathf.Max(0f, levelT - dt);
            if (dead) deathT = Mathf.Min(deathDuration, deathT + dt);

            float hk = hitDuration > 0f ? hitT / hitDuration : 0f;
            if (chrom != null) chrom.intensity.Override(Mathf.Lerp(baseChrom, Mathf.Max(baseChrom, hitChromatic), hk));
            if (vig != null) vig.intensity.Override(baseVig + hitVignetteAdd * hk);

            if (adj != null)
            {
                float lk = levelUpDuration > 0f ? levelT / levelUpDuration : 0f;
                adj.postExposure.Override(levelUpExposure * lk);
                float dk = dead && deathDuration > 0f ? deathT / deathDuration : 0f;
                adj.saturation.Override(Mathf.Lerp(baseSat, deathSaturation, dk));
            }
            dirty = false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PostFxPulse] " + e.Message);
            enabled = false;
        }
    }
}
