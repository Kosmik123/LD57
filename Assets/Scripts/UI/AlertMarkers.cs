using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Żółty „!” nad wrogami objętymi alarmem (EnemyAlert.OnAlert): pop-in 0 → 1,2 → 1 przez 0,6 s, billboard.
/// Wrogowie w promieniu `radius` od punktu alarmu (ten sam co alertRadius w EnemyAlert, domyślnie 8).
/// </summary>
public class AlertMarkers : MonoBehaviour
{
    private class Marker
    {
        public TextMeshPro text;
        public Transform target;
        public Sequence seq;
    }

    [SerializeField] private Camera viewCamera;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float radius = 8f;
    [SerializeField] private float height = 2.3f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float perEnemyCooldown = 1f;

    private readonly List<Marker> live = new List<Marker>();
    private readonly Stack<Marker> pool = new Stack<Marker>();
    private readonly Dictionary<EnemyAlert, float> lastShown = new Dictionary<EnemyAlert, float>();

    private void OnEnable() => EnemyAlert.OnAlert += EnemyAlert_OnAlert;

    private void OnDisable()
    {
        EnemyAlert.OnAlert -= EnemyAlert_OnAlert;
        foreach (var m in live) { m.seq?.Kill(); m.text.gameObject.SetActive(false); pool.Push(m); }
        live.Clear();
    }

    private void EnemyAlert_OnAlert(Vector3 at)
    {
        foreach (var e in Object.FindObjectsByType<EnemyAlert>(FindObjectsSortMode.None))
        {
            if ((e.transform.position - at).sqrMagnitude > radius * radius) continue;
            if (lastShown.TryGetValue(e, out float t) && Time.time - t < perEnemyCooldown) continue;
            lastShown[e] = Time.time;
            Show(e.transform);
        }
    }

    private void Show(Transform target)
    {
        var m = pool.Count > 0 ? pool.Pop() : Create();
        m.target = target;
        m.text.gameObject.SetActive(true);
        Place(m);
        m.seq?.Kill();
        var tr = m.text.transform;
        tr.localScale = Vector3.zero;
        m.seq = DOTween.Sequence().SetUpdate(true)
            .Append(tr.DOScale(1.2f, duration * 0.35f).SetEase(Ease.OutQuad))
            .Append(tr.DOScale(1f, duration * 0.15f))
            .AppendInterval(duration * 0.5f)
            .OnComplete(() => Release(m));
        live.Add(m);
    }

    private void Release(Marker m)
    {
        live.Remove(m);
        m.target = null;
        m.text.gameObject.SetActive(false);
        pool.Push(m);
    }

    private void LateUpdate()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var m = live[i];
            if (m.target == null) { m.seq?.Kill(); Release(m); continue; }
            Place(m);
        }
    }

    private void Place(Marker m)
    {
        var cam = viewCamera != null ? viewCamera : Camera.main;
        m.text.transform.position = m.target.position + Vector3.up * height;
        if (cam != null) m.text.transform.rotation = cam.transform.rotation;
    }

    private Marker Create()
    {
        var go = new GameObject("Alert !");
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = "!";
        tmp.fontSize = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.1f);
        return new Marker { text = tmp };
    }
}
