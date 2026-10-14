using System.Collections.Generic;
using UnityEngine;

/// <summary>Generuje efekty dźwiękowe syntetycznie (bez plików) i odtwarza je z puli AudioSource.</summary>
public class ProceduralSfxBank : MonoBehaviour
{
    public static ProceduralSfxBank Instance { get; private set; }

    private const int SampleRate = 44100;
    private const int PoolSize = 8;

    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.6f;
    [SerializeField, Range(0f, 0.6f), Tooltip("Udział pogłosu w miksie.")] private float reverbMix = 0.2f;
    [SerializeField, Range(0.5f, 4f), Tooltip("Nasycenie (tanh) dla uderzeń; tony dostają ok. 2/3 tej wartości.")] private float drive = 1.8f;
    [SerializeField, Tooltip("Opcjonalne prawdziwe klipy indeksowane SfxKind. Ustawiony element zastępuje syntezę.")]
    private AudioClip[] overrides;
    [SerializeField, Tooltip("Prawdziwe nagrania ciosu mieczem (losowany jeden na cios); puste = synteza/override.")]
    private AudioClip[] attackVariants;

    private static readonly Dictionary<SfxKind, float> kindVolume = new Dictionary<SfxKind, float>
    {
        { SfxKind.Dash, 0.7f }, { SfxKind.Attack, 0.7f }, { SfxKind.Hit, 0.8f }, { SfxKind.PlayerHurt, 1f },
        { SfxKind.Windup, 0.6f }, { SfxKind.Shot, 0.6f }, { SfxKind.GateOpen, 1f }, { SfxKind.Heart, 0.8f },
        { SfxKind.LevelUp, 0.8f }, { SfxKind.CardPick, 0.7f }, { SfxKind.WaveCleared, 0.8f }, { SfxKind.Death, 1f },
        { SfxKind.Win, 1f }, { SfxKind.BossTelegraph, 0.9f }, { SfxKind.EnemyDie, 0.8f },
    };

    private AudioClip[] clips;
    private AudioSource[] sources;
    private int next;
    private readonly System.Random rng = new System.Random(12345);

    private void Awake()
    {
        Instance = this;
        var kinds = System.Enum.GetValues(typeof(SfxKind));
        clips = new AudioClip[kinds.Length];
        foreach (SfxKind k in kinds)
        {
            int i = (int)k;
            if (overrides != null && i < overrides.Length && overrides[i] != null)
                clips[i] = overrides[i];
            else
                clips[i] = Build(k);
        }

        sources = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Sfx Source " + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.ignoreListenerPause = true;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 40f;
            sources[i] = s;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static float KindVolume(SfxKind k) => kindVolume.TryGetValue(k, out var v) ? v : 1f;

    public void Play(SfxKind kind, Vector3 position, float volume, bool spatial)
    {
        if (clips == null) return;
        var clip = clips[(int)kind];
        if (kind == SfxKind.Attack && attackVariants != null && attackVariants.Length > 0)
        {
            var variant = attackVariants[rng.Next(attackVariants.Length)];
            if (variant != null) clip = variant;
        }
        if (clip == null) return;

        AudioSource src = null;
        for (int i = 0; i < PoolSize; i++)
        {
            int idx = (next + i) % PoolSize;
            if (!sources[idx].isPlaying)
            {
                src = sources[idx];
                next = (idx + 1) % PoolSize;
                break;
            }
        }
        if (src == null)
        {
            src = sources[next];
            next = (next + 1) % PoolSize;
        }

        src.transform.position = position;
        src.spatialBlend = spatial ? 0.8f : 0f;
        src.pitch = 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
        src.Stop();
        src.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume * KindVolume(kind));
    }

    // ------------------------------------------------------------ synteza (warstwy: ciało, transjent, sub, pogłos)

    private const float TwoPi = 2f * Mathf.PI;

    private float Rand() => (float)(rng.NextDouble() * 2.0 - 1.0);

    private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

    private static float Attack(float t, float attack) => Mathf.Min(1f, t / Mathf.Max(0.001f, attack));

