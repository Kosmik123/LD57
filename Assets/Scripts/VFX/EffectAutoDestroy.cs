using UnityEngine;

/// <summary>Niszczy obiekt po zadanym czasie (efekty VFX).</summary>
public class EffectAutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;

    private void Start() => Destroy(gameObject, lifetime);
}
