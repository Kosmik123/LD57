using UnityEngine;

/// <summary>
/// Zastępczy wygląd wroga: jeśli w hierarchii nie ma żadnego renderera (np. brakujący model slime'a
/// „SlimePADefault”, który nie jest w repo), tworzy prostą kulę. Gdy prawdziwy model jest, nic nie robi.
/// </summary>
public class PlaceholderVisual : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.35f, 0.8f, 0.3f);
    [SerializeField] private Vector3 scale = new Vector3(1.0f, 0.7f, 1.0f);
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.35f, 0f);

    private void Awake()
    {
        var existing = GetComponentsInChildren<Renderer>(true);
        foreach (var r in existing)
            if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer))
                return;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Placeholder Visual";
        Destroy(visual.GetComponent<Collider>());
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = offset;
        visual.transform.localScale = scale;
        var renderer = visual.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
        }
    }
}
