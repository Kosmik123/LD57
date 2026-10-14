using UnityEngine;

/// <summary>
/// Sieć bossa: pocisk z białych nitek (obracający się pęk 3 cienkich kryształów + smuga). Leci prosto, w podkrokach sprawdza
/// OverlapSphere. Trafienie gracza: obrażenia + PlayerController.Root(rootSeconds) + biały pierścień brył na graczu.
/// Dash (Health.IsInvulnerable) przelatuje przez sieć bez efektu. Ściana albo koniec zasięgu: rozpad na nitki.
/// Prefab Web.prefab: korzeń z tym komponentem i dzieckiem "Spinner" (3 nitki) bez colliderów; smugę dodaje Awake.
/// </summary>
public class WebProjectile : MonoBehaviour
{
    private const float SubStep = 0.3f;
    private static readonly Collider[] buffer = new Collider[24];

    [SerializeField, Tooltip("Obracany pęk nitek (dziecko). Null = obraca korzeń.")] private Transform spinner;
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private float spinDegreesPerSecond = 540f;
    [SerializeField] private Color webColor = new Color(0.95f, 0.97f, 1f);

    private Vector3 direction;
    private float speed, remaining;
    private int damage;
    private float rootSeconds;
    private Transform owner;
    private bool flying;
    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.widthMultiplier = 0.25f;
            trail.minVertexDistance = 0.1f;
            trail.sharedMaterial = SwordTrail.CreateTransparentMaterial("Web Trail (runtime)", new Color(1f, 1f, 1f, 0.8f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(webColor, 0f), new GradientColorKey(webColor, 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }
    }

    /// <param name="owner">Boss: jego collidery są ignorowane.</param>
    public void Launch(Transform owner, Vector3 origin, Vector3 dir, float speed, float range, int damage, float rootSeconds)
    {
        this.owner = owner;
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        this.speed = speed;
        remaining = range;
        this.damage = damage;
        this.rootSeconds = rootSeconds;
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction);
        gameObject.SetActive(true);
        if (trail != null) trail.Clear();
        flying = true;
    }

    private void Update()
    {
        if (!flying) return;
        float dt = Time.deltaTime;
        var spin = spinner != null ? spinner : transform;
        spin.Rotate(0f, 0f, spinDegreesPerSecond * dt, Space.Self);

        float move = Mathf.Min(speed * dt, remaining);
        int steps = Mathf.Max(1, Mathf.CeilToInt(move / SubStep));
        float step = move / steps;
        for (int i = 0; i < steps; i++)
        {
            transform.position += direction * step;
            if (Probe())
                return;
        }
        remaining -= move;
        if (remaining <= 0.0001f)
            Break();
    }

    /// <summary>True, gdy pocisk się skończył (trafienie gracza albo ściana).</summary>
    private bool Probe()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, buffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null) continue;
            if (owner != null && col.transform.IsChildOf(owner)) continue;
            if (ColliderTags.IsPlayer(col))
            {
                if (HitPlayer(col)) return true;
                continue; // dash: sieć przelatuje
            }
            if (col.isTrigger || ColliderTags.IsEnemy(col)) continue;
            Break(); // ściana
            return true;
        }
        return false;
    }

    private bool HitPlayer(Collider col)
    {
        var body = col.attachedRigidbody;
        var health = body != null ? body.GetComponent<Health>() : null;
        if (health == null) health = col.GetComponentInParent<Health>();
        if (health == null || health.IsDead || health.IsInvulnerable)
            return false;

        health.ChangeHealth(-damage);
        var controller = col.GetComponentInParent<PlayerController>();
        if (controller != null)
        {
            controller.Root(rootSeconds);
            // Biały pierścień brył na graczu na czas unieruchomienia.
            VoxelOrbit.Attach(controller.transform, new Color(2f, 2f, 2.2f), 10, 0.9f, 0.9f, rootSeconds, 0.14f, 0.8f);
        }
        var pos = transform.position;
        VoxelFx.Burst(pos, new Color(2f, 2f, 2.2f), 14, 0.1f, 4f, 0.45f);
        VoxelFx.Flash(pos, new Color(1.8f, 1.9f, 2.2f), 0.7f);
        flying = false;
        Destroy(gameObject);
        return true;
    }

    private void Break()
    {
        if (!flying) return;
        flying = false;
        VoxelFx.Burst(transform.position, new Color(2f, 2f, 2.2f), 8, 0.09f, 3f, 0.4f, 1f);
        Destroy(gameObject);
    }
}
