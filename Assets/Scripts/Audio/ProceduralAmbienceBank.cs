using UnityEngine;

/// <summary>Proceduralne placeholdery dźwięków otoczenia (kapanie, szelest, dudnienie, wiatr, serce, oddech bossa). Generowane w Awake.</summary>
public class ProceduralAmbienceBank : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const float TwoPi = 2f * Mathf.PI;

    public AudioClip[] Drips { get; private set; }
    public AudioClip[] Rustles { get; private set; }
    public AudioClip Rumble { get; private set; }
    public AudioClip Wind { get; private set; }
    public AudioClip Heartbeat { get; private set; }
    public AudioClip BossBreath { get; private set; }

    private System.Random rng;

    private void Awake()
    {
        rng = new System.Random(5150);
        Drips = new AudioClip[4];
        for (int i = 0; i < Drips.Length; i++) Drips[i] = MakeDrip(i);
        Rustles = new AudioClip[3];
        for (int i = 0; i < Rustles.Length; i++) Rustles[i] = MakeRustle(0.3f + 0.15f * i);
        Rumble = MakeRumble();
        Wind = MakeWind();
        Heartbeat = MakeHeartbeat();
        BossBreath = MakeBossBreath();
    }

    private float Rand() => (float)(rng.NextDouble() * 2.0 - 1.0);

    private static AudioClip ToClip(string name, float[] b)
    {
        float peak = 0.0001f;
        for (int i = 0; i < b.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
        float g = 0.9f / peak;
        for (int i = 0; i < b.Length; i++) b[i] *= g;
        var c = AudioClip.Create(name, b.Length, 1, SampleRate, false);
        c.SetData(b, 0);
        return c;
    }

    /// <summary>Proste opóźnienia jako lekki pogłos.</summary>
    private static void AddEchoes(float[] b, float[] delaysSec, float[] gains)
    {
        for (int d = 0; d < delaysSec.Length; d++)
        {
            int off = Mathf.RoundToInt(delaysSec[d] * SampleRate);
            for (int i = b.Length - 1; i >= off; i--) b[i] += b[i - off] * gains[d];
        }
    }

    /// <summary>Crossfade końca z początkiem: ostatni fragment bufora wtapia się w początek, pętla jest bezszwowa.</summary>
    private static float[] MakeSeamless(float[] raw, int fade)
    {
        int n = raw.Length - fade;
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = raw[i];
        for (int i = 0; i < fade; i++)
        {
            float u = i / (float)fade;
            o[i] = raw[i] * u + raw[n + i] * (1f - u);
        }
        return o;
    }

    private AudioClip MakeDrip(int variant)
    {
        float dur = 0.06f + 0.02f * variant;
        float tail = 0.35f;
        var b = new float[Mathf.CeilToInt((dur + tail) * SampleRate)];
        float f0 = 1800f - 120f * variant, f1 = 900f - 60f * variant;
        float ph = 0f;
        int n = Mathf.CeilToInt(dur * SampleRate);
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = f0 * Mathf.Pow(f1 / f0, u);
            ph += TwoPi * f / SampleRate;
            float env = Mathf.Min(1f, i / (0.003f * SampleRate)) * Mathf.Exp(-u * 4f);
            b[i] = Mathf.Sin(ph) * env;
        }
        AddEchoes(b, new[] { 0.07f, 0.13f, 0.21f }, new[] { 0.35f, 0.2f, 0.1f });
        return ToClip("Drip" + variant, b);
    }

    private AudioClip MakeRustle(float dur)
    {
        var b = new float[Mathf.CeilToInt(dur * SampleRate)];
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float u = i / (float)b.Length;
            float env = Mathf.Sin(Mathf.PI * u);
            env *= env;
            float crackle = 0.6f + 0.4f * Mathf.Sin(TwoPi * 28f * i / SampleRate + Rand());
            lp1 += (Rand() - lp1) * 0.45f;
            lp2 += (lp1 - lp2) * 0.6f;
            b[i] = (lp1 - lp2) * env * crackle;
        }
        return ToClip("Rustle", b);
    }

    private AudioClip MakeRumble()
    {
        int fade = SampleRate / 2;
        var raw = new float[4 * SampleRate + fade];
        float brown = 0f, lp = 0f, ph = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)SampleRate;
            brown = Mathf.Clamp(brown + Rand() * 0.05f, -1f, 1f) * 0.998f;
            lp += (brown - lp) * 0.1f;
            ph += TwoPi * (40f + 20f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.25f * t))) / SampleRate;
            raw[i] = lp * 3f + Mathf.Sin(ph) * 0.5f;
        }
        return ToClip("Rumble", MakeSeamless(raw, fade));
    }

    private AudioClip MakeWind()
    {
        int fade = SampleRate;
        var raw = new float[6 * SampleRate + fade];
        float b0 = 0, b1 = 0, b2 = 0, lp = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)SampleRate;
            float w = Rand();
            b0 = 0.99765f * b0 + w * 0.0990460f;
            b1 = 0.96300f * b1 + w * 0.2965164f;
            b2 = 0.57000f * b2 + w * 1.0526913f;
            float pink = b0 + b1 + b2 + w * 0.1848f;
            lp += (pink - lp) * 0.2f;
            float mod = 0.6f + 0.25f * Mathf.Sin(TwoPi * t / 6f) + 0.15f * Mathf.Sin(TwoPi * 2f * t / 6f + 1f);
            raw[i] = lp * mod;
        }
        return ToClip("Wind", MakeSeamless(raw, fade));
    }

    private AudioClip MakeHeartbeat()
    {
        var b = new float[Mathf.CeilToInt(0.8f * SampleRate)];
        AddThump(b, 0.02f, 55f, 1f);
        AddThump(b, 0.26f, 48f, 0.7f);
        return ToClip("Heartbeat", b);
    }

    private static void AddThump(float[] b, float start, float freq, float amp)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int n = Mathf.Min(b.Length - s, Mathf.RoundToInt(0.2f * SampleRate));
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            ph += TwoPi * freq * (1f - 0.35f * u) / SampleRate;
            float env = Mathf.Min(1f, i / (0.012f * SampleRate)) * Mathf.Exp(-u * 5f);
            b[s + i] += Mathf.Sin(ph) * env * amp;
        }
    }

    private AudioClip MakeBossBreath()
    {
        int fade = SampleRate / 2;
        var raw = new float[3 * SampleRate + fade];
        float brown = 0f, lp = 0f, ph = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)SampleRate;
            brown = Mathf.Clamp(brown + Rand() * 0.06f, -1f, 1f) * 0.997f;
            lp += (brown - lp) * 0.08f;
            float mod = 0.55f + 0.45f * Mathf.Sin(TwoPi * 0.5f * t);
            ph += TwoPi * (70f + 10f * mod) / SampleRate;
            float growl = Mathf.Sin(ph) * (0.6f + 0.4f * Mathf.Sin(TwoPi * 23f * t));
            raw[i] = (lp * 3f + growl * 0.35f) * mod;
        }
        return ToClip("BossBreath", MakeSeamless(raw, fade));
    }
}
