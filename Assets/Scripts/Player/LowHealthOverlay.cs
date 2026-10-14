using UnityEngine;
using UnityEngine.UI;

/// <summary>Czerwona winieta pulsująca 1 Hz, gdy HP gracza spadnie poniżej 30%. Obiekt z Image w canvasie Game UI.</summary>
[RequireComponent(typeof(Image))]
public class LowHealthOverlay : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField, Range(0f, 1f)] private float threshold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.5f;

    private Image image;
    private float intensity;
    private bool low;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = CreateVignetteSprite();
        image.color = new Color(0.8f, 0.03f, 0.03f, 0f);
    }

    private void Start()
    {
        if (playerHealth == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerHealth = pc.GetComponent<Health>();
        }
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += Health_OnHealthChanged;
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Health_OnHealthChanged;
    }

    private void Health_OnHealthChanged(int delta) => Refresh();

    private void Refresh()
    {
        low = playerHealth != null && !playerHealth.IsDead
              && playerHealth.CurrentHealth < threshold * playerHealth.MaxHealth;
    }

    private void Update()
    {
        intensity = Mathf.MoveTowards(intensity, low ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);
        var c = image.color;
        c.a = maxAlpha * intensity * pulse;
        image.color = c;
    }

    private static Sprite CreateVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette (runtime)" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
