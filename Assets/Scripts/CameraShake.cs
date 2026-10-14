using UnityEngine;

/// <summary>Wstrząs kamery. Wymaga CameraController (ShakeOffset jest dodawany na końcu ApplyPosition).</summary>
[RequireComponent(typeof(CameraController))]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private CameraController cameraController;
    private float duration;
    private float magnitude;
    private float elapsed;

    private void Awake()
    {
        Instance = this;
        cameraController = GetComponent<CameraController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Losowy offset malejący do zera. Słabszy wstrząs nie przerywa silniejszego, który jeszcze trwa.</summary>
    public void Shake(float duration, float magnitude)
    {
        float remaining = this.duration - elapsed;
        if (remaining > 0f && this.magnitude * remaining / Mathf.Max(this.duration, 0.0001f) > magnitude)
            return;
        this.duration = duration;
        this.magnitude = magnitude;
        elapsed = 0f;
    }

    /// <summary>Wstrząs skalowany obrażeniami: 0,05 + 0,01 × dmg, maks. 0,35.</summary>
    public void ShakeScaled(int damage)
    {
        Shake(0.12f, Mathf.Min(0.35f, 0.05f + 0.01f * damage));
    }

    private void Update()
    {
        if (elapsed >= duration)
        {
            cameraController.ShakeOffset = Vector3.zero;
            return;
        }
        elapsed += Time.unscaledDeltaTime;
        float falloff = 1f - Mathf.Clamp01(elapsed / duration);
        cameraController.ShakeOffset = Random.insideUnitSphere * (magnitude * falloff);
    }

    private void OnDisable()
    {
        if (cameraController != null)
            cameraController.ShakeOffset = Vector3.zero;
    }
}
