using UnityEngine;

/// <summary>Migotanie światła punktowego: losowe wahanie intensywności (szum Perlina) wokół wartości bazowej.</summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f), Tooltip("Względna amplituda wahania (0,15 = ±15 %)")] private float amplitude = 0.15f;
    [SerializeField, Tooltip("Szybkość szumu")] private float speed = 3f;

    private Light target;
    private float baseIntensity;
    private float seed;

    private void Awake()
    {
        target = GetComponent<Light>();
        baseIntensity = target.intensity;
        seed = Random.value * 100f;
    }

    /// <summary>Zmienia bazową intensywność (migotanie działa wokół nowej wartości).</summary>
    public void SetBaseIntensity(float intensity) => baseIntensity = intensity;

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        target.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * amplitude);
    }

    private void OnDisable()
    {
        if (target != null) target.intensity = baseIntensity;
    }
}
