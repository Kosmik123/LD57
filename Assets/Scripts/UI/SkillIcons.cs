using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ikony skilli rysowane w kodzie (96x96, tło koliste, 2 kolory + ciemny obrys, 2x supersampling).
/// Runtime: <see cref="Get(SkillDefinition)"/> zwraca <c>Icon</c> z assetu, a gdy go brak - sprite wygenerowany w pamięci.
/// Edytor (<c>P12Wiring_SkillUi</c>) zapisuje te same piksele do <c>Assets/Graphics/Icons/&lt;Kind&gt;.png</c>.
/// </summary>
public static class SkillIcons
{
    public const int Size = 96;
    private const int SuperSample = 2;

    private static readonly Dictionary<SkillKind, Sprite> cache = new Dictionary<SkillKind, Sprite>();
    private static Sprite heart;
    private static Sprite roundedSquare;
    private static Sprite disc;

    // ------------------------------------------------------------------ API

    /// <summary>Ikona z assetu albo (gdy null) wygenerowana w pamięci. Null tylko dla null definicji.</summary>
    public static Sprite Get(SkillDefinition definition)
    {
        if (definition == null) return null;
        return definition.Icon != null ? definition.Icon : Get(definition.Kind);
    }

    /// <summary>Ikona wygenerowana w pamięci (cache).</summary>
    public static Sprite Get(SkillKind kind)
    {
        if (cache.TryGetValue(kind, out var s) && s != null) return s;
        s = ToSprite(Render(kind), "SkillIcon " + kind + " (runtime)");
        cache[kind] = s;
        return s;
    }

    /// <summary>Serce na kartę „+HP”.</summary>
    public static Sprite HeartIcon()
    {
        if (heart == null) heart = ToSprite(RenderHeart(), "SkillIcon Heart (runtime)");
        return heart;
    }

    /// <summary>Tekstura 96x96 do zapisu jako PNG.</summary>
    public static Texture2D CreateTexture(SkillKind kind) => ToTexture(Render(kind), "SkillIcon " + kind);

