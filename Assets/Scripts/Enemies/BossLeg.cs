using UnityEngine;

/// <summary>
/// Noga bossa (Leg_0..Leg_7 pod Visual/Legs). Ma WŁASNY Health (HP nogi), kinematyczny Rigidbody, tag Enemy i triggerowe
/// collidery na Upper/Lower: miecz (DamagingBlade: attachedRigidbody -> Health), skille i pociski (GetComponentInParent&lt;Health&gt;)
/// trafiają więc nogę, a nie bossa. Zdarzenia: OnDamaged(noga, faktyczne obrażenia) i OnBroken(noga) słucha SpiderMotherBoss.
/// BossLeg.Damage(int) to też punkt wejścia dla testów.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossLeg : MonoBehaviour
{
    [SerializeField, Tooltip("Numer nogi 0-7. -1 = z nazwy obiektu (Leg_3).")] private int index = -1;

    public event System.Action<BossLeg, int> OnDamaged;
    public event System.Action<BossLeg> OnBroken;

    public Health Health { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public int CurrentHealth => Health != null ? Mathf.Max(0, Health.CurrentHealth) : 0;
    public int MaxHealth => Health != null ? Health.MaxHealth : 0;
    public int Index
    {
        get
        {
            if (index < 0) index = IndexFromName(name);
            return index;
        }
    }

    private int lastHealth;
    private Collider[] colliders;

    public static int IndexFromName(string objectName)
    {
        int us = objectName.LastIndexOf('_');
        if (us >= 0 && int.TryParse(objectName.Substring(us + 1), out int parsed))
            return parsed;
        return 0;
    }

    private void Awake()
    {
        Health = GetComponent<Health>();
        lastHealth = Health.CurrentHealth;
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        Health.OnHealthChanged += Health_OnHealthChanged;
        Health.OnDied += Health_OnDied;
    }

    private void OnDisable()
    {
        Health.OnHealthChanged -= Health_OnHealthChanged;
        Health.OnDied -= Health_OnDied;
    }

    /// <summary>Obrażenia nogi (jak trafienie bronią). Nieaktywne przy nietykalności Health nogi.</summary>
    public void Damage(int amount)
    {
        if (!IsAlive || amount <= 0) return;
        Health.ChangeHealth(-amount);
    }

    private void Health_OnHealthChanged(int delta)
    {
        int now = Health.CurrentHealth;
        int actual = lastHealth - now;
        lastHealth = now;
        if (actual > 0 && IsAlive)
            OnDamaged?.Invoke(this, actual);
    }

    private void Health_OnDied()
    {
        if (!IsAlive) return;
        IsAlive = false;
        foreach (var c in colliders)
            if (c != null) c.enabled = false;
        OnBroken?.Invoke(this);
    }
}

/// <summary>
/// Hitbox ciała bossa (dziecko "Body Hurtbox": solidny collider, kinematyczny Rigidbody, tag Enemy, własny duży Health).
/// Każde trafienie jest cofane (leczenie o tyle samo) i przekazywane do bossa jako trafienie w ciało:
/// SpiderMotherBoss.RouteBodyHit przenosi je (x0,5) na najbliższą żywą nogę.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossBodyHitbox : MonoBehaviour
{
    private Health health;
    private SpiderMotherBoss boss;
    private bool restoring;

    private void Awake()
    {
        health = GetComponent<Health>();
        boss = GetComponentInParent<SpiderMotherBoss>();
    }

    private void OnEnable() => health.OnHealthChanged += Health_OnHealthChanged;
    private void OnDisable() => health.OnHealthChanged -= Health_OnHealthChanged;

    private void Health_OnHealthChanged(int delta)
    {
        if (delta >= 0 || restoring) return;
        restoring = true;
        health.ChangeHealth(-delta); // cofnij
        restoring = false;
        if (boss != null) boss.RouteBodyHit(-delta);
    }
}

/// <summary>Odłamana noga: leci łukiem z grawitacją, odbija się od podłogi, po czasie życia znika (zmniejszenie + Destroy).</summary>
public class DetachedLegPiece : MonoBehaviour
{
    private Vector3 velocity, spin;
    private float life, age, floorY;
    private Vector3 baseScale;

    public static DetachedLegPiece Launch(GameObject clone, Vector3 velocity, float lifeSeconds)
    {
        var piece = clone.AddComponent<DetachedLegPiece>();
        piece.velocity = velocity;
        piece.life = Mathf.Max(0.3f, lifeSeconds);
        piece.spin = Random.onUnitSphere * Random.Range(200f, 420f);
        piece.baseScale = clone.transform.localScale;
        piece.floorY = clone.transform.position.y - 1.5f;
        if (Physics.Raycast(clone.transform.position + Vector3.up * 2f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            piece.floorY = hit.point.y + 0.1f;
        return piece;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Vector3.down * 14.7f * dt; // grawitacja x1,5
        var pos = transform.position + velocity * dt;
        if (pos.y < floorY && velocity.y < 0f)
        {
            pos.y = floorY;
            velocity.y = -velocity.y * 0.3f;
            velocity.x *= 0.6f; velocity.z *= 0.6f;
            spin *= 0.5f;
        }
        transform.position = pos;
        transform.Rotate(spin * dt, Space.World);
        float fade = life - 0.3f;
        if (age > fade)
            transform.localScale = baseScale * Mathf.Clamp01(1f - (age - fade) / 0.3f);
        if (age >= life)
            Destroy(gameObject);
    }
}
