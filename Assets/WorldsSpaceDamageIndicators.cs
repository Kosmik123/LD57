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

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth enemy, int damage)
    {
        var display = displaysPool.Get();
        display.GetComponentInChildren<TMP_Text>()?.SetText($"{-damage}");
        display.gameObject.SetActive(true);

        var position = enemy.transform.position + Vector3.up * 2f;
        display.position = position;
        var yStart = position.y;
        var yEnd = position.y + 1f;

        display.DOMoveY(yEnd, 2f).OnComplete(() =>
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
