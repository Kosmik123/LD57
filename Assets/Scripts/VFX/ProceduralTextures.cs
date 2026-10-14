using UnityEngine;

/// <summary>
/// Deterministyczne, kafelkowalne generatory tekstur skaly (szum fBm). Uzywane przez edytor (P9Wiring_Materials)
/// i dostepne w runtime. Zwracaja tablice kolorow size*size (wiersz po wierszu).
/// </summary>
public static class ProceduralTextures
{
    public const int DefaultSize = 512;

    // ---- hash / szum (okresowy, wiec tekstura sie kafelkuje)
    static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / (float)0x1000000;

    static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = Mod(x0, period), xb = Mod(x0 + 1, period), ya = Mod(y0, period), yb = Mod(y0 + 1, period);
        float a = Rand(xa, ya, seed), b = Rand(xb, ya, seed), c = Rand(xa, yb, seed), d = Rand(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>fBm w [0,1], okresowy na [0,1) po u,v.</summary>
    public static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int freq = baseFreq;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * ValueNoise(u * freq, v * freq, freq, seed + o * 31);
            norm += amp;
            amp *= 0.5f;
            freq *= 2;
        }
        return sum / norm;
    }

    /// <summary>Heightmapa skaly [0,1]: fBm 5 oktaw + zylowe grzbiety.</summary>
    public static float[] Height(int size = DefaultSize, int seed = 1337)
    {
        var h = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u, v, 4, 5, seed);
                float ridge = 1f - Mathf.Abs(Fbm(u, v, 3, 4, seed + 101) * 2f - 1f); // grzbiety
                h[y * size + x] = Mathf.Clamp01(n * 0.8f + Mathf.Pow(ridge, 4f) * 0.25f);
            }
        return h;
    }

    /// <summary>Albedo w szarosciach: fBm + jasniejsze zyly + ciemne pory.</summary>
    public static Color32[] RockAlbedo(int size = DefaultSize, int seed = 1337)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u, v, 4, 5, seed);
                float val = 0.35f + n * 0.5f;
                // zyly: waska linia wokol zera zaburzonego szumu
                float vein = 1f - Mathf.Clamp01(Mathf.Abs(Fbm(u, v, 3, 3, seed + 7) - 0.5f) * 14f);
                val += vein * vein * 0.22f;
                // pory: male ciemne plamy
                float pore = Fbm(u, v, 32, 2, seed + 13);
                if (pore > 0.68f) val -= (pore - 0.68f) * 2.2f;
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(val) * 255f);
                px[y * size + x] = new Color32(g, g, g, 255);
            }
        return px;
    }

    /// <summary>Normal map (przestrzen stycznych) z heightmapy; kafelkowalna.</summary>
    public static Color32[] RockNormal(int size = DefaultSize, int seed = 1337, float strength = 2f)
    {
        var h = Height(size, seed);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = h[y * size + Mod(x - 1, size)], r = h[y * size + Mod(x + 1, size)];
                float d = h[Mod(y - 1, size) * size + x], u = h[Mod(y + 1, size) * size + x];
                var n = new Vector3((l - r) * strength * 4f, (d - u) * strength * 4f, 1f).normalized;
                px[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        return px;
    }

    /// <summary>Maska AO/brudu: jasno = czysto, ciemno = dolki (niskie fragmenty heightmapy + szum).</summary>
    public static Color32[] RockMask(int size = DefaultSize, int seed = 1337)
    {
        var h = Height(size, seed);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float dirt = Fbm(u, v, 8, 4, seed + 55);
                float val = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0.15f, 0.7f, h[y * size + x])) * Mathf.Lerp(0.7f, 1f, dirt);
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(val) * 255f);
                px[y * size + x] = new Color32(g, g, g, 255);
            }
        return px;
    }

    /// <summary>Albedo mchu/trawy (zielone, kafelkowalne): fBm + drobne kepki + ciemne plamy. Mnozone przez _TopColor.</summary>
    public static Color32[] MossAlbedo(int size = DefaultSize, int seed = 4242)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u, v, 6, 5, seed);
                float tuft = Fbm(u, v, 40, 2, seed + 9);
                float patch = Fbm(u, v, 3, 3, seed + 21);
                float val = 0.45f + n * 0.4f + (tuft - 0.5f) * 0.25f - Mathf.Clamp01(patch - 0.6f) * 0.5f;
                val = Mathf.Clamp01(val);
                float yellow = Mathf.Clamp01(Fbm(u, v, 4, 3, seed + 33) - 0.45f) * 0.6f;   // przebarwienia ku zolci
                px[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(val * (0.75f + yellow)) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(val) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(val * 0.55f) * 255f), 255);
            }
        return px;
    }

    /// <summary>Skala z jasnobłekitnymi zylami (wariant RockAlbedo): szara baza + swiecace zyly o barwie cyjan.</summary>
    public static Color32[] RockVeinsAlbedo(int size = DefaultSize, int seed = 1337)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u, v, 4, 5, seed);
                float val = 0.35f + n * 0.5f;
                float pore = Fbm(u, v, 32, 2, seed + 13);
                if (pore > 0.68f) val -= (pore - 0.68f) * 2.2f;
                // zyly kryształu: waskie linie wokol zera dwoch zaburzonych szumow
                float v1 = 1f - Mathf.Clamp01(Mathf.Abs(Fbm(u, v, 3, 3, seed + 71) - 0.5f) * 22f);
                float v2 = 1f - Mathf.Clamp01(Mathf.Abs(Fbm(u, v, 5, 3, seed + 97) - 0.5f) * 30f);
                float vein = Mathf.Clamp01(v1 * v1 + v2 * v2 * 0.7f);
                var r = Mathf.Lerp(val, 0.35f, vein);
                var g = Mathf.Lerp(val, 0.85f, vein);
                var b = Mathf.Lerp(val, 1.0f, vein);
                px[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f), 255);
            }
        return px;
    }
}
