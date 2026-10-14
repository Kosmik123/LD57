using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

public class WorldsSpaceDamageIndicators : MonoBehaviour
{
    [SerializeField]
    private Transform damageTextPrototype;

    private ObjectPool<Transform> displaysPool;

    private void Awake()
    {
        displaysPool = new ObjectPool<Transform>(() => Instantiate(damageTextPrototype, transform));
    }

    private void OnEnable()
    {
        EnemyHealth.OnEnemyDamaged += EnemyHealth_OnEnemyDamaged;
    }

    [SerializeField, Tooltip("Skala liczby dla 1 obrażenia; rośnie z pierwiastkiem obrażeń do maks. 2,2x.")]
    private float baseScale = 0.45f;
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private float rise = 0.8f;
    [SerializeField] private float spread = 0.5f;
    [SerializeField, Tooltip("Od tylu obrażeń liczba ma styl mocnego ciosu.")]
    private int heavyHitThreshold = 5;

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth enemy, int damage)
    {
        var display = displaysPool.Get();
        var text = display.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            bool kill = enemy.Health != null && enemy.Health.IsDead;
            text.SetText(kill ? damage + "!" : damage.ToString());
            text.alpha = 1f;
            // Mocniejszy cios = większa liczba i bardziej żółta.
            float strength = Mathf.Clamp01((damage - 1f) / 14f);
            text.color = Color.Lerp(new Color(1f, 0.35f, 0.25f), new Color(1f, 0.9f, 0.3f), strength);
            if (damage >= heavyHitThreshold) text.color = new Color(1f, 0.65f, 0.1f); // mocny cios
            if (kill) text.color = new Color(0.95f, 0.1f, 0.1f); // zabójstwo
        }
        display.gameObject.SetActive(true);
        display.DOKill();

        // Rozrzut w bok, żeby kilka trafień naraz nie nakładało się na siebie.
        var position = enemy.transform.position + Vector3.up * 1.6f + new Vector3(Random.Range(-spread, spread), Random.Range(0f, 0.3f), Random.Range(-spread, spread));
        display.position = position;
        var cam = Camera.main;
        if (cam != null)
            display.rotation = cam.transform.rotation; // liczba zawsze przodem do kamery
        float scale = baseScale * Mathf.Min(2.2f, 0.8f + 0.25f * Mathf.Sqrt(damage));
        if (damage >= heavyHitThreshold) scale *= 1.4f;
        display.localScale = Vector3.one * scale * 1.3f;
        display.DOScale(scale, 0.12f).SetEase(Ease.OutBack);

        display.DOMoveY(position.y + rise, lifetime).SetEase(Ease.OutCubic);
        if (damage >= heavyHitThreshold && text != null)
        {
            // Wstrząs na dziecku z tekstem; gdy tekst jest na samym obiekcie, trzęsiemy rotacją (pozycję zajmuje DOMoveY).
            var tt = text.transform;
            if (tt != display) { tt.DOKill(); tt.localPosition = Vector3.zero; tt.DOShakePosition(0.15f, 0.1f); }
            else display.DOShakeRotation(0.15f, 10f);
        }
        if (text != null)
            DOTween.To(() => text.alpha, a => text.alpha = a, 0f, lifetime * 0.5f).SetDelay(lifetime * 0.5f);
        DOVirtual.DelayedCall(lifetime, () =>
        {
            display.gameObject.SetActive(false);
            displaysPool.Release(display);
        });
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDamaged -= EnemyHealth_OnEnemyDamaged;
    }
}
