using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Klimat poziomow 1-8, swiatlo kraty, napis sterowania i lampa gracza.</summary>
public static class P2Wiring_Ambience
{
    const string LevelsFolder = "Assets/Prefabs/Levels";
    const string PlayerPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("LD57/Wire P2 Ambience")]
    public static void Run()
    {
        for (int level = 1; level <= 8; level++) WireLevel(level);
        WirePlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[P2Ambience] done");
    }

    static void WireLevel(int level)
    {
        var path = $"{LevelsFolder}/Level {level}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[P2Ambience] brak {path}"); return; }
        // Pozycje pochodni/grzybów liczone w tymczasowej scenie (NavMesh + collidery), przed otwarciem prefabu do zapisu.
        var lights = PlanWallLights(level);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<LevelAmbience>() == null)
                GetOrAdd<LevelAmbience>(root).ApplyPresetValues(level);
            else
                Debug.Log($"[P2Ambience] Level {level}: LevelAmbience istnieje, wartości bez zmian");

            var gateT = root.transform.Find("Exit Gate");
            if (gateT != null)
            {
                var lightGo = GetOrCreateChild(gateT.gameObject, "Exit Light");
                if (lightGo.transform.localPosition == Vector3.zero)
                    lightGo.transform.localPosition = new Vector3(0, 3, 0);
                var l = GetOrAdd<Light>(lightGo);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.15f);
                l.range = 6f;
                l.intensity = 1.5f;
                l.enabled = false;
                var gate = gateT.GetComponent<ExitGate>();
                if (gate != null) SetRef(gate, "exitLight", l);
                else Debug.LogWarning($"[P2Ambience] Level {level}: brak ExitGate");
            }
            else Debug.LogWarning($"[P2Ambience] Level {level}: brak Exit Gate");

            if (level == 1) WireSign(root);
            BuildWallLights(root, level, lights);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- pochodnie i grzyby

    struct WallLightSpot { public Vector3 Floor; public Vector3 Wall; public Vector3 Normal; public bool Standing; }

    /// <summary>Odstęp pochodni po ścieżce (m): poziomy 1–2 co 10, 3–4 co 14, 5–6 grzyby co 12, 7–8 nic.</summary>
    static float WallLightSpacing(int level) => level <= 2 ? 10f : level <= 4 ? 14f : level <= 6 ? 12f : 0f;

    /// <summary>
    /// Punkty co `spacing` po ścieżce od wejścia do kraty, przy najbliższej ścianie (8 kierunków, do 4 m).
    /// Liczone na żywym NavMeshu i colliderach poziomu w tymczasowej scenie; zwraca puste, gdy nie ma ścieżki.
    /// </summary>
    static List<WallLightSpot> PlanWallLights(int level)
    {
        var result = new List<WallLightSpot>();
        float spacing = WallLightSpacing(level);
        if (spacing <= 0f) return result;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelsFolder}/Level {level}.prefab");
        var instance = P1Wiring_Levels.OpenLevelInTempScene(prefab, level, out var previousScene);
        if (instance == null) return result;
        try
        {
            var entryT = instance.transform.Find("Level Entry");
            var gateT = instance.transform.Find("Exit Gate");
            if (entryT == null || gateT == null) { Debug.LogWarning($"[P2Ambience] Level {level}: brak Level Entry/Exit Gate, bez pochodni"); return result; }
            if (!NavMesh.SamplePosition(entryT.position, out var a, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(gateT.position, out var b, 4f, NavMesh.AllAreas))
            { Debug.LogWarning($"[P2Ambience] Level {level}: wejście/krata poza NavMeshem, bez pochodni"); return result; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            { Debug.LogWarning($"[P2Ambience] Level {level}: brak ścieżki wejście-krata, bez pochodni"); return result; }
            float len = P1Wiring_Levels.PathLength(path.corners);
            int misses = 0;
            for (float d = spacing * 0.5f; d < len - 3f; d += spacing)
            {
                var floor = P1Wiring_Levels.PointAlong(path.corners, d);
                if (P1Wiring_Levels.NearestWall(instance, floor, 14f, out var hit, out _)) // korytarze jamowej jaskini mają 10–20 m szerokości
                    result.Add(new WallLightSpot { Floor = floor, Wall = hit.point, Normal = hit.normal });
                else if (NavMesh.FindClosestEdge(floor, out var edge, NavMesh.AllAreas) && edge.distance > 1f)
                {
                    // poziomy 2–8: bryła leży pod podłogą (ściany idą w dół), więc pochodnia stoi na słupie przy krawędzi
                    var inward = floor - edge.position; inward.y = 0f;
                    inward = inward.sqrMagnitude > 0.001f ? inward.normalized : Vector3.forward;
                    var post = edge.position + inward * 0.6f;
                    result.Add(new WallLightSpot { Floor = new Vector3(post.x, floor.y, post.z), Wall = post, Normal = inward, Standing = true });
                }
                else misses++;
            }
            Debug.Log($"[P2Ambience] Level {level}: ścieżka {len:F0} m, {result.Count} miejsc na pochodnie/grzyby, {misses} bez ściany i bez krawędzi");
        }
        finally { P1Wiring_Levels.RestoreScene(previousScene); }
        return result;
    }

    static void BuildWallLights(GameObject root, int level, List<WallLightSpot> spots)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var c = root.transform.GetChild(i);
            if (c.name.StartsWith("Torch ") || c.name.StartsWith("Mushrooms ")) Object.DestroyImmediate(c.gameObject);
        }
        if (spots.Count == 0) return;

        if (level <= 4)
        {
            var holderMat = P1Wiring_Levels.EnsureMaterial("Torch Holder", new Color(0.25f, 0.14f, 0.07f), null, 0.05f);
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var go = new GameObject($"Torch {i + 1}");
                go.transform.SetParent(root.transform, false);
                var n = s.Normal; n.y = 0f; n = n.sqrMagnitude > 0.001f ? n.normalized : Vector3.up;
                go.transform.position = new Vector3(s.Wall.x, s.Floor.y + (s.Standing ? 1.7f : 1.8f), s.Wall.z) + (s.Standing ? Vector3.zero : n * 0.15f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.3f);
                l.range = 7f;
                l.intensity = 2.5f;
                l.shadows = LightShadows.None;
                go.AddComponent<LightFlicker>();
                go.AddComponent<Torch>();
                var holder = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Cylinder, "Holder", go.transform, holderMat);
                holder.transform.localPosition = new Vector3(0f, s.Standing ? -0.85f : -0.1f, 0f);
                holder.transform.localScale = s.Standing ? new Vector3(0.12f, 0.85f, 0.12f) : new Vector3(0.1f, 0.15f, 0.1f); // słup 1,7 m albo uchwyt 0,3 m
            }
        }
        else
        {
            var rng = new System.Random(level * 7919);
            var shroomMat = P1Wiring_Levels.EnsureMaterial("Mushroom Glow", new Color(0.1f, 0.35f, 0.3f), new Color(0.2f, 1.2f, 0.9f), 0.4f);
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var n = s.Normal; n.y = 0f; n = n.sqrMagnitude > 0.001f ? n.normalized : Vector3.up;
                var go = new GameObject($"Mushrooms {i + 1}");
                go.transform.SetParent(root.transform, false);
                var basePos = new Vector3(s.Wall.x, s.Floor.y, s.Wall.z) + n * 0.3f;
                go.transform.position = basePos + Vector3.up * 0.35f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.5f, 1f, 0.8f);
                l.range = 4f;
                l.intensity = 1.5f;
                l.shadows = LightShadows.None;
                for (int k = 0; k < 3; k++)
                {
                    float diameter = 0.15f + (float)rng.NextDouble() * 0.15f;
                    var off = new Vector3((float)(rng.NextDouble() - 0.5) * 0.5f, 0f, (float)(rng.NextDouble() - 0.5) * 0.5f);
                    var sph = P1Wiring_Levels.ColliderlessPrimitive(PrimitiveType.Sphere, $"Mushroom {k + 1}", go.transform, shroomMat);
                    sph.transform.position = basePos + off + Vector3.up * (diameter * 0.4f);
                    sph.transform.localScale = Vector3.one * diameter;
                }
            }
        }
    }

    static void WireSign(GameObject root)
    {
        var entry = root.GetComponentInChildren<LevelEntry>(true);
        if (entry == null) { Debug.LogWarning("[P2Ambience] Level 1: brak Level Entry"); return; }
        var go = GetOrCreateChild(root, "Controls Sign");
        Vector3 fwd = entry.transform.forward; fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        // Wejscie lezy 0,5 m nad NavMeshem (P1Wiring_Levels); napis 2 m przed wejsciem, 0,05 nad podloga.
        Vector3 pos = entry.transform.position + fwd * 2f;
        pos.y = entry.transform.position.y - 0.5f + 0.05f;
        go.transform.position = pos;
        // Napis lezy (X 90), a jego gora wskazuje forward wejscia.
        go.transform.rotation = Quaternion.LookRotation(Vector3.down, fwd);

        var tmp = GetOrAdd<TextMeshPro>(go);
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 3.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(12, 4);
        var sign = GetOrAdd<WorldSign>(go);
        const string text = "WSAD ruch · LPM atak · Shift dash\nQ E R skille · Spacja uderzenie\nZejdź w dół. Nie wracaj.";
        tmp.text = text;
        SetProp(sign, "text", p => p.stringValue = text);
        SetRef(sign, "label", tmp);
    }

    static void WirePlayer()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null) { Debug.LogWarning("[P2Ambience] brak Player.prefab"); return; }
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var lampGo = GetOrCreateChild(root, "Lamp");
            if (lampGo.transform.localPosition == Vector3.zero) lampGo.transform.localPosition = new Vector3(0, 2f, 0);
            var l = GetOrAdd<Light>(lampGo);
            l.type = LightType.Point;
            l.range = 12f;
            l.intensity = 0f;
            l.color = new Color(1f, 0.8f, 0.55f);
            l.enabled = false;
            var lamp = GetOrAdd<PlayerLamp>(lampGo);
            SetRef(lamp, "lampLight", l);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SetProp(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[P2Ambience] brak pola '{field}' w {target.GetType().Name}"); return; }
        set(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetRef(Object target, string field, Object value) => SetProp(target, field, p => p.objectReferenceValue = value);
}