    /// <summary>Ciało: dwa oscylatory (detune), mieszanka sinus/trójkąt, wykładniczy sweep wysokości, opcjonalne wibrato.</summary>
    private void AddBody(float[] b, float start, float dur, float f0, float f1, float amp, float attack, float decay,
        float tri = 0.3f, float vibHz = 0f, float vibDepth = 0f)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(b.Length - s, Mathf.CeilToInt(dur * SampleRate));
        float pa = 0f, pb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / dur;
            float f = f0 * Mathf.Pow(f1 / f0, u);
            if (vibHz > 0f) f *= 1f + vibDepth * Mathf.Sin(TwoPi * vibHz * t);
            pa += TwoPi * f * 0.993f / SampleRate;
            pb += TwoPi * f * 1.007f / SampleRate;
            float oa = Mathf.Lerp(Mathf.Sin(pa), (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(pa)), tri);
            float ob = Mathf.Lerp(Mathf.Sin(pb), (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(pb)), tri);
            float env = Attack(t, attack) * Mathf.Exp(-t / decay) * Mathf.Min(1f, (dur - t) / 0.006f);
            b[s + i] += 0.5f * (oa + ob) * env * amp;
        }
    }

    /// <summary>Transjent/szum: biały szum przez filtr dolnoprzepustowy (cutoff c0 -> cMid -> c1, wykładniczo); band odcina też dół.</summary>
    private void AddNoise(float[] b, float start, float dur, float c0, float cMid, float c1, float amp,
        float attack, float decay, bool band = false, float pulseHz = 0f)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(b.Length - s, Mathf.CeilToInt(dur * SampleRate));
        float lp = 0f, lo = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / dur;
            float c = u < 0.5f ? c0 * Mathf.Pow(cMid / c0, u * 2f) : cMid * Mathf.Pow(c1 / cMid, (u - 0.5f) * 2f);
            float a = 1f - Mathf.Exp(-TwoPi * c / SampleRate);
            lp += a * (Rand() - lp);
            float y = lp;
            if (band)
            {
                float a2 = 1f - Mathf.Exp(-TwoPi * (c * 0.25f) / SampleRate);
                lo += a2 * (lp - lo);
                y = lp - lo;
            }
            float norm = Mathf.Min(1f / Mathf.Sqrt(Mathf.Max(0.02f, a)), 6f); // wyrównanie głośności po filtrze
            float env = Attack(t, attack) * Mathf.Exp(-t / decay) * Mathf.Min(1f, (dur - t) / 0.006f);
            if (pulseHz > 0f) env *= 0.6f + 0.4f * Mathf.Cos(TwoPi * pulseHz * t);
            b[s + i] += y * norm * 0.5f * env * amp;
        }
    }

    /// <summary>Sub: sinus 40-70 Hz z szybkim decayem, lekki spadek wysokości, opcjonalna pulsacja.</summary>
    private void AddSub(float[] b, float start, float dur, float f, float amp, float decay,
        float drop = 0.8f, float pulseHz = 0f)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(b.Length - s, Mathf.CeilToInt(dur * SampleRate));
        float p = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / dur;
            p += TwoPi * Mathf.Lerp(f, f * drop, u) / SampleRate;
            float env = Attack(t, 0.004f) * Mathf.Exp(-t / decay) * Mathf.Min(1f, (dur - t) / 0.006f);
            if (pulseHz > 0f) env *= 0.55f + 0.45f * Mathf.Cos(TwoPi * pulseHz * t);
            b[s + i] += Mathf.Sin(p) * env * amp;
        }
    }

    /// <summary>Dzwonek: sinus + 2. harmoniczna (0,4).</summary>
    private void AddBell(float[] b, float start, float dur, float f, float amp, float decay)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(b.Length - s, Mathf.CeilToInt(dur * SampleRate));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Attack(t, 0.003f) * Mathf.Exp(-t / decay) * Mathf.Min(1f, (dur - t) / 0.006f);
            b[s + i] += (Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * 2f * f * t)) * 0.7f * env * amp;
        }
    }

    /// <summary>Pogłos (4 opóźnienia ze sprzężeniem i filtrem w pętli), soft clip tanh, normalizacja do 0,8.</summary>
    private AudioClip Finish(string name, float[] dry, bool impact, bool longTail, float driveScale = 1f)
    {
        int n = dry.Length;
        float[] outBuf = new float[n];
        float[] ms = { 23f, 37f, 53f, 71f };
        float fb = longTail ? 0.4f : 0.28f;
        float stretch = longTail ? 1.6f : 1f;
        float mix = Mathf.Clamp01(reverbMix * (longTail ? 1.25f : 1f));
        float[] wet = new float[n];
        for (int d = 0; d < ms.Length; d++)
        {
            int dl = Mathf.Max(1, Mathf.RoundToInt(ms[d] * stretch * 0.001f * SampleRate));
            float[] line = new float[n];
            float lpState = 0f;
            for (int i = 0; i < n; i++)
            {
                float fed = 0f;
                if (i >= dl)
                {
                    lpState += 0.35f * (line[i - dl] - lpState);
                    fed = lpState * fb;
                }
                line[i] = dry[i] + fed;
                wet[i] += line[i] * 0.3f;
            }
        }
        float dr = (impact ? drive : drive * 0.67f) * driveScale;
        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = dry[i] * (1f - mix * 0.5f) + wet[i] * mix;
            x = (float)System.Math.Tanh(x * dr);
            outBuf[i] = x;
            float a = Mathf.Abs(x);
            if (a > peak) peak = a;
        }
        float g = peak > 0.0001f ? 0.8f / peak : 1f;
        int fade = Mathf.Max(1, Mathf.Min(n, Mathf.RoundToInt(0.01f * SampleRate)));
        for (int i = 0; i < n; i++)
        {
            float e = i >= n - fade ? (n - i) / (float)fade : 1f;
            outBuf[i] *= g * e;
        }
        var clip = AudioClip.Create("sfx_" + name, n, 1, SampleRate, false);
        clip.SetData(outBuf, 0);
        return clip;
    }

    private static readonly float[] ArpLevelUp = { 523f, 659f, 784f, 1047f };
    private static readonly float[] ArpWin = { 523f, 659f, 784f, 1047f, 1319f, 1568f };
    private static readonly float[] Chord = { 523f, 659f, 784f };

    private AudioClip Build(SfxKind kind)
    {
        float[] b;
        switch (kind)
        {
            case SfxKind.Dash:
                b = Buf(0.42f);
                AddNoise(b, 0f, 0.3f, 400f, 2500f, 600f, 1f, 0.06f, 0.14f, true);
                AddSub(b, 0f, 0.2f, 50f, 0.25f, 0.1f);
                return Finish("dash", b, false, false);
            case SfxKind.Attack:
                b = Buf(0.28f);
                AddNoise(b, 0f, 0.12f, 5000f, 2500f, 1200f, 0.6f, 0.004f, 0.05f);
                AddBody(b, 0f, 0.15f, 500f, 150f, 0.9f, 0.004f, 0.07f, 0.4f);
                return Finish("attack", b, false, false);
            case SfxKind.Hit:
                b = Buf(0.32f);
                AddNoise(b, 0f, 0.06f, 6000f, 2500f, 800f, 0.8f, 0.002f, 0.015f);
                AddSub(b, 0f, 0.18f, 60f, 1f, 0.08f);
                AddBody(b, 0f, 0.18f, 220f, 90f, 0.8f, 0.003f, 0.06f, 0.2f);
                return Finish("hit", b, true, false);
            case SfxKind.PlayerHurt:
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.08f, 5000f, 2000f, 800f, 0.6f, 0.002f, 0.02f);
                AddSub(b, 0f, 0.3f, 55f, 1f, 0.12f);
                AddBody(b, 0f, 0.3f, 110f, 55f, 1f, 0.003f, 0.12f, 0.7f);
                return Finish("hurt", b, true, false, 1.3f);
            case SfxKind.Windup:
                b = Buf(0.62f);
                AddBody(b, 0f, 0.5f, 180f, 520f, 0.8f, 0.05f, 1.2f, 0.4f, 6f, 0.03f);
                AddNoise(b, 0f, 0.5f, 800f, 1800f, 3000f, 0.35f, 0.45f, 5f);
                return Finish("windup", b, false, false);
            case SfxKind.Shot:
                b = Buf(0.24f);
                AddNoise(b, 0f, 0.04f, 7000f, 3000f, 1500f, 0.9f, 0.002f, 0.012f);
                AddBody(b, 0f, 0.12f, 1200f, 300f, 0.8f, 0.003f, 0.04f, 0.3f);
                return Finish("shot", b, false, false);
            case SfxKind.GateOpen:
                b = Buf(1.9f);
                AddSub(b, 0f, 1f, 45f, 1f, 0.4f, 0.9f);
                AddNoise(b, 0f, 1f, 300f, 220f, 120f, 0.9f, 0.03f, 0.4f);
                AddNoise(b, 0f, 0.05f, 5000f, 2500f, 1000f, 0.5f, 0.002f, 0.012f);
                AddBody(b, 0.02f, 0.25f, 1100f, 1100f, 0.25f, 0.003f, 0.08f, 0f);
                AddBody(b, 0.02f, 0.25f, 1650f, 1650f, 0.2f, 0.003f, 0.07f, 0f);
                return Finish("gate", b, true, true);
            case SfxKind.Heart:
                b = Buf(0.75f);
                AddBell(b, 0f, 0.5f, 660f, 0.8f, 0.18f);
                AddBell(b, 0.1f, 0.5f, 990f, 0.7f, 0.18f);
                return Finish("heart", b, false, false);
            case SfxKind.LevelUp:
                b = Buf(1.7f);
                foreach (float f in Chord)
                    AddBody(b, 0f, 0.8f, f, f, 0.35f, 0.15f, 0.5f, 0.5f);
                for (int k = 0; k < ArpLevelUp.Length; k++)
                    AddBody(b, 0.15f + k * 0.09f, 0.5f, ArpLevelUp[k], ArpLevelUp[k], 0.45f, 0.004f, 0.18f, 0.3f);
                return Finish("levelup", b, false, true);
            case SfxKind.CardPick:
                b = Buf(0.2f);
                AddNoise(b, 0f, 0.02f, 4000f, 2500f, 1500f, 0.4f, 0.002f, 0.01f);
                AddBody(b, 0f, 0.08f, 700f, 500f, 0.9f, 0.003f, 0.03f, 0.1f);
                return Finish("card", b, false, false);
            case SfxKind.WaveCleared:
                b = Buf(0.95f);
                AddBody(b, 0f, 0.3f, 784f, 784f, 0.7f, 0.004f, 0.12f, 0.4f);
                AddBody(b, 0.14f, 0.45f, 523f, 523f, 0.8f, 0.004f, 0.2f, 0.4f);
                return Finish("wave", b, false, true);
            case SfxKind.Death:
                b = Buf(2.0f);
                AddBody(b, 0f, 1f, 200f, 40f, 1f, 0.005f, 0.6f, 0.5f);
                AddNoise(b, 0f, 0.8f, 3000f, 800f, 200f, 0.5f, 0.004f, 0.3f);
                AddSub(b, 0f, 1f, 40f, 0.9f, 0.5f, 0.9f);
                return Finish("death", b, true, true);
            case SfxKind.Win:
                b = Buf(2.4f);
                for (int k = 0; k < ArpWin.Length; k++)
                    AddBody(b, k * 0.1f, 0.6f, ArpWin[k], ArpWin[k], 0.4f, 0.004f, 0.2f, 0.3f);
                foreach (float f in Chord)
                    AddBody(b, 0.6f, 0.9f, f, f, 0.4f, 0.05f, 0.5f, 0.5f);
                return Finish("win", b, false, true);
            case SfxKind.BossTelegraph:
                b = Buf(1.7f);
                AddSub(b, 0f, 0.9f, 55f, 1f, 3f, 1f, 5f);
                AddNoise(b, 0f, 0.9f, 300f, 300f, 300f, 0.8f, 0.05f, 3f, false, 5f);
                return Finish("boss", b, true, true);
            case SfxKind.EnemyDie:
                b = Buf(0.35f);
                AddNoise(b, 0f, 0.15f, 3000f, 1200f, 400f, 0.9f, 0.003f, 0.05f);
                AddBody(b, 0f, 0.15f, 350f, 90f, 0.7f, 0.003f, 0.05f, 0.3f);
                AddSub(b, 0f, 0.15f, 60f, 0.7f, 0.08f);
                return Finish("enemydie", b, true, false);
        }
        return null;
    }
}
