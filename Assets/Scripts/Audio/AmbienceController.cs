using System.Collections;
using UnityEngine;

/// <summary>
/// Miks otoczenia per poziom (kapanie, szelest, dudnienie, wiatr, serce), cisza przed bossem i oddech bossa.
/// Puste pola AudioClip = bank proceduralny (ProceduralAmbienceBank na tym samym obiekcie).
/// </summary>
public class AmbienceController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.6f;
    [SerializeField] private float crossfadeSeconds = 1.5f;
    [SerializeField] private float stopFadeSeconds = 1f;

    [Header("Nagrania (opcjonalne; puste = proceduralne)")]
    [SerializeField] private AudioClip[] dripClips;
    [SerializeField] private AudioClip[] rustleClips;
    [SerializeField] private AudioClip rumbleClip;
    [SerializeField] private AudioClip windClip;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private AudioClip bossBreathClip;

    private struct Mix
    {
        public float wind, rumble, bossBreath;
        public Vector2 drip, rustle, heartbeat; // przedział w sekundach; y <= 0 = brak
        public float dripVol, rustleVol, heartVol;
    }

    private const int WindL = 0, RumbleL = 1, BossL = 2, LayerCount = 3;

    private ProceduralAmbienceBank bank;
    private AudioSource[] loops;
    private float[] target;
    private AudioSource oneShot;
    private Mix mix;
    private bool stopped = true;
    private bool bossSpawned;
    private float fadeSeconds;
    private Coroutine eventsRoutine, bossRoutine;
    private Transform playerT;

    private void Awake()
    {
        bank = GetComponent<ProceduralAmbienceBank>();
        loops = new AudioSource[LayerCount];
        target = new float[LayerCount];
        fadeSeconds = crossfadeSeconds;
        for (int i = 0; i < LayerCount; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.ignoreListenerPause = true;
            loops[i] = s;
        }
        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.ignoreListenerPause = true;
    }

    private void Start()
    {
        loops[WindL].clip = windClip != null ? windClip : bank != null ? bank.Wind : null;
        loops[RumbleL].clip = rumbleClip != null ? rumbleClip : bank != null ? bank.Rumble : null;
        loops[BossL].clip = bossBreathClip != null ? bossBreathClip : bank != null ? bank.BossBreath : null;
        foreach (var l in loops) if (l.clip != null) l.Play();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelStarted += HandleLevelStarted;
        GameEvents.OnRunWon += HandleEnd;
        GameEvents.OnPlayerDied += HandleEnd;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelStarted -= HandleLevelStarted;
        GameEvents.OnRunWon -= HandleEnd;
        GameEvents.OnPlayerDied -= HandleEnd;
    }

    private void HandleEnd()
    {
        stopped = true;
        fadeSeconds = stopFadeSeconds;
        for (int i = 0; i < LayerCount; i++) target[i] = 0f;
        StopRoutines();
    }

    private void HandleLevelStarted(int lvl)
    {
        stopped = false;
        bossSpawned = false;
        fadeSeconds = crossfadeSeconds;
        mix = MixFor(lvl);
        target[WindL] = mix.wind;
        target[RumbleL] = mix.rumble;
        target[BossL] = mix.bossBreath;
        StopRoutines();
        eventsRoutine = StartCoroutine(EventsLoop());
        if (lvl >= 8) bossRoutine = StartCoroutine(BossWatch());
    }

    private void StopRoutines()
    {
        if (eventsRoutine != null) StopCoroutine(eventsRoutine);
        if (bossRoutine != null) StopCoroutine(bossRoutine);
        eventsRoutine = bossRoutine = null;
    }

    private static Mix MixFor(int lvl)
    {
        var m = new Mix();
        if (lvl <= 2)
        {
            m.wind = 0.15f;
            m.drip = new Vector2(3, 7); m.dripVol = 0.5f;
        }
        else if (lvl <= 4)
        {
            m.wind = 0.1f; m.rumble = 0.15f;
            m.drip = new Vector2(4, 9); m.dripVol = 0.5f;
            m.rustle = new Vector2(6, 12); m.rustleVol = 0.5f;
        }
        else if (lvl <= 6)
        {
            m.rumble = 0.25f;
            m.drip = new Vector2(8, 14); m.dripVol = 0.4f;
            m.rustle = new Vector2(3, 8); m.rustleVol = 0.6f;
        }
        else if (lvl == 7)
        {
            m.rumble = 0.3f;
            m.rustle = new Vector2(2, 5); m.rustleVol = 0.6f;
            m.heartbeat = new Vector2(4, 4); m.heartVol = 0.3f;
        }
        else
        {
            m.drip = new Vector2(8, 15); m.dripVol = 0.2f; // cisza przed bossem
        }
        return m;
    }

    private IEnumerator BossWatch()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (!bossSpawned)
        {
            yield return wait;
            if (FindFirstObjectByType<SpiderMotherBoss>() != null) bossSpawned = true;
        }
        mix.drip = mix.rustle = mix.heartbeat = Vector2.zero;
        fadeSeconds = crossfadeSeconds;
        target[BossL] = 0.3f;
        target[RumbleL] = 0.2f;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeSeconds);
        for (int i = 0; i < LayerCount; i++)
            loops[i].volume = Mathf.MoveTowards(loops[i].volume, target[i] * masterVolume, step * masterVolume);
    }

    private IEnumerator EventsLoop()
    {
        float nextDrip = Next(mix.drip), nextRustle = Next(mix.rustle), nextHeart = Next(mix.heartbeat);
        while (!stopped)
        {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            nextDrip -= dt; nextRustle -= dt; nextHeart -= dt;
            if (nextDrip <= 0f) { nextDrip = Next(mix.drip); if (Time.timeScale > 0f) PlayDrip(); }
            if (nextRustle <= 0f) { nextRustle = Next(mix.rustle); if (Time.timeScale > 0f) PlayRustle(); }
            if (nextHeart <= 0f) { nextHeart = Next(mix.heartbeat); if (Time.timeScale > 0f) PlayHeart(); }
        }
    }

    private static float Next(Vector2 range) => range.y <= 0f ? float.MaxValue : Random.Range(range.x, range.y);

    private static AudioClip Pick(AudioClip[] custom, AudioClip[] generated)
    {
        var arr = custom != null && custom.Length > 0 ? custom : generated;
        return arr == null || arr.Length == 0 ? null : arr[Random.Range(0, arr.Length)];
    }

    private void PlayDrip()
    {
        var c = Pick(dripClips, bank != null ? bank.Drips : null);
        if (c != null) PlayAt(c, mix.dripVol);
    }

    private void PlayRustle()
    {
        var c = Pick(rustleClips, bank != null ? bank.Rustles : null);
        if (c != null) PlayAt(c, mix.rustleVol);
    }

    private void PlayHeart()
    {
        var c = heartbeatClip != null ? heartbeatClip : bank != null ? bank.Heartbeat : null;
        if (c != null) oneShot.PlayOneShot(c, mix.heartVol * masterVolume);
    }

    /// <summary>Dźwięk 3D w losowym punkcie 6-12 m od gracza.</summary>
    private void PlayAt(AudioClip clip, float vol)
    {
        if (playerT == null && Player.Instance != null) playerT = Player.Instance.transform;
        Vector3 center = playerT != null ? playerT.position : Vector3.zero;
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 pos = center + new Vector3(dir.x, 0f, dir.y) * Random.Range(6f, 12f);

        var go = new GameObject("Ambience OneShot");
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = Mathf.Clamp01(vol) * masterVolume;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 4f;
        s.maxDistance = 30f;
        s.pitch = Random.Range(0.92f, 1.08f);
        s.ignoreListenerPause = true;
        s.Play();
        Destroy(go, clip.length / s.pitch + 0.2f);
    }
}
