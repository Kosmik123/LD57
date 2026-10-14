using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Poziomy stoją w scenie jeden pod drugim: gracz, który spadnie z krawędzi bieżącego poziomu, wraca na ostatnie
/// bezpieczne miejsce (ostatnia pozycja na ziemi i NavMeshu) i traci trochę HP. Dalej w dół tylko przez kratę.
/// </summary>
public class FallGuard : MonoBehaviour
{
    [SerializeField] private Rigidbody player;
    [SerializeField] private PlayerController controller;
    [SerializeField, Tooltip("Ile metrów poniżej wejścia bieżącego poziomu liczy się jako upadek.")] private float fallBelow = 4f;
    [SerializeField] private int fallDamage = 5;
    [SerializeField] private float sampleInterval = 0.25f;

    private Vector3 lastSafe;
    private bool hasSafe;
    private float nextSample;
    private int safeLevel;

    private void Update()
    {
        if (player == null || LevelManager.Instance == null) return;
        var lm = LevelManager.Instance;
        var root = lm.CurrentInstance;
        if (root == null) return;
        if (safeLevel != lm.CurrentLevel) { hasSafe = false; safeLevel = lm.CurrentLevel; }

        var entry = root.GetComponentInChildren<LevelEntry>();
        float floorY = entry != null ? entry.transform.position.y : player.position.y;

        bool grounded = controller == null || controller.IsGrounded;
        if (grounded && Time.time >= nextSample && player.position.y > floorY - 1.5f)
        {
            nextSample = Time.time + sampleInterval;
            if (NavMesh.SamplePosition(player.position, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - player.position.y) < 1.2f)
            {
                lastSafe = hit.position + Vector3.up * 0.3f;
                hasSafe = true;
            }
        }

        if (player.position.y < floorY - fallBelow)
        {
            var target = hasSafe ? lastSafe : (entry != null ? entry.transform.position : player.position);
            player.position = target;
            player.transform.position = target;
            player.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            var health = player.GetComponent<Health>();
            if (health != null && fallDamage > 0) { bool inv = health.IsInvulnerable; health.IsInvulnerable = false; health.ChangeHealth(-fallDamage); health.IsInvulnerable = inv; }
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash();
        }
    }
}