    /// <summary>Zaokrąglony kwadrat 9-slice (tło slotów, gdy nie ma wbudowanego UISprite).</summary>
    public static Sprite RoundedSquare()
    {
        if (roundedSquare != null) return roundedSquare;
        const int n = 32; const float r = 8f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float qx = Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - r);
            float qy = Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - r);
            float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
        }
        var tex = MakeTexture(px, n, n, "Rounded Square (runtime)");
        roundedSquare = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
        return roundedSquare;
    }

    /// <summary>Pełne koło (nakładka cooldownu typu Radial360).</summary>
    public static Sprite Disc()
    {
        if (disc != null) return disc;
        const int n = 64;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f)) - n * 0.5f;
            px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
        }
        disc = Sprite.Create(MakeTexture(px, n, n, "Disc (runtime)"), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return disc;
    }

    // ------------------------------------------------------------------ tekstury

    private static Texture2D MakeTexture(Color32[] px, int w, int h, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    private static Texture2D ToTexture(Color32[] px, string name) => MakeTexture(px, Size, Size, name);

    private static Sprite ToSprite(Color32[] px, string name)
    {
        var tex = ToTexture(px, name);
        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }

    // ------------------------------------------------------------------ rysowanie

    private struct Palette
    {
        public Color A, B;
        public Palette(Color a, Color b) { A = a; B = b; }
    }

    private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    private static Palette PaletteOf(SkillKind kind)
    {
        switch (kind)
        {
            case SkillKind.Spin:           return new Palette(C(0.78f, 0.88f, 0.98f), C(1f, 0.72f, 0.2f));
            case SkillKind.Charge:         return new Palette(C(1f, 0.46f, 0.18f), C(1f, 0.86f, 0.4f));
            case SkillKind.GroundSlam:     return new Palette(C(0.72f, 0.78f, 0.88f), C(0.92f, 0.52f, 0.18f));
            case SkillKind.AxeThrow:       return new Palette(C(0.82f, 0.88f, 0.94f), C(0.78f, 0.48f, 0.24f));
            case SkillKind.WarCry:         return new Palette(C(0.97f, 0.76f, 0.36f), C(0.96f, 0.28f, 0.24f));
            case SkillKind.Fireball:       return new Palette(C(1f, 0.4f, 0.1f), C(1f, 0.9f, 0.32f));
            case SkillKind.FrostNova:      return new Palette(C(0.3f, 0.7f, 1f), C(0.92f, 0.98f, 1f));
            case SkillKind.ArcaneOrbs:     return new Palette(C(0.7f, 0.42f, 1f), C(0.96f, 0.82f, 1f));
            case SkillKind.Blink:          return new Palette(C(0.4f, 0.88f, 0.96f), C(0.62f, 0.44f, 0.9f));
            case SkillKind.ChainLightning: return new Palette(C(1f, 0.93f, 0.36f), C(0.5f, 0.8f, 1f));
            default:                       return new Palette(C(0.9f, 0.9f, 0.9f), C(0.6f, 0.6f, 0.6f));
        }
    }

    /// <summary>Piksele 96x96 ikony (RGBA, kolejność Unity: wiersz 0 = dół).</summary>
    public static Color32[] Render(SkillKind kind)
    {
        var pal = PaletteOf(kind);
        var cv = new Canvas(Size, SuperSample);
        switch (kind)
        {
            case SkillKind.Spin: DrawSpin(cv, pal); break;
            case SkillKind.Charge: DrawCharge(cv, pal); break;
            case SkillKind.GroundSlam: DrawGroundSlam(cv, pal); break;
            case SkillKind.AxeThrow: DrawAxe(cv, pal); break;
            case SkillKind.WarCry: DrawWarCry(cv, pal); break;
            case SkillKind.Fireball: DrawFireball(cv, pal); break;
            case SkillKind.FrostNova: DrawFrost(cv, pal); break;
            case SkillKind.ArcaneOrbs: DrawOrbs(cv, pal); break;
            case SkillKind.Blink: DrawBlink(cv, pal); break;
            case SkillKind.ChainLightning: DrawChain(cv, pal); break;
        }
        return cv.Finish(pal.A);
    }

    private static Color32[] RenderHeart()
    {
        var pal = new Palette(C(0.92f, 0.16f, 0.22f), C(1f, 0.6f, 0.62f));
        var cv = new Canvas(Size, SuperSample);
        cv.Xf(0.85f, 0f);
        cv.Disc(-13, 10, 16, pal.A);
        cv.Disc(13, 10, 16, pal.A);
        cv.Poly(pal.A, -28, 4, 28, 4, 0, -34);
        cv.Disc(-14, 14, 5, pal.B);
        return cv.Finish(pal.A);
    }

    // Glify rysowane w lokalnych jednostkach ok. ±40 (środek ikony = 0,0, oś Y w górę).

    private static void DrawSpin(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f);
        const int steps = 48;
        Vector2 prev = Vector2.zero, cur = Vector2.zero;
        for (int k = 0; k <= steps; k++)
        {
            float t = k / (float)steps;
            float ang = Mathf.Deg2Rad * (210f + t * 700f);
            float r = 4f + t * 25f;
            cur = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            if (k > 0) cv.Seg(prev.x, prev.y, cur.x, cur.y, 3.5f + t * 4f, p.A);
            prev = cur;
        }
        var tan = (cur - prev).normalized;
        var nrm = new Vector2(-tan.y, tan.x);
        var tip = cur + tan * 12f;
        var l = cur + nrm * 10f; var rr = cur - nrm * 10f;
        cv.Poly(p.B, l.x, l.y, rr.x, rr.y, tip.x, tip.y);
        cv.Disc(0, 0, 4.5f, p.B);
    }

    private static void DrawCharge(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f);
        cv.Seg(-38, 15, -12, 15, 5f, p.B);
        cv.Seg(-38, -15, -12, -15, 5f, p.B);
        cv.Seg(-26, 0, 12, 0, 11f, p.A);
        cv.Poly(p.A, 8, -19, 8, 19, 40, 0);
    }

    private static void DrawGroundSlam(Canvas cv, Palette p)
    {
        // podłoże, linie wybuchu i odłamki
        cv.Xf(0.85f, 0f);
        cv.Seg(-38, -25, 38, -25, 6f, p.A);
        foreach (float deg in new[] { 14f, 46f, 134f, 166f })
        {
            float a = deg * Mathf.Deg2Rad;
            var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            cv.Seg(-7 + u.x * 16, -25 + u.y * 16, -7 + u.x * 30, -25 + u.y * 30, 4f, p.B);
        }
        cv.Disc(-30, -12, 3f, p.A);
        cv.Disc(22, -14, 3.5f, p.A);
        // młot opada pod kątem
        cv.Xf(0.85f, 30f, -1f, 3f);
        cv.Seg(0, 4, 0, 42, 8f, p.B);
        cv.PolySep(p.A, -18, -16, 18, -16, 18, 9, -18, 9);
        cv.Seg(-12, 2, 12, 2, 3f, p.B);
    }

    private static void DrawAxe(Canvas cv, Palette p)
    {
        cv.Xf(0.74f, -32f, -3f, 0f);
        cv.Seg(0, -38, 0, 38, 7.5f, p.B);
        cv.Disc(0, -38, 5f, p.B);
        cv.PolySep(p.A, 3, 34, 22, 44, 33, 30, 37, 15, 33, 0, 22, -14, 3, -4);
    }

    private static void DrawWarCry(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f, 0f, -2f);
        var dark = new Color(0.1f, 0.04f, 0.04f, 1f);
        cv.PolySep(p.B, -13, 14, -24, 34, -6, 22);
        cv.PolySep(p.B, 13, 14, 24, 34, 6, 22);
        cv.Disc(0, 0, 17, p.A, true);
        cv.PolySep(p.B, -17, 4, -15, 15, -8, 22, 8, 22, 15, 15, 17, 4);
        cv.Seg(-8, -1, -3, -3, 3f, dark);
        cv.Seg(8, -1, 3, -3, 3f, dark);
        cv.Disc(0, -9, 5f, dark);
        cv.Arc(0, -1, 27, -32, 32, 4.5f, p.B);
        cv.Arc(0, -1, 36, -28, 28, 4.5f, p.B);
        cv.Arc(0, -1, 27, 148, 212, 4.5f, p.B);
        cv.Arc(0, -1, 36, 152, 208, 4.5f, p.B);
    }

    private static void DrawFireball(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f);
        cv.Poly(p.A, -2, 23, -38, -36, 22, -3);
        cv.Poly(p.A, 2, 4, -22, -30, 16, -2);
        cv.Disc(10, 10, 19, p.A);
        cv.Poly(p.B, 3, 17, -18, -22, 17, 3);
        cv.Disc(10, 10, 11, p.B);
        cv.Disc(7, 13, 4, new Color(1f, 1f, 0.85f, 1f));
    }

    private static void DrawFrost(Canvas cv, Palette p)
    {
        cv.Xf(0.9f, 0f);
        for (int k = 0; k < 6; k++)
        {
            float a = (90f + 60f * k) * Mathf.Deg2Rad;
            var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            cv.Seg(0, 0, u.x * 37, u.y * 37, 5.5f, p.B);
            AddBranch(cv, p, u, 19f, 12f, 4.5f);
            AddBranch(cv, p, u, 29f, 8f, 4f);
            cv.Disc(u.x * 37, u.y * 37, 3.8f, p.A);
        }
        cv.Disc(0, 0, 8f, p.A);
        cv.Disc(0, 0, 3.5f, p.B);
    }

    private static void AddBranch(Canvas cv, Palette p, Vector2 u, float dist, float len, float w)
    {
        foreach (float s in new[] { 55f, -55f })
        {
            float a = Mathf.Atan2(u.y, u.x) + s * Mathf.Deg2Rad;
            var b = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
            cv.Seg(u.x * dist, u.y * dist, u.x * dist + b.x, u.y * dist + b.y, w, p.B);
        }
    }

    private static void DrawOrbs(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f);
        cv.Ring(0, 0, 26, 3.5f, p.B);
        cv.Disc(0, 0, 6f, p.B);
        for (int k = 0; k < 3; k++)
        {
            float a = (90f + 120f * k) * Mathf.Deg2Rad;
            float x = Mathf.Cos(a) * 26f, y = Mathf.Sin(a) * 26f;
            cv.Disc(x, y, 12f, p.A, true);
            cv.Disc(x - 3.5f, y + 3.5f, 4.5f, p.B);
        }
    }

    private static void DrawBlink(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f, 0f, -2f);
        var ghost = Color.Lerp(p.B, new Color(0.1f, 0.1f, 0.2f, 1f), 0.2f);
        DrawFigure(cv, -28f, ghost);
        DrawFigure(cv, 28f, p.A);
        cv.Seg(-11, -6, -6, -6, 4f, p.B);
        cv.Seg(-2, -6, 3, -6, 4f, p.B);
        cv.Poly(p.B, 8, -12, 8, 0, 17, -6);
    }

    private static void DrawFigure(Canvas cv, float x, Color col)
    {
        cv.Disc(x, 17, 9f, col);
        cv.PolySep(col, x - 14, -24, x + 14, -24, x + 14, -9, x + 8, 3, x - 8, 3, x - 14, -9);
    }

    private static void DrawChain(Canvas cv, Palette p)
    {
        cv.Xf(0.85f, 0f);
        cv.Seg(-31, 24, -9, 14, 3.5f, p.B);
        cv.Seg(31, -26, 10, -14, 3.5f, p.B);
        cv.Disc(-31, 24, 6.5f, p.B);
        cv.Disc(31, -26, 6.5f, p.B);
        cv.Poly(p.A, 12, 40, -17, 0, -2, 0, -11, -40, 17, 5, 2, 5);
    }

    // ------------------------------------------------------------------ rasteryzer

    /// <summary>
    /// Płótno SDF: kształty w jednostkach logicznych (0..Size), warstwa obrysu pod warstwą glifu, na końcu tło koliste
    /// i uśrednienie SuperSample x SuperSample (kolory premultiplikowane).
    /// </summary>
    private sealed class Canvas
    {
        private const float OutlineW = 2.6f;
        private const float SepW = 1.1f;
        private static readonly Color Dark = new Color(0.04f, 0.04f, 0.07f, 1f);

        private readonly int size;
        private readonly int n;
        private readonly float s;
        private readonly Color[] outline;
        private readonly Color[] glyph;

        private float scale = 1f, cos = 1f, sin = 0f, ox = Size * 0.5f, oy = Size * 0.5f;

        public Canvas(int size, int superSample)
        {
            this.size = size;
            s = superSample;
            n = size * superSample;
            outline = new Color[n * n];
            glyph = new Color[n * n];
        }

        /// <summary>Transformacja lokalna: skala, obrót (stopnie) i przesunięcie względem środka ikony.</summary>
        public void Xf(float scale, float rotDeg, float dx = 0f, float dy = 0f)
        {
            this.scale = scale;
            float r = rotDeg * Mathf.Deg2Rad;
            cos = Mathf.Cos(r); sin = Mathf.Sin(r);
            ox = size * 0.5f + dx; oy = size * 0.5f + dy;
        }

        private Vector2 P(float x, float y) => new Vector2(ox + scale * (x * cos - y * sin), oy + scale * (x * sin + y * cos));

        public void Disc(float cx, float cy, float r, Color col, bool sep = false)
        {
            var c = P(cx, cy); float rr = r * scale;
            Fill(c.x - rr, c.y - rr, c.x + rr, c.y + rr, col, sep, (x, y) => Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y)) - rr);
        }

        public void Ring(float cx, float cy, float r, float w, Color col, bool sep = false)
        {
            var c = P(cx, cy); float rr = r * scale, hw = w * scale * 0.5f;
            Fill(c.x - rr - hw, c.y - rr - hw, c.x + rr + hw, c.y + rr + hw, col, sep,
                (x, y) => Mathf.Abs(Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y)) - rr) - hw);
        }

        public void Seg(float ax, float ay, float bx, float by, float w, Color col, bool sep = false)
        {
            var a = P(ax, ay); var b = P(bx, by); float hw = w * scale * 0.5f;
            Fill(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw, col, sep,
                (x, y) => DistSeg(x, y, a, b) - hw);
        }

        /// <summary>Wypełniony wielokąt (pary współrzędnych x, y).</summary>
        public void Poly(Color col, params float[] xy) => PolyImpl(col, false, xy);

        /// <summary>Wielokąt z cienkim ciemnym obrysem oddzielającym od warstwy pod spodem.</summary>
        public void PolySep(Color col, params float[] xy) => PolyImpl(col, true, xy);

        private void PolyImpl(Color col, bool sep, float[] xy)
        {
            int cnt = xy.Length / 2;
            var pts = new Vector2[cnt];
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            for (int i = 0; i < cnt; i++)
            {
                pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
                minx = Mathf.Min(minx, pts[i].x); maxx = Mathf.Max(maxx, pts[i].x);
                miny = Mathf.Min(miny, pts[i].y); maxy = Mathf.Max(maxy, pts[i].y);
            }
            Fill(minx, miny, maxx, maxy, col, sep, (x, y) => DistPoly(x, y, pts));
        }

        /// <summary>Łuk okręgu od a0 do a1 stopni (przeciwnie do ruchu wskazówek), grubość w.</summary>
        public void Arc(float cx, float cy, float r, float a0Deg, float a1Deg, float w, Color col, bool sep = false)
        {
            var c = P(cx, cy); float rr = r * scale, hw = w * scale * 0.5f;
            float rot = Mathf.Atan2(sin, cos);
            float a0 = a0Deg * Mathf.Deg2Rad + rot, a1 = a1Deg * Mathf.Deg2Rad + rot;
            float span = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
            var e0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr;
            var e1 = c + new Vector2(Mathf.Cos(a0 + span), Mathf.Sin(a0 + span)) * rr;
            Fill(c.x - rr - hw, c.y - rr - hw, c.x + rr + hw, c.y + rr + hw, col, sep, (x, y) =>
            {
                float dx = x - c.x, dy = y - c.y;
                float ang = Mathf.Atan2(dy, dx);
                if (Mathf.Repeat(ang - a0, Mathf.PI * 2f) <= span)
                    return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - rr) - hw;
                float d0 = Mathf.Sqrt((x - e0.x) * (x - e0.x) + (y - e0.y) * (y - e0.y));
                float d1 = Mathf.Sqrt((x - e1.x) * (x - e1.x) + (y - e1.y) * (y - e1.y));
                return Mathf.Min(d0, d1) - hw;
            });
        }

        private static float DistSeg(float x, float y, Vector2 a, Vector2 b)
        {
            float abx = b.x - a.x, aby = b.y - a.y;
            float len2 = abx * abx + aby * aby;
            float t = len2 > 1e-6f ? Mathf.Clamp01(((x - a.x) * abx + (y - a.y) * aby) / len2) : 0f;
            float px = a.x + abx * t - x, py = a.y + aby * t - y;
            return Mathf.Sqrt(px * px + py * py);
        }

        private static float DistPoly(float x, float y, Vector2[] p)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                best = Mathf.Min(best, DistSeg(x, y, p[j], p[i]));
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside ? -best : best;
        }

        private void Fill(float minx, float miny, float maxx, float maxy, Color col, bool sep, Func<float, float, float> sdf)
        {
            float pad = OutlineW + 1.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minx - pad) * s));
            int x1 = Mathf.Min(n - 1, Mathf.CeilToInt((maxx + pad) * s));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((miny - pad) * s));
            int y1 = Mathf.Min(n - 1, Mathf.CeilToInt((maxy + pad) * s));
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float d = sdf((px + 0.5f) / s, (py + 0.5f) / s);
                if (d > OutlineW + 1f) continue;
                int i = py * n + px;
                Over(ref outline[i], Dark, 0.5f - (d - OutlineW) * s);
                if (sep) Over(ref glyph[i], Dark, 0.5f - (d - SepW) * s);
                Over(ref glyph[i], col, 0.5f - d * s);
            }
        }

        private static void Over(ref Color dst, Color src, float coverage)
        {
            float sa = src.a * Mathf.Clamp01(coverage);
            if (sa <= 0f) return;
            float da = dst.a * (1f - sa);
            float oa = sa + da;
            dst.r = (src.r * sa + dst.r * da) / oa;
            dst.g = (src.g * sa + dst.g * da) / oa;
            dst.b = (src.b * sa + dst.b * da) / oa;
            dst.a = oa;
        }

        /// <summary>Składa tło + obrys + glif i zmniejsza do docelowego rozmiaru.</summary>
        public Color32[] Finish(Color accent)
        {
            var full = new Color[n * n];
            float center = n * 0.5f;
            var bgTop = new Color(0.17f, 0.17f, 0.22f, 1f);
            var bgBottom = new Color(0.05f, 0.05f, 0.08f, 1f);
            var tint = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f);
            var rim = new Color(accent.r * 0.62f, accent.g * 0.62f, accent.b * 0.62f, 1f);
            for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                int i = py * n + px;
                float dx = px + 0.5f - center, dy = py + 0.5f - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / s; // jednostki logiczne
                var o = new Color(0, 0, 0, 0);
                Over(ref o, Color.Lerp(bgBottom, bgTop, py / (float)n), 0.5f - (dist - 47f) * s);
                var glow = Color.Lerp(Color.Lerp(bgBottom, bgTop, py / (float)n), tint, 0.3f * Mathf.Clamp01(1f - dist / 47f));
                Over(ref o, glow, (0.5f - (dist - 47f) * s) * 0.9f);
                Over(ref o, rim, 0.5f - (Mathf.Abs(dist - 45.3f) - 1.5f) * s);
                Over(ref o, outline[i], outline[i].a);
                Over(ref o, glyph[i], glyph[i].a);
                full[i] = o;
            }

            int ss = Mathf.RoundToInt(s);
            var result = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var c = full[(y * ss + sy) * n + x * ss + sx];
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                }
                float cnt = ss * ss;
                if (a <= 0f) { result[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                result[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(r / a) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(g / a) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(b / a) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(a / cnt) * 255f));
            }
            return result;
        }
    }
}
