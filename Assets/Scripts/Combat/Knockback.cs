using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Odrzut wroga po trafieniu: przez `seconds` przesuwa go o `distance` (OutCubic) w zadanym kierunku.
/// Z NavMeshAgentem używa agent.Move i zeruje jego prędkość co klatkę (agent zostaje na siatce, AI nie walczy
/// z odrzutem); bez agenta przesuwa Rigidbody. Kolejne trafienie w trakcie restartuje odrzut.
/// </summary>
public class Knockback : MonoBehaviour
{
    [SerializeField, Tooltip("Mnożnik dystansu odrzutu dla tego wroga (boss 0,15, slime 1).")]
    private float resistance = 1f;

    private NavMeshAgent agent;
    private Rigidbody body;
    private Vector3 direction;
    private float total, traveled, duration, elapsed;
    private bool active;

    public bool IsActive => active;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
    }

    public void Apply(Vector3 dir, float distance, float seconds = 0.15f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f || distance * resistance <= 0f) return;
        direction = dir.normalized;
        total = distance * resistance;
        duration = Mathf.Max(0.02f, seconds);
        traveled = 0f;
        elapsed = 0f;
        active = true;
    }

    private void Update()
    {
        if (!active) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f); // OutCubic
        float target = total * eased;
        var step = direction * (target - traveled);
        traveled = target;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            agent.Move(step);
        }
        else if (body != null && !body.isKinematic)
        {
            body.MovePosition(body.position + step);
        }
        else
        {
            transform.position += step;
        }

        if (t >= 1f) active = false;
    }
}
