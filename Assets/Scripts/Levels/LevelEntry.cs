using UnityEngine;

/// <summary>Marker w prefabie poziomu: tu trafia gracz po wejściu na poziom.</summary>
public class LevelEntry : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 p = transform.position;
        Vector3 f = transform.forward;
        Gizmos.DrawWireSphere(p, 0.4f);
        Gizmos.DrawLine(p, p + f * 1.5f);
        Gizmos.DrawLine(p + f * 1.5f, p + f * 1.1f + transform.right * 0.3f);
        Gizmos.DrawLine(p + f * 1.5f, p + f * 1.1f - transform.right * 0.3f);
    }
}
