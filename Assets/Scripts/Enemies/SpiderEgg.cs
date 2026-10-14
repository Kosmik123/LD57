using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Jajo pająka składane przez bossa (P12). Health 1 (jedno trafienie), tag Enemy, EnemyHealth z XP: zniszczenie = rozbryzg + XP.
/// Po hatchSeconds wykluwa zwykłego pająka (spiderPrefab) na podłodze (bez wspinaczki/siedzenia na suficie) i znika bez śmierci.
/// Jajko nie jest rejestrowane w WaveZone/LevelEnemyTracker, więc nie wpływa na kratę. Max naraz pilnuje boss (CountAlive).
/// </summary>
[RequireComponent(typeof(Health))]
public class SpiderEgg : MonoBehaviour
{
    private static readonly List<SpiderEgg> all = new List<SpiderEgg>();

    /// <summary>Liczba jajek na scenie (żywych, jeszcze niewyklutych).</summary>
    public static int CountAlive
    {
        get
        {
            all.RemoveAll(e => e == null || e.finished);
            return all.Count;
        }
    }

    [SerializeField] private float hatchSeconds = 6f;
    [SerializeField, Tooltip("Mały pająk z jajka (Spider.prefab).")] private GameObject spiderPrefab;
    [SerializeField, Tooltip("Obiekt do kołysania przed wykluciem (dziecko Visual).")] private Transform visual;
    [SerializeField] private float wobbleDegrees = 14f;

    private Health health;
    private float age;
    private bool finished;
    private Quaternion visualRotation = Quaternion.identity;
    private Vector3 visualScale = Vector3.one;
    private static readonly Color Cream = new Color(0.95f, 0.9f, 0.72f);
    private static readonly Color Purple = new Color(0.45f, 0.15f, 0.7f);

    private void Awake()
    {
        health = GetComponent<Health>();
        if (visual != null)
        {
            visualRotation = visual.localRotation;
            visualScale = visual.localScale;
        }
    }

    private void OnEnable()
    {
        all.Add(this);
        health.OnDied += Health_OnDied;
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (health != null) health.OnDied -= Health_OnDied;
    }

    private void Update()
    {
        if (finished) return;
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / hatchSeconds);

        if (visual != null)
        {
            // Kołysanie coraz szybsze i szersze, tuż przed wykluciem puls.
            float freq = Mathf.Lerp(2f, 11f, progress);
            float amp = wobbleDegrees * progress * progress;
            float phase = age * freq * Mathf.PI * 2f;
            visual.localRotation = visualRotation * Quaternion.Euler(Mathf.Sin(phase) * amp, 0f, Mathf.Cos(phase * 0.8f) * amp);
            float pulse = 1f + 0.08f * progress * Mathf.Sin(age * 20f);
            visual.localScale = visualScale * pulse;
        }

        if (age >= hatchSeconds)
            Hatch();
    }

    private void Health_OnDied()
    {
        if (finished) return;
        finished = true;
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        var pos = transform.position + Vector3.up * 0.25f;
        VoxelFx.Burst(pos, Cream, 12, 0.14f, 5f, 0.5f, 1f, true);
        VoxelFx.Burst(pos, Purple, 6, 0.1f, 4f, 0.5f, 1f, true);
        VoxelFx.Flash(pos, new Color(1.6f, 1.5f, 1.1f), 0.5f);
        Destroy(gameObject, 0.02f);
    }

    private void Hatch()
    {
        if (finished) return;
        finished = true;
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        var pos = transform.position;
        if (NavMesh.SamplePosition(pos, out var navHit, 2.5f, NavMesh.AllAreas))
            pos = navHit.position;

        VoxelFx.Burst(pos + Vector3.up * 0.25f, Cream, 14, 0.14f, 4.5f, 0.5f, 1f, true);
        VoxelFx.Burst(pos + Vector3.up * 0.25f, Purple, 8, 0.1f, 3.5f, 0.5f, 1f, true);
        VoxelFx.Ring(pos, 0.3f, new Color(1.4f, 1.3f, 1f), 12, 0.1f, 2.5f, 0.35f);

        if (spiderPrefab != null)
        {
            var spider = Instantiate(spiderPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            PreferGround(spider);
            if (spider.TryGetComponent<NavMeshAgent>(out var agent))
            {
                agent.enabled = false;
                spider.transform.position = pos;
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(pos);
            }
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// SpiderController.perchOnSpawn jest prywatne (true = siedzi na suficie/ścianie). Mały pająk z jajka ma od razu gonić gracza,
    /// więc zerujemy je refleksją przed Start. Brak pola = pająk zachowuje się domyślnie. Brakujący hook: publiczny setter.
    /// </summary>
    private static void PreferGround(GameObject spider)
    {
        var controller = spider.GetComponent<SpiderController>();
        if (controller == null) return;
        var field = typeof(SpiderController).GetField("perchOnSpawn",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(controller, false);
        else
            Debug.LogWarning("[SpiderEgg] SpiderController.perchOnSpawn nie znaleziono; pająk z jajka może usiąść na suficie.");
    }
}
