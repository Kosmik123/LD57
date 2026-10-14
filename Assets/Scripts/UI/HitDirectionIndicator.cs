using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Czerwony klin przy krawędzi ekranu w stronę źródła obrażeń gracza (atakujący wróg w 2,5 m albo pocisk w 4 m).
/// Bez źródła: czerwona obwódka całego ekranu 0,3 s. Obiekt rozciągnięty na cały canvas `Game UI`.
/// </summary>
public class HitDirectionIndicator : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private float meleeRadius = 2.5f;
    [SerializeField] private float projectileRadius = 4f;
    [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.8f;
    [SerializeField] private float duration = 0.5f;

    private const int PoolSize = 3;
    private static readonly Color Red = new Color(0.9f, 0.05f, 0.05f);
    private readonly Image[] wedges = new Image[PoolSize];
    private int next;
    private Image border;
    private RectTransform rect;

    private void Awake()
    {
        rect = (RectTransform)transform;
        var wedgeSprite = CreateWedgeSprite();
        for (int i = 0; i < PoolSize; i++)
        {
            wedges[i] = CreateImage("Wedge " + i, wedgeSprite);
            wedges[i].rectTransform.anchorMin = wedges[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        }
        border = CreateImage("Border", CreateBorderSprite());
        var r = border.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private Image CreateImage(string label, Sprite sprite)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(Red.r, Red.g, Red.b, 0f);
        return img;
    }

    private void Start()
    {
        if (playerHealth == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerHealth = pc.GetComponent<Health>();
        }
        if (playerHealth != null) playerHealth.OnHealthChanged += Health_OnHealthChanged;
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Health_OnHealthChanged;
        foreach (var w in wedges) if (w != null) w.DOKill();
        if (border != null) border.DOKill();
    }

    private void Health_OnHealthChanged(int delta)
    {
        if (delta >= 0) return;
        var cam = Camera.main;
        Vector3 origin = playerHealth.transform.position;
        if (cam != null && TryFindSource(origin, out Vector3 sourcePos))
        {
            Vector3 dir = sourcePos - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector3 reference = Flat(cam.transform.forward);
                if (reference.sqrMagnitude < 0.01f) reference = Flat(cam.transform.up);
                float angle = Vector3.SignedAngle(reference, dir, Vector3.up); // + = w prawo
                ShowWedge(angle);
                return;
            }
        }
        Fade(border, 0.3f);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private bool TryFindSource(Vector3 origin, out Vector3 pos)
    {
        pos = default;
        float best = float.MaxValue;
        bool found = false;
        foreach (var e in FindObjectsByType<EnemyMeleeAttack>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(origin, e.transform.position);
            if ((e.IsAttacking || d <= meleeRadius) && d < best) { best = d; pos = e.transform.position; found = true; }
        }
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(origin, p.transform.position);
            if (d <= projectileRadius && d < best) { best = d; pos = p.transform.position; found = true; }
        }
        return found;
    }

    private void ShowWedge(float angle)
    {
        var w = wedges[next];
        next = (next + 1) % PoolSize;
        float size = Mathf.Min(rect.rect.width, rect.rect.height);
        w.rectTransform.sizeDelta = new Vector2(size, size);
        w.rectTransform.anchoredPosition = Vector2.zero;
        w.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        Fade(w, duration);
    }

    private void Fade(Image img, float time)
    {
        img.DOKill();
        var c = img.color; c.a = peakAlpha; img.color = c;
        img.DOFade(0f, time).SetUpdate(true).SetTarget(img);
    }

    /// <summary>Kwadratowy sprite: łuk pierścienia u góry (±28°), rotowany wokół środka ekranu.</summary>
    private static Sprite CreateWedgeSprite()
    {
        const int size = 128;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.97f, r)) * (1f - Mathf.SmoothStep(0.96f, 1.02f, r))
                      * (1f - Mathf.SmoothStep(14f, 28f, ang));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }
        return MakeSprite(px, size, "HitWedge (runtime)");
    }

    private static Sprite CreateBorderSprite()
    {
        const int size = 128;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
            float dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
            float a = Mathf.SmoothStep(0.7f, 1f, Mathf.Max(dx, dy));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        return MakeSprite(px, size, "HitBorder (runtime)");
    }

    private static Sprite MakeSprite(Color32[] px, int size, string name)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
