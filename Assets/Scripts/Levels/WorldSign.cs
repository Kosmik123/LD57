using TMPro;
using UnityEngine;

/// <summary>Napis w swiecie (TextMeshPro, nie UGUI) ustawiany z pola tekstowego.</summary>
public class WorldSign : MonoBehaviour
{
    [SerializeField, TextArea(2, 6)] private string text;
    [SerializeField] private TextMeshPro label;

    private void Awake()
    {
        // Napis leży na ziemi, ale jego góra wskazuje „górę ekranu”: kierunek patrzenia kamery rzutowany na podłogę.
        var cam = Camera.main;
        if (cam != null)
        {
            var screenUp = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (screenUp.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(Vector3.down, screenUp.normalized);
        }
        if (label == null) label = GetComponent<TextMeshPro>();
        if (label != null) label.text = text;
    }
}
