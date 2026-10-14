using UnityEngine;

/// <summary>Lekkie kolysanie dekoracji (wahadlo wokol pivotu obiektu). Placeholder pajakow wiszacych na suficie.</summary>
public class DecorSway : MonoBehaviour
{
    [SerializeField] private float amplitude = 6f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float phase;

    private Quaternion baseRotation;
    private bool initialized;

    public void Configure(float amplitudeDeg, float speed, float phase)
    {
        amplitude = amplitudeDeg; this.speed = speed; this.phase = phase;
    }

    private void Start()
    {
        baseRotation = transform.localRotation;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        float t = Time.time * speed + phase;
        transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t) * amplitude, 0f, Mathf.Cos(t * 0.8f) * amplitude * 0.7f);
    }
}
