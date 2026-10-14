using UnityEngine;

/// <summary>Serce leczące gracza po dotknięciu. Lekko się buja i obraca (bez DOTween).</summary>
public class HeartPickup : MonoBehaviour
{
    [SerializeField]
    private int heal = 15;
    [SerializeField]
    private float bobAmplitude = 0.15f;
    [SerializeField]
    private float bobSpeed = 3f;
    [SerializeField]
    private float rotationSpeed = 90f;

    [SerializeField, Tooltip("Puls skali 1 -> 1+tyle, 1 Hz.")]
    private float pulseAmount = 0.15f;

    private Vector3 startPosition;
    private Vector3 baseScale;
    private float sparkleTimer;

    private void Start()
    {
        startPosition = transform.position;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        transform.localScale = baseScale * (1f + pulseAmount * (0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f)));
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        WorldVfx.HeartSparkle(transform.position, Time.deltaTime, ref sparkleTimer);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ColliderTags.IsPlayer(other))
            return;

        var health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        health.ChangeHealth(heal);
        WorldVfx.HeartPickupBurst(transform.position, health.transform.position);
        Sfx.Play(SfxKind.Heart, transform.position);
        Destroy(gameObject);
    }
}
