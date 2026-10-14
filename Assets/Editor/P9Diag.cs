using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Diagnostyka raycastów do ścian w tymczasowej scenie poziomu (bash Tools/unity-batch.sh exec P9Diag.WallProbe).</summary>
public static class P9Diag
{
    public static void WallProbe()
    {
        for (int level = 1; level <= 3; level++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Levels/Level {level}.prefab");
            var instance = P1Wiring_Levels.OpenLevelInTempScene(prefab, level, out var previous);
            if (instance == null) { Debug.Log($"[P9Diag] Level {level}: brak instancji"); continue; }
            try
            {
                foreach (var c in instance.GetComponentsInChildren<Collider>(true))
                    Debug.Log($"[P9Diag] Level {level}: collider {c.name} typ={c.GetType().Name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} bounds={c.bounds} mesh={(c as MeshCollider)?.sharedMesh?.name}({(c as MeshCollider)?.sharedMesh?.vertexCount}) convex={(c as MeshCollider)?.convex}");
                var entry = instance.transform.Find("Level Entry");
                var zone = instance.GetComponentInChildren<WaveZone>(true);
                var origin = (zone != null ? zone.transform.position : entry.position) + Vector3.up * 1f;
                var mc = instance.GetComponentInChildren<MeshCollider>(true);
                Debug.Log($"[P9Diag] Level {level}: NavMesh wierzchołków {NavMesh.CalculateTriangulation().vertices.Length}, origin {origin}, backfaces={Physics.queriesHitBackfaces}, mc.Raycast w dół: {(mc != null && mc.Raycast(new Ray(origin, Vector3.down), out var mh, 50f) ? mh.distance.ToString("F1") : "nic")}, mc.Raycast +X: {(mc != null && mc.Raycast(new Ray(origin, Vector3.right), out var mh2, 60f) ? mh2.distance.ToString("F1") : "nic")}");
                Physics.SyncTransforms();
                bool prev = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
                var down = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log($"[P9Diag] Level {level}: origin {origin}, w dół trafień {down.Length}: {string.Join("; ", down.Select(h => h.collider.name + "@" + h.distance.ToString("F1")))}");
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var hits = Physics.RaycastAll(origin, d, 60f, ~0, QueryTriggerInteraction.Ignore);
                    Debug.Log($"[P9Diag] Level {level}: kierunek {i * 45}°: {hits.Length} trafień: {string.Join("; ", hits.Select(h => h.collider.name + "@" + h.distance.ToString("F1")))}");
                }
                Physics.queriesHitBackfaces = prev;
            }
            finally { P1Wiring_Levels.RestoreScene(previous); }
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
