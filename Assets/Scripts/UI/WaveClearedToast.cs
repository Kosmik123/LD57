using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Koniec fali tylko w świecie (decyzja designu: bez HUD): mały napis „Czysto” nad graczem i 8 białych iskier.</summary>
public class WaveClearedToast : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private string message = "Czysto";

    private void OnEnable() => GameEvents.OnWaveCleared += GameEvents_OnWaveCleared;
    private void OnDisable() => GameEvents.OnWaveCleared -= GameEvents_OnWaveCleared;

    private void GameEvents_OnWaveCleared(WaveZone zone)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        var cam = viewCamera != null ? viewCamera : Camera.main;
        Vector3 start = player.transform.position + Vector3.up * 2.2f;

        var tmp = UiFeelFx.SpawnText(message, start, 3.5f, new Color(1f, 1f, 1f, 0.95f), font, cam, out Transform t);
        UiFeelFx.Billboard(t, cam, 0.85f);
        DOTween.Sequence().SetTarget(t).SetLink(t.gameObject).SetUpdate(true)
            .Join(t.DOMoveY(start.y + 0.7f, 0.8f).SetEase(Ease.OutCubic))
            .Insert(0.4f, DOVirtual.Float(0.95f, 0f, 0.4f, a => { if (tmp != null) tmp.alpha = a; }))
            .OnComplete(() => { if (t != null) Destroy(t.gameObject); });

        UiFeelFx.Sparks(player.transform.position + Vector3.up * 0.6f, Color.white, 8, 2.5f, 0.7f);
    }
}
