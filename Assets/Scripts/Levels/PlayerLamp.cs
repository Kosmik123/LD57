using UnityEngine;

/// <summary>Lampa gracza (Point Light w dziecku). Natezenie ustawia LevelAmbience; 0 = wylaczona.</summary>
public class PlayerLamp : MonoBehaviour
{
    [SerializeField] private Light lampLight;

    private void Awake()
    {
        if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);
    }

    public void SetIntensity(float intensity)
    {
        if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);
        if (lampLight == null) return;
        lampLight.intensity = intensity;
        lampLight.enabled = intensity > 0f;
    }
}
