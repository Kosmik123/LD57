using UnityEngine;
using UnityEngine.Pool;

/// <summary>Pula pocisków strzelca. Jeden komponent w scenie (obiekt "Projectile Pool").</summary>
public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool Instance { get; private set; }

    [SerializeField] private Projectile prefab;

    private ObjectPool<Projectile> pool;

    private void Awake()
    {
        Instance = this;
        pool = new ObjectPool<Projectile>(
            () => Instantiate(prefab, transform),
            p => p.gameObject.SetActive(true),
            p => p.gameObject.SetActive(false),
            p => { if (p != null) Destroy(p.gameObject); });
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Pobiera pocisk z puli (aktywny, do wywołania Launch). Null gdy brak puli w scenie.</summary>
    public static Projectile Get()
    {
        if (Instance == null || Instance.prefab == null)
        {
            Debug.LogWarning("Brak ProjectilePool w scenie (obiekt 'Projectile Pool').");
            return null;
        }
        return Instance.pool.Get();
    }

    public void Release(Projectile projectile)
    {
        if (projectile != null)
            pool.Release(projectile);
    }
}
