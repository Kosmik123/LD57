using Bipolar.Prototyping;
using UnityEngine;

/// <summary>
/// Poziomy w scenie. Domyślnie (showAllLevels) wszystkie 8 prefabów stoi naraz, jeden pod drugim, tak jak w FBX:
/// gracz widzi głębsze poziomy w dole, ale przechodzi dalej tylko przez kratę (teleport na LevelEntry kolejnego).
/// Fale głębszych poziomów odpalają się dopiero, gdy gracz wejdzie w ich strefę. Z showAllLevels = false ładuje
/// jeden prefab naraz (stare zachowanie).
/// </summary>
public class LevelManager : SceneSingleton<LevelManager>
{
    [SerializeField, Tooltip("8 prefabów poziomów w kolejności 1..8.")]
    private GameObject[] levelPrefabs;
    [SerializeField, Tooltip("Rodzic instancji poziomu (obiekt Levels w scenie).")]
    private Transform levelsParent;
    [SerializeField]
    private Rigidbody player;
    [SerializeField, Tooltip("Wszystkie poziomy w scenie od startu (widoczne w dół); false = jeden prefab naraz.")]
    private bool showAllLevels = true;

    /// <summary>Numer bieżącego poziomu 1–8 (0 przed startem).</summary>
    public int CurrentLevel { get; private set; }

    /// <summary>Instancja bieżącego poziomu (do szukania stref, kraty, wejścia tylko tego poziomu).</summary>
    public GameObject CurrentInstance => CurrentLevel >= 1 && CurrentLevel <= instances.Length ? instances[CurrentLevel - 1] : null;

    private GameObject[] instances = new GameObject[0];

    private void Start()
    {
        StartRun();
    }

    public void StartRun()
    {
        // usuń ręcznie położone poziomy ze sceny (tylko obiekty z LevelEnemyTracker, nie np. Camera Center)
        if (levelsParent != null)
            for (int i = levelsParent.childCount - 1; i >= 0; i--)
            {
                var child = levelsParent.GetChild(i);
                if (child.GetComponent<LevelEnemyTracker>() != null || child.name.StartsWith("Level "))
                    Destroy(child.gameObject);
            }

        instances = new GameObject[levelPrefabs != null ? levelPrefabs.Length : 0];
        CurrentLevel = 0;
        if (showAllLevels)
        {
            CurrentLevel = 1; // ustawione przed Instantiate: LevelAmbience stosuje klimat tylko bieżącego poziomu
            for (int i = 0; i < instances.Length; i++)
                if (levelPrefabs[i] != null)
                    instances[i] = Instantiate(levelPrefabs[i], levelsParent);
            CurrentLevel = 0;
            EnterLevel(1);
        }
        else
        {
            LoadLevel(1);
        }
    }

    public void GoToNextLevel()
    {
        if (CurrentLevel >= 8 || CurrentLevel >= levelPrefabs.Length)
            return; // koniec runu przez bossa
        if (showAllLevels) EnterLevel(CurrentLevel + 1);
        else LoadLevel(CurrentLevel + 1);
    }

    private void LoadLevel(int level)
    {
        if (levelPrefabs == null || level < 1 || level > levelPrefabs.Length || levelPrefabs[level - 1] == null)
        {
            Debug.LogError($"Brak prefabu poziomu {level} w LevelManager.", this);
            return;
        }

        if (CurrentInstance != null)
            Destroy(CurrentInstance);

        CurrentLevel = level;
        instances[level - 1] = Instantiate(levelPrefabs[level - 1], levelsParent);
        MovePlayerToEntry(level);
        GameEvents.RaiseLevelStarted(CurrentLevel);
    }

    private void EnterLevel(int level)
    {
        if (level < 1 || level > instances.Length || instances[level - 1] == null)
        {
            Debug.LogError($"Brak instancji poziomu {level} w LevelManager.", this);
            return;
        }
        CurrentLevel = level;
        MovePlayerToEntry(level);
        GameEvents.RaiseLevelStarted(CurrentLevel);
    }

    private void MovePlayerToEntry(int level)
    {
        var entry = instances[level - 1].GetComponentInChildren<LevelEntry>();
        if (entry != null && player != null)
        {
            player.position = entry.transform.position;
            player.transform.position = entry.transform.position;
            player.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        else
        {
            Debug.LogWarning($"Poziom {level}: brak LevelEntry lub gracza, gracz nie został przeniesiony.", this);
        }
    }
}
