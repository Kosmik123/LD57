using UnityEngine;

public enum RelicEffect
{
    IronWill,     // +Value% obrażeń
    LightFeet,    // +Value% prędkości
    VampireFang,  // Value% lifesteal
    Thorns,       // Value obrażeń napastnikowi
    Luck,         // Value% szansy na kryt (×2)
    SecondWind,   // serce leczy +Value HP więcej
    Greed,        // esencja ×Value
    EliteHunter,  // +Value% obrażeń elitom
    ThickSkin,    // -Value% otrzymywanych obrażeń
    Echo,         // Value% szansy, że skill nie wejdzie na cooldown
}

/// <summary>Relikt jako asset (Assets/Relics/*.asset, tworzy P12Wiring_Mechanics). Efekt aplikuje RelicHolder do PlayerStats.</summary>
[CreateAssetMenu(menuName = "LD57/Relic Definition", fileName = "Relic")]
public class RelicDefinition : ScriptableObject
{
    public string DisplayName = "Relikt";
    [TextArea] public string Description;
    [Tooltip("Litera w ikonie (sześciokąt).")] public string Letter = "R";
    public Color Color = new Color(0.9f, 0.75f, 0.2f);
    public RelicEffect Effect;
    [Tooltip("Wartość efektu (jednostka zależy od efektu, patrz RelicEffect).")] public float Value = 10f;
}

/// <summary>Ikona reliktu: kolorowy sześciokąt generowany w kodzie (jedna współdzielona tekstura).</summary>
public static class RelicIcons
{
    private static Sprite hex;

    public static Sprite Hexagon
    {
        get
        {
            if (hex != null) return hex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Relic Hexagon (runtime)", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f, r = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // sześciokąt foremny ze szpicem w górę: |x| ≤ √3/2·r oraz 0,5·|x| + √3/2·|y| ≤ √3/2·r
                    float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c);
                    float k = 0.8660254f;
                    float edge = Mathf.Max(dx / (k * r), (dx * 0.5f + dy * k) / (k * r));
                    float a = Mathf.Clamp01((1f - edge) * r * 0.5f + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            hex = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            hex.name = "Relic Hexagon";
            return hex;
        }
    }
}
