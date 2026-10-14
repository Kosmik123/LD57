using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Menu startowe: tytuł, „Graj”, „Sterowanie”, „Wyjdź”. Gra czeka w pauzie (GameManager.Awake ustawia timeScale 0),
/// po „Graj” menu gaśnie (czas rzeczywisty) i startuje run. Pomijane w batchu, przy SkipMenu i po restarcie runu.
/// </summary>
public class StartMenu : MonoBehaviour
{
    /// <summary>Ustawiane przez testy (lub kod), żeby menu się nie pokazało.</summary>
    public static bool SkipMenu;

    private static bool restartPending;
    public static void MarkRestart() => restartPending = true;
    public static bool ConsumeRestart()
    {
        bool value = restartPending;
        restartPending = false;
        return value;
    }

    [SerializeField] private GameManager gameManager;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text tagline;
    [SerializeField] private Button playButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [Header("Wybór klasy")]
    [SerializeField] private Button warriorButton;
    [SerializeField] private Button mageButton;
    [SerializeField, Tooltip("Opis wybranej klasy pod przyciskami.")] private TMP_Text classDescription;
    [SerializeField] private PlayerClassApplier classApplier;
    [SerializeField, Tooltip("Panel z tabelą sterowania, domyślnie wyłączony.")]
    private GameObject controlsPanel;
    [SerializeField, Tooltip("Obiekty HUD ukryte do startu runu (Skill HUD, Health Bar).")]
    private GameObject[] hudObjects;
    [SerializeField, Tooltip("Wyłączany do startu, bo czyta input także przy timeScale 0.")]
    private Behaviour playerController;
    [SerializeField] private float fadeDuration = 0.4f;

    private const string WarriorName = "Wojownik";
    private const string MageName = "Mag";
    private static readonly Color ClassSelected = new Color(0.45f, 0.35f, 0.7f, 0.98f);
    private static readonly Color ClassIdle = new Color(0.12f, 0.12f, 0.17f, 0.95f);

    private bool leaving;
    private Tween pulse;

    private void Awake()
    {
        if (title != null) title.text = GameTitle.Name;
        if (tagline != null) tagline.text = GameTitle.Tagline;
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (controlsButton != null) controlsButton.onClick.AddListener(ToggleControls);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (classApplier == null)
            classApplier = FindFirstObjectByType<PlayerClassApplier>();
        if (warriorButton != null) warriorButton.onClick.AddListener(() => SelectClass(WarriorName));
        if (mageButton != null) mageButton.onClick.AddListener(() => SelectClass(MageName));
    }

    /// <summary>Wybiera klasę (zapis do ClassSelection, odświeżenie przycisków i opisu, zastosowanie na graczu).</summary>
    public void SelectClass(string className)
    {
        ClassSelection.ChosenName = className;
        RefreshClassUi(className);
        if (classApplier != null) classApplier.Apply(className);
    }

    private void RefreshClassUi(string className)
    {
        Tint(warriorButton, className == WarriorName);
        Tint(mageButton, className == MageName);
        if (classDescription == null) return;
        var cls = classApplier != null ? classApplier.Resolve(className) : null;
        classDescription.text = cls != null ? cls.Description : "";
    }

    private static void Tint(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = selected ? ClassSelected : ClassIdle;
    }

    private void Start()
    {
        if (gameManager == null || gameManager.IsRunStarted)
        {
            gameObject.SetActive(false);
            return;
        }
        RefreshClassUi(string.IsNullOrEmpty(ClassSelection.ChosenName) ? WarriorName : ClassSelection.ChosenName);
        SetHud(false);
        if (playerController != null) playerController.enabled = false;
        if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
        if (title != null)
            pulse = title.transform.DOScale(1.04f, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnDestroy()
    {
        pulse?.Kill();
    }

    private void Update()
    {
        if (leaving) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            Play();
        else if (Input.GetKeyDown(KeyCode.Escape) && controlsPanel != null && controlsPanel.activeSelf)
            controlsPanel.SetActive(false);
    }

    public void Play()
    {
        if (leaving) return;
        leaving = true;
        Sfx.Play(SfxKind.CardPick);
        if (group != null) group.blocksRaycasts = false;
        if (group == null) { Begin(); return; }
        group.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(Begin);
    }

    private void Begin()
    {
        pulse?.Kill();
        SetHud(true);
        if (playerController != null) playerController.enabled = true;
        if (gameManager != null) gameManager.StartRun();
        gameObject.SetActive(false);
    }

    private void ToggleControls()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(!controlsPanel.activeSelf);
    }

    private void SetHud(bool visible)
    {
        if (hudObjects == null) return;
        foreach (var go in hudObjects)
            if (go != null) go.SetActive(visible);
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
