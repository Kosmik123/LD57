using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wspólny alarm wrogów: kontroler woła Raise(pozycja) przy pierwszym wykryciu gracza, a każdy EnemyAlert
/// w promieniu alertRadius ustawia IsAlerted i woła OnAlerted (kontrolery przechodzą do pościgu).
/// </summary>
public class EnemyAlert : MonoBehaviour
{
    public static event System.Action<Vector3> OnAlert;

    private static readonly List<EnemyAlert> Active = new List<EnemyAlert>();

    [SerializeField] private float alertRadius = 8f;

    public bool IsAlerted { get; private set; }
    public event System.Action OnAlerted;

    public static void Raise(Vector3 at)
    {
        OnAlert?.Invoke(at);
    }

    private void OnEnable()
    {
        OnAlert += HandleAlert;
        Active.Add(this);
    }

    private void OnDisable()
    {
        OnAlert -= HandleAlert;
        Active.Remove(this);
    }

    private void HandleAlert(Vector3 at)
    {
        if ((transform.position - at).sqrMagnitude > alertRadius * alertRadius)
            return;
        IsAlerted = true;
        OnAlerted?.Invoke();
    }
}
