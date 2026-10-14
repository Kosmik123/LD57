using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walka z bossem na dowolnym poziomie (na razie poziom 1 do playtestów): po starcie poziomu spawnuje bossa
/// przy ostatniej strefie, poza liczeniem stref (krata zależy tylko od fal). Śmierć bossa poza poziomem 8 nie kończy
/// runu (GameManager sprawdza numer poziomu). Testy dymne wyłączają go przez DisabledForTests.
/// </summary>
public class BossEncounter : MonoBehaviour
{
    public static bool DisabledForTests;

    [SerializeField] private int levelNumber = 1;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField, Tooltip("Sekundy po starcie poziomu.")] private float delay = 3f;
    [SerializeField, Tooltip("Minimalna odległość od gracza przy spawnie.")] private float minDistance = 12f;

    private GameObject instance;

    private void OnEnable() => GameEvents.OnLevelStarted += GameEvents_OnLevelStarted;
    private void OnDisable() => GameEvents.OnLevelStarted -= GameEvents_OnLevelStarted;

    private void GameEvents_OnLevelStarted(int level)
    {
        if (level != levelNumber || DisabledForTests || bossPrefab == null || instance != null) return;
        StartCoroutine(SpawnAfterDelay());
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        if (DisabledForTests || instance != null) yield break;
        var zones = GetComponentsInChildren<WaveZone>(true);
        Vector3 at = transform.position;
        if (zones.Length > 0)
        {
            System.Array.Sort(zones, (a, b) => string.CompareOrdinal(a.name, b.name));
            at = zones[zones.Length - 1].transform.position;
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector3.Distance(player.transform.position, at) < minDistance)
        {
            var away = at - player.transform.position; away.y = 0f;
            at = player.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * minDistance;
        }
        if (NavMesh.SamplePosition(at, out var hit, 6f, NavMesh.AllAreas)) at = hit.position;
        instance = Instantiate(bossPrefab, at, Quaternion.identity);
        if (instance.TryGetComponent<NavMeshAgent>(out var agent))
        {
            agent.enabled = false;
            instance.transform.position = at;
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(at);
        }
        Debug.Log($"[BossEncounter] poziom {levelNumber}: boss w {at}");
    }
}
