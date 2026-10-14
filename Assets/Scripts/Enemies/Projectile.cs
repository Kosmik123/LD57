using UnityEngine;

/// <summary>Pocisk strzelca: leci prosto, rani gracza przy trafieniu, znika na ścianie lub po czasie życia.</summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private int damage;
    private float lifeLeft;
    private bool inFlight;
    private TrailRenderer trail;
    private MageShardTrail cubeTrail;
    private const float LifetimeFactor = 1.8f; // pociski znikały za szybko: zasięg x1,8 przy tej samej prędkości
    private static readonly Color CubeYellow = new Color(1f, 0.85f, 0.15f);

    private void Awake()
    {
        trail = WorldVfx.AddProjectileTrail(gameObject);
        cubeTrail = MageStylizedFx.AddTrail(gameObject, CubeYellow, 0.1f, 0.45f, false);
    }

    private void Reset()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        GetComponent<SphereCollider>().isTrigger = true;
    }

    public void Launch(Vector3 origin, Vector3 direction, float speed, int damage, float lifetime)
    {
        transform.position = origin;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(this.direction);
        this.speed = speed;
        this.damage = damage;
        lifeLeft = lifetime * LifetimeFactor;
        inFlight = true;
        gameObject.SetActive(true);
        if (trail != null) trail.Clear();
        if (cubeTrail != null) cubeTrail.Restart();
    }

    private void Update()
    {
        if (!inFlight)
            return;
        transform.position += direction * (speed * Time.deltaTime);
        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f)
        {
            MageStylizedFx.Burst(transform.position, CubeYellow, 4, 0.12f, 2.5f, 0.45f); // rozsypuje się na końcu zasięgu
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!inFlight)
            return;

        if (ColliderTags.IsPlayer(other))
        {
            var body = other.attachedRigidbody;
            var health = body != null ? body.GetComponent<Health>() : null;
            if (health == null)
                health = other.GetComponentInParent<Health>();
            if (health != null)
                health.ChangeHealth(-damage);
            HitSplash.Burst(transform.position, new Color(0.9f, 0.1f, 0.1f), 10);
            WorldVfx.ProjectileHit(transform.position, WorldVfx.ProjectileYellow);
            ReturnToPool();
            return;
        }

        if (other.isTrigger || ColliderTags.IsEnemy(other))
            return;
        WorldVfx.ProjectileHit(transform.position, WorldVfx.ProjectileYellow);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (!inFlight)
            return;
        inFlight = false;
        if (ProjectilePool.Instance != null)
            ProjectilePool.Instance.Release(this);
        else
            Destroy(gameObject);
    }
}
