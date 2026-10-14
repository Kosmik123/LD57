using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wędrowanie dla wrogów bez własnego Idle (strzelec): co 3-6 s losowy punkt NavMesha w promieniu radius,
/// dopóki EnemyAlert.IsAlerted jest false. Po alercie wyłącza się na stałe. Dodawany przez LevelPopulation.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Wanderer : MonoBehaviour
{
    [SerializeField] private float radius = 12f;
    [SerializeField] private Vector2 interval = new Vector2(3f, 6f);

    private NavMeshAgent agent;
    private EnemyAlert alert;
    private Health health;
    private float next;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        alert = GetComponent<EnemyAlert>();
        health = GetComponent<Health>();
        next = Time.time + Random.Range(interval.x, interval.y);
    }

    private void Update()
    {
        if ((alert != null && alert.IsAlerted) || (health != null && health.IsDead))
        {
            enabled = false;
            return;
        }
        if (Time.time < next || !agent.enabled || !agent.isOnNavMesh)
            return;
        next = Time.time + Random.Range(interval.x, interval.y);
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 target = transform.position + new Vector3(offset.x, 0f, offset.y);
        if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - transform.position.y) < 3f)
            agent.SetDestination(hit.position);
    }
}
