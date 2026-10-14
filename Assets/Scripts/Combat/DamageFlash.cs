using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Błysk na biało po otrzymaniu obrażeń: na czas błysku wszystkie renderery dostają wspólny biały materiał Unlit
/// (niezależnie od światła), a w LateUpdate biel jest wymuszana ponownie, bo animatory (VisualRig/RendererTint)
/// ustawiają własne MaterialPropertyBlock co klatkę. Oryginalne materiały wracają po `duration`.
/// </summary>
[RequireComponent(typeof(Health))]
[DefaultExecutionOrder(1000)]
public class DamageFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material flashMaterial;

    [SerializeField] private float duration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    private Health health;
    private MaterialPropertyBlock block;
    private readonly List<Renderer> flashing = new List<Renderer>();
    private readonly List<Material[]> originals = new List<Material[]>();
    private float flashUntil = -1f;

    private static Material FlashMaterial
    {
        get
        {
            if (flashMaterial != null) return flashMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            flashMaterial = new Material(shader) { name = "DamageFlash (runtime)", color = Color.white };
            if (flashMaterial.HasProperty(BaseColorId)) flashMaterial.SetColor(BaseColorId, Color.white);
            return flashMaterial;
        }
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable() => health.OnHealthChanged += Health_OnHealthChanged;

    private void OnDisable()
    {
        health.OnHealthChanged -= Health_OnHealthChanged;
        Restore();
    }

    private void Health_OnHealthChanged(int change)
    {
        if (change >= 0 || !isActiveAndEnabled)
            return;
        if (flashing.Count == 0)
            Begin();
        flashUntil = Time.unscaledTime + duration;
    }

    private void Begin()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;
            originals.Add(mats);
            flashing.Add(r);
            var white = new Material[mats.Length];
            for (int i = 0; i < white.Length; i++) white[i] = FlashMaterial;
            r.sharedMaterials = white;
        }
        AssertWhite();
    }

    private void LateUpdate()
    {
        if (flashing.Count == 0) return;
        if (Time.unscaledTime >= flashUntil) { Restore(); return; }
        AssertWhite();
    }

    private void AssertWhite()
    {
        block.Clear();
        block.SetColor(BaseColorId, flashColor);
        block.SetColor(ColorId, flashColor);
        foreach (var r in flashing)
            if (r != null) r.SetPropertyBlock(block);
    }

    private void Restore()
    {
        for (int i = 0; i < flashing.Count; i++)
        {
            var r = flashing[i];
            if (r == null) continue;
            r.sharedMaterials = originals[i];
            r.SetPropertyBlock(null); // animatory (RendererTint) nałożą swój tint w następnej klatce
        }
        flashing.Clear();
        originals.Clear();
        flashUntil = -1f;
    }
}
