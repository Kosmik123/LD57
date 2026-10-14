using DG.Tweening;
using UnityEngine;

/// <summary>
/// Krata zejścia. Zamknięta do OnLevelCleared; potem trigger z tagiem Player wysyła gracza na kolejny poziom.
/// Komponent musi leżeć na tym samym obiekcie co collider `trigger` (OnTriggerEnter).
/// </summary>
public class ExitGate : MonoBehaviour
{
    public event System.Action OnOpened;

    [SerializeField, Tooltip("Wizualna krata, wyłączana przy otwarciu.")]
    private GameObject closedVisual;
    [SerializeField, Tooltip("Collider blokujący przejście, wyłączany przy otwarciu.")]
    private Collider blocking;
    [SerializeField, Tooltip("Trigger zejścia (isTrigger), włączany przy otwarciu.")]
    private Collider trigger;
    [SerializeField, Tooltip("Światło włączane przy otwarciu kraty.")]
    private Light exitLight;
    [SerializeField] private float openDropDistance = 4f;
    [SerializeField] private float openDuration = 0.6f;
    [SerializeField, Tooltip("Drżenie kraty przed zjazdem.")] private float shakeDuration = 0.3f;

    public bool IsOpen { get; private set; }
    private bool used;

    private void Awake()
    {
        if (trigger != null)
            trigger.enabled = false;
    }

    private void OnEnable()
    {
        GameEvents.OnLevelCleared += GameEvents_OnLevelCleared;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelCleared -= GameEvents_OnLevelCleared;
    }

    private void GameEvents_OnLevelCleared(int level)
    {
        // wszystkie poziomy stoją w scenie naraz: krata reaguje tylko na wyczyszczenie własnego poziomu
        var tracker = GetComponentInParent<LevelEnemyTracker>();
        if (tracker != null && tracker.LevelNumber != level)
            return;
        Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;
        IsOpen = true;
        if (blocking != null) blocking.enabled = false;
        if (exitLight != null) exitLight.enabled = true;
        if (closedVisual != null)
        {
            if (isActiveAndEnabled) StartCoroutine(OpenRoutine());
            else closedVisual.SetActive(false);
        }
        if (trigger != null) trigger.enabled = true;
        OnOpened?.Invoke();
        Sfx.Play(SfxKind.GateOpen, transform.position, 1f);
    }

    private System.Collections.IEnumerator OpenRoutine()
    {
        var t = closedVisual.transform;
        Vector3 origin = t.position;
        var bounds = new Bounds(origin, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in closedVisual.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; } else bounds.Encapsulate(r.bounds);
        }
        var top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        var ground = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        WorldVfx.GateDust(top, bounds.size.x);
        if (exitLight != null)
        {
            float baseIntensity = exitLight.intensity;
            exitLight.DOKill();
            DOTween.To(() => exitLight.intensity, v => exitLight.intensity = v, baseIntensity * 3f, 0.15f).SetTarget(exitLight).SetUpdate(true)
                .OnComplete(() => DOTween.To(() => exitLight.intensity, v => exitLight.intensity = v, baseIntensity, 0.15f).SetTarget(exitLight).SetUpdate(true));
        }
        t.DOShakePosition(shakeDuration, 0.08f, 25, 90f, false, true, ShakeRandomnessMode.Full).SetUpdate(true);
        yield return new WaitForSecondsRealtime(shakeDuration);
        t.DOKill();
        t.position = origin;
        Vector3 start = origin;
        Vector3 end = start + Vector3.down * openDropDistance;
        for (float e = 0; e < openDuration; e += Time.deltaTime)
        {
            t.position = Vector3.Lerp(start, end, e / openDuration);
            yield return null;
        }
        t.position = end;
        WorldVfx.GatePuff(ground, bounds.size.x);
        closedVisual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOpen || used || !ColliderTags.IsPlayer(other))
            return;
        if (LevelManager.Instance == null)
            return;
        used = true;
        LevelManager.Instance.GoToNextLevel();
    }
}
