using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Wspólne efekty świata dla LevelUpBurst i WaveClearedToast (napis, pierścień, iskry). Czas rzeczywisty.</summary>
public static class UiFeelFx
{
    private static Texture2D ringTexture, dotTexture;

    private static Material Mat(Texture2D tex)
    {
        var m = SwordTrail.CreateTransparentMaterial("UiFeel (runtime)", Color.white);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        return m;
    }

    public static TextMeshPro SpawnText(string text, Vector3 pos, float fontSize, Color color, TMP_FontAsset font, Camera cam, out Transform t)
    {
        // TextMeshPro wymaga RectTransform: dodanie go do obiektu ze zwykłym Transformem PODMIENIA transform
        // (stara referencja staje się zniszczona), więc obiekt powstaje od razu z RectTransform.
        var go = new GameObject("UiFeel Text", typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshPro>();
        t = go.transform;
        t.position = pos;
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 3f);
        if (cam != null) t.rotation = cam.transform.rotation;
        return tmp;
    }

    /// <summary>Billboard przez czas życia obiektu (tween na czasie rzeczywistym).</summary>
    public static void Billboard(Transform t, Camera cam, float duration)
    {
        if (cam == null) return;
        DOVirtual.Float(0f, 1f, duration, _ => { if (t != null && cam != null) t.rotation = cam.transform.rotation; })
            .SetUpdate(true).SetTarget(t);
    }

    public static void Ring(Vector3 center, Color color, float maxRadius, float duration)
    {
        if (ringTexture == null) ringTexture = MakeRing();
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "UiFeel Ring";
        go.transform.position = center + Vector3.up * 0.05f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.3f;
        var r = go.GetComponent<MeshRenderer>();
        var mat = Mat(ringTexture);
        mat.SetColor("_BaseColor", color);
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        DOTween.Sequence().SetTarget(go).SetUpdate(true)
            .Join(go.transform.DOScale(maxRadius * 2f, duration).SetEase(Ease.OutCubic))
            .Join(DOVirtual.Float(color.a, 0f, duration, a => { var c = color; c.a = a; mat.SetColor("_BaseColor", c); }).SetEase(Ease.InQuad))
            .OnComplete(() => { Object.Destroy(mat); Object.Destroy(go); });
    }

    public static void Sparks(Vector3 pos, Color color, int count, float speed, float life)
    {
        if (dotTexture == null) dotTexture = MakeDot();
        var go = new GameObject("UiFeel Sparks");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // forward = w górę
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = color;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 0;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.5f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var mat = Mat(dotTexture);
        rend.sharedMaterial = mat;
        ps.Emit(count);
        ps.Play();
        DOVirtual.DelayedCall(life + 0.2f, () => { Object.Destroy(mat); Object.Destroy(go); }).SetUpdate(true);
    }

    private static Texture2D MakeRing()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ring (runtime)" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
        }
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }

    private static Texture2D MakeDot()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Dot (runtime)" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }
}

/// <summary>Efekt świata przy awansie: napis „LEVEL UP”, złoty pierścień na ziemi, iskry w górę. Czas rzeczywisty.</summary>
public class LevelUpBurst : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Color gold = new Color(1f, 0.82f, 0.2f);

    private void OnEnable() => GameEvents.OnLevelUp += GameEvents_OnLevelUp;
    private void OnDisable() => GameEvents.OnLevelUp -= GameEvents_OnLevelUp;

    private void GameEvents_OnLevelUp(int level)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        var cam = viewCamera != null ? viewCamera : Camera.main;
        Vector3 ground = player.transform.position;
        Vector3 start = ground + Vector3.up * 2.4f;

        var tmp = UiFeelFx.SpawnText("LEVEL UP", start, 8f, gold, font, cam, out Transform t);
        t.localScale = Vector3.zero;
        UiFeelFx.Billboard(t, cam, 1.3f);
        DOTween.Sequence().SetTarget(t).SetLink(t.gameObject).SetUpdate(true)
            .Append(t.DOScale(1.3f, 0.2f).SetEase(Ease.OutQuad))
            .Append(t.DOScale(1f, 0.15f).SetEase(Ease.InOutQuad))
            .Join(t.DOMoveY(start.y + 1f, 1.2f).SetEase(Ease.OutCubic))
            .Insert(0.85f, DOVirtual.Float(1f, 0f, 0.35f, a => { if (tmp != null) tmp.alpha = a; }))
            .OnComplete(() => { if (t != null) Destroy(t.gameObject); });

        UiFeelFx.Ring(ground, new Color(gold.r, gold.g, gold.b, 0.9f), 3f, 0.9f);
        UiFeelFx.Sparks(ground + Vector3.up * 0.3f, gold, 20, 4f, 1f);
        if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(gold);
    }
}
