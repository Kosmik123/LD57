using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Pauza na Esc w trakcie runu: Wznów, Od nowa, Wyjdź. Zapamiętuje poprzedni timeScale (slow-mo fali).</summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField, Tooltip("Wyłączany na czas pauzy, bo czyta input także przy timeScale 0.")]
    private Behaviour playerController;

    private const float HitStopScale = 0.0001f;

    private bool paused;
    private float previousScale = 1f;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;
        if (paused) { Resume(); return; }
        // Nie w menu startowym, po końcu runu, na ekranie kart (timeScale 0) ani w hit stopie.
        if (gameManager == null || !gameManager.IsRunStarted || gameManager.IsRunOver)
            return;
        if (Time.timeScale <= 0f || Mathf.Approximately(Time.timeScale, HitStopScale))
            return;
        Pause();
    }

    private void OnDisable()
    {
        if (paused) Resume();
    }

    private void Pause()
    {
        paused = true;
        previousScale = Time.timeScale;
        Time.timeScale = 0f;
        if (playerController != null) playerController.enabled = false;
        if (root != null) root.SetActive(true);
        if (group != null)
        {
            group.alpha = 0f;
            group.DOKill();
            group.DOFade(1f, 0.15f).SetUpdate(true);
        }
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        if (group != null) group.DOKill();
        if (root != null) root.SetActive(false);
        if (playerController != null) playerController.enabled = true;
        // Gdyby w międzyczasie coś zakończyło run, nie odpauzowuj.
        if (gameManager == null || !gameManager.IsRunOver)
            Time.timeScale = previousScale > 0.01f ? previousScale : 1f;
    }

    private void Restart()
    {
        paused = false;
        if (gameManager != null) gameManager.RestartRun();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
