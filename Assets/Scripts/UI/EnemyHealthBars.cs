using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Małe paski HP nad wrogami (world-space, billboard). Pasek pojawia się po pierwszym trafieniu,
/// znika po `hideAfter` s bez obrażeń i po śmierci. Boss jest pomijany (ma własny pasek). Paski są pulowane.
/// </summary>
public class EnemyHealthBars : MonoBehaviour
{
    private class Bar
    {
        public GameObject go;
        public Image fill;
        public EnemyHealth owner;
        public float hideAt;
    }

    [SerializeField] private Camera viewCamera;
    [SerializeField] private float height = 2f;
    [SerializeField] private float hideAfter = 2f;
    [SerializeField, Tooltip("Rozmiar paska w jednostkach świata.")] private Vector2 size = new Vector2(1f, 0.12f);

    private readonly Dictionary<EnemyHealth, Bar> active = new Dictionary<EnemyHealth, Bar>();
    private readonly Stack<Bar> pool = new Stack<Bar>();
    private readonly List<EnemyHealth> toRemove = new List<EnemyHealth>();

    private void OnEnable()
    {
        EnemyHealth.OnEnemyDamaged += EnemyHealth_OnEnemyDamaged;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDamaged -= EnemyHealth_OnEnemyDamaged;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
    }

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth e, int amount)
    {
        if (e == null || e.IsBoss || e.Health.IsDead) return;
        if (!active.TryGetValue(e, out var bar))
        {
            bar = pool.Count > 0 ? pool.Pop() : CreateBar();
            bar.owner = e;
            bar.go.SetActive(true);
            active[e] = bar;
        }
        bar.fill.fillAmount = Mathf.Clamp01((float)e.Health.CurrentHealth / Mathf.Max(1, e.Health.MaxHealth));
        bar.hideAt = Time.time + hideAfter;
        Place(bar);
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth e)
    {
        if (e != null && active.ContainsKey(e)) Release(e);
    }

    private void LateUpdate()
    {
        toRemove.Clear();
        foreach (var kv in active)
        {
            if (kv.Key == null || Time.time >= kv.Value.hideAt) toRemove.Add(kv.Key);
            else Place(kv.Value);
        }
        foreach (var e in toRemove) Release(e);
    }

    private void Place(Bar bar)
    {
        var cam = viewCamera != null ? viewCamera : Camera.main;
        bar.go.transform.position = bar.owner.transform.position + Vector3.up * height;
        if (cam != null) bar.go.transform.rotation = cam.transform.rotation;
    }

    private void Release(EnemyHealth e)
    {
        if (!active.TryGetValue(e, out var bar)) return;
        active.Remove(e);
        bar.owner = null;
        bar.go.SetActive(false);
        pool.Push(bar);
    }

    private Bar CreateBar()
    {
        var go = new GameObject("Enemy Bar", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(100f, 12f);
        go.transform.localScale = new Vector3(size.x / 100f, size.y / 12f, 1f);

        var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        bg.raycastTarget = false;
        bg.transform.SetParent(go.transform, false);
        Stretch(bg.rectTransform, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fill.color = new Color(0.85f, 0.15f, 0.15f);
        fill.raycastTarget = false;
        fill.transform.SetParent(go.transform, false);
        Stretch(fill.rectTransform, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;

        return new Bar { go = go, fill = fill };
    }

    private static void Stretch(RectTransform r, float margin)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin); r.offsetMax = new Vector2(-margin, -margin);
    }
}
