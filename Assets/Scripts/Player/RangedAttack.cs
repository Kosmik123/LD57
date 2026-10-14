using UnityEngine;

/// <summary>
/// Atak podstawowy maga: na PlayerController.OnAttack (Fire1) wystrzeliwuje pocisk w transform.forward.
/// Komponent jest wyłączony u Wojownika; PlayerClassApplier włącza go i ustawia obrażenia (Configure).
/// </summary>
public class RangedAttack : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField, Tooltip("Dziecko roota z modelem; pocisk startuje nad jego pozycją.")]
    private Transform body;
    [SerializeField] private int damage = 2;
    [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private float speed = 22f;
    [SerializeField] private float range = 26f;
    [SerializeField] private float height = 0.9f;
    [SerializeField] private Color boltColor = new Color(0.45f, 0.65f, 1f);

    private float nextShot;

    public void Configure(int newDamage, float newCooldown)
    {
        damage = newDamage;
        cooldown = newCooldown;
    }

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (body == null) body = transform.Find("Body");
    }

    private void OnEnable()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (player != null) player.OnAttack += Player_OnAttack;
    }

    private void OnDisable()
    {
        if (player != null) player.OnAttack -= Player_OnAttack;
    }

    private void Player_OnAttack()
    {
        if (Time.time < nextShot) return;
        nextShot = Time.time + cooldown;

        var dir = transform.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        var basePos = body != null ? body.position : transform.position;
        var origin = basePos + Vector3.up * height + dir * 0.4f;

        // Minimum zasięgu/prędkości wymuszone w kodzie (prefab ma starsze wartości 18 m/s, 12 m).
        PlayerBoltPool.Get().Launch(transform, origin, dir, Mathf.Max(speed, 22f), Mathf.Max(range, 26f), damage, boltColor, 0.3f);
        Sfx.Play(SfxKind.Shot);
        MageStylizedFx.CastFlash(transform, body, boltColor); // błysk w dłoni + 3 iskry
    }
}
