using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pętla runu: śmierć gracza, wygrana po bossie, restart sceny.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField]
    private Health playerHealth;

    [SerializeField]
    private RunState runState;
    public RunState RunState => runState;

    [SerializeField, Tooltip("Gra od razu po załadowaniu sceny (bez menu startowego). Domyślnie false: czeka na StartRun().")]
    private bool autoStart;

    public bool IsRunOver { get; private set; }
    /// <summary>False, dopóki gracz nie kliknie Graj w menu startowym.</summary>
    public bool IsRunStarted { get; private set; }

    public event System.Action OnRunStarted;

    private bool startEventPending;

    private void Awake()
    {
        if (runState == null)
            runState = GetComponent<RunState>();
        // Jaskinia z jamu ma normalne na zewnątrz: raycasty od środka (linia wzroku, ściany pod propy, podłoga) muszą
        // trafiać w tylne ścianki, inaczej nic nie trafia.
        Physics.queriesHitBackfaces = true;
        // Menu pomijane w batchu (testy), po restarcie runu i gdy ustawiono StartMenu.SkipMenu.
        bool restarted = StartMenu.ConsumeRestart();
        if (autoStart || Application.isBatchMode || StartMenu.SkipMenu || restarted)
        {
            IsRunStarted = true;
            startEventPending = true;
            Time.timeScale = 1;
        }
        else
        {
            Time.timeScale = 0;
        }
    }

    private void Start()
    {
        if (startEventPending)
        {
            startEventPending = false;
            OnRunStarted?.Invoke();
        }
    }

    /// <summary>Start runu z menu: zdejmuje pauzę. Wielokrotne wywołanie jest ignorowane.</summary>
    public void StartRun()
    {
        if (IsRunStarted)
            return;
        IsRunStarted = true;
        Time.timeScale = 1;
        OnRunStarted?.Invoke();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDied += PlayerHealth_OnDied;
        EnemyHealth.OnEnemyDied += EnemyHealth_OnEnemyDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= PlayerHealth_OnDied;
        EnemyHealth.OnEnemyDied -= EnemyHealth_OnEnemyDied;
    }

    private void Update()
    {
        if (!IsRunOver)
            return;
        // Po końcu runu pauza jest wymuszona: hit stop ani ekran kart nie mogą jej cofnąć.
        if (Time.timeScale != 0f)
            Time.timeScale = 0f;
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space))
            RestartRun();
    }

    private void PlayerHealth_OnDied()
    {
        if (IsRunOver)
            return;
        IsRunOver = true;
        Time.timeScale = 0;
        GameEvents.RaisePlayerDied();
    }

    private void EnemyHealth_OnEnemyDied(EnemyHealth enemy)
    {
        if (IsRunOver || !enemy.IsBoss)
            return;
        IsRunOver = true;
        Time.timeScale = 0;
        GameEvents.RaiseRunWon();
    }

    public void RestartRun()
    {
        Time.timeScale = 1;
        StartMenu.MarkRestart();
        GameEvents.ResetEvents();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
