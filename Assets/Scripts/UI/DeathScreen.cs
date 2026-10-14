using TMPro;
using UnityEngine;

/// <summary>Ekran końca runu (śmierć lub wygrana). Działa przy timeScale = 0.</summary>
public class DeathScreen : MonoBehaviour
{
    [SerializeField, Tooltip("Panel, domyślnie wyłączony.")]
    private GameObject root;
    [SerializeField]
    private TMP_Text title;
    [SerializeField]
    private TMP_Text stats;
    [SerializeField]
    private GameManager gameManager;
    [SerializeField]
    private RunState runState;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += GameEvents_OnPlayerDied;
        GameEvents.OnRunWon += GameEvents_OnRunWon;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= GameEvents_OnPlayerDied;
        GameEvents.OnRunWon -= GameEvents_OnRunWon;
    }

    private RunState State => runState != null ? runState : (gameManager != null ? gameManager.RunState : null);

    private void GameEvents_OnPlayerDied()
    {
        var state = State;
        Show($"Zginąłeś na poziomie {(state != null ? state.DeepestLevel : 0)}", state);
    }

    private void GameEvents_OnRunWon()
    {
        Show("Dno osiągnięte", State);
    }

    private void Show(string titleText, RunState state)
    {
        if (root != null)
            root.SetActive(true);
        if (title != null)
            title.text = titleText;
        if (stats == null)
            return;

        if (state == null)
        {
            stats.text = "R: od nowa";
            return;
        }

        int total = Mathf.FloorToInt(state.ElapsedTime);
        stats.text = $"Czas: {total / 60:00}:{total % 60:00}\nZabójstwa: {state.Kills}\nLevel bohatera: {state.HeroLevel}\n\nR: od nowa";
    }
}
