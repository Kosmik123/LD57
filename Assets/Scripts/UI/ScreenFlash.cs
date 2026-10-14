using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Czerwony błysk całego ekranu po obrażeniach gracza (czas rzeczywisty).</summary>
[RequireComponent(typeof(Image))]
public class ScreenFlash : MonoBehaviour
{
    public static ScreenFlash Instance { get; private set; }

    [SerializeField] private float peakAlpha = 0.35f;
    [SerializeField] private float duration = 0.25f;

    private Image image;
    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        image = GetComponent<Image>();
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private Color baseColor;
    private bool baseColorSaved;

    public void Flash()
    {
        if (!isActiveAndEnabled)
            return;
        if (routine != null)
            StopCoroutine(routine);
        RestoreBaseColor();
        routine = StartCoroutine(FlashRoutine());
    }

    /// <summary>Błysk w innym kolorze (np. złoty przy level upie); po błysku wraca kolor bazowy (czerwony).</summary>
    public void Flash(Color color)
    {
        if (!isActiveAndEnabled)
            return;
        if (routine != null)
            StopCoroutine(routine);
        RestoreBaseColor();
        var c = color; c.a = image.color.a;
        image.color = c;
        routine = StartCoroutine(FlashRoutine());
    }

    private void RestoreBaseColor()
    {
        if (!baseColorSaved) { baseColor = image.color; baseColorSaved = true; }
        var c = baseColor; c.a = 0f;
        image.color = c;
    }

    private IEnumerator FlashRoutine()
    {
        float half = duration * 0.5f;
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(0f, peakAlpha, t / half));
            yield return null;
        }
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(peakAlpha, 0f, t / half));
            yield return null;
        }
        SetAlpha(0f);
        RestoreBaseColor();
        routine = null;
    }

    private void SetAlpha(float a)
    {
        var c = image.color;
        c.a = a;
        image.color = c;
    }
}
