using UnityEngine;

/// <summary>Rodzaj miejsca, przy którym stoi landmark.</summary>
public enum LandmarkKind { Entry, Gate, Zone }

/// <summary>
/// Pusty obiekt `Landmark N` w prefabie poziomu (stawia go P1Wiring_Levels / P2Wiring_Ambience). Pivot stoi na podłodze
/// (NavMesh). Hero prop zależny od biomu ubiera `CaveDecor` w runtime (las: wielkie drzewo, skała: menhir,
/// kryształy: trzy kryształy, otchłań: kieł). Osobny plik, bo Unity wiąże MonoBehaviour z nazwą pliku.
/// </summary>
public class LandmarkMarker : MonoBehaviour
{
    [SerializeField] private LandmarkKind kind;

    public LandmarkKind Kind => kind;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 5f);
    }
}
