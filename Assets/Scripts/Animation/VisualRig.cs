using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wspólny szkielet animacji proceduralnej wroga. Animuje WYŁĄCZNIE transformy pod dzieckiem Visual
/// (Visual, Body, Eyes, Legs/Leg_i), nigdy root wroga. Wszystkie kanały są składane w jednym miejscu (LateUpdate),
/// więc animatory nie walczą o transformy, a nowa animacja na kanale zabija poprzednią (brak kumulacji).
/// Skala Visual: poza (hold) x reakcja (puls) x proceduralne (ProcY) w osi Y, objętość zachowana (xz = 1/sqrt y),
/// plus jednolite nadęcie (Inflate). Pozycja Body: reakcja (kick/punch) + proceduralna (ProcOffset).
/// </summary>
[DefaultExecutionOrder(100)]
public class VisualRig : MonoBehaviour
{
    public const int MaxLegs = 8;

    public Transform Visual { get; private set; }
    public Transform Body { get; private set; }
    public Transform Eyes { get; private set; }
    public Transform[] Legs { get; private set; } = new Transform[MaxLegs];
    public bool HasVisual => Visual != null;
    /// <summary>Prędkość wroga (NavMeshAgent.velocity, a bez agenta różnica pozycji roota).</summary>
    public float Speed { get; private set; }

    // kanały ustawiane co klatkę przez animatory
    [System.NonSerialized] public float ProcY = 1f;
    [System.NonSerialized] public Vector3 ProcOffset;
    [System.NonSerialized] public Vector3 ProcEuler;

    private float poseY = 1f, reactY = 1f, inflate = 1f, eyeScale = 1f;
    private Vector3 reactOffset;
    private readonly float[] legAngle = new float[MaxLegs];

    private Tween poseTween, reactTween, inflateTween, offsetTween, eyeTween;
    private Vector3 visualScale = Vector3.one, bodyPos, eyesScale = Vector3.one;
    private Quaternion bodyRot = Quaternion.identity;
    private readonly Quaternion[] legRot = new Quaternion[MaxLegs];
    private bool inited, frozen;
    private NavMeshAgent agent;
    private Vector3 lastPos;

    private void Awake() => EnsureInit();

    public void EnsureInit()
    {
        if (inited) return;
        inited = true;
        agent = GetComponent<NavMeshAgent>();
        lastPos = transform.position;

        Visual = transform.Find("Visual");
        if (Visual == null)
        {
            var r = GetComponentInChildren<Renderer>();
            if (r != null && r.transform != transform)
            {
                var p = r.transform.parent;
                Visual = (p == null || p == transform) ? r.transform : p;
            }
        }
        if (Visual == null) return;

        Body = Visual.Find("Body") ?? Visual;
        Eyes = Visual.Find("Eyes");
        for (int i = 0; i < MaxLegs; i++)
            Legs[i] = Visual.Find("Legs/Leg_" + i);

        visualScale = Visual.localScale;
        bodyPos = Body.localPosition;
        bodyRot = Body.localRotation;
        if (Eyes != null) eyesScale = Eyes.localScale;
        for (int i = 0; i < MaxLegs; i++)
            if (Legs[i] != null) legRot[i] = Legs[i].localRotation;
    }

    private void OnDestroy() => KillAll();

    private void KillAll()
    {
        poseTween?.Kill(); reactTween?.Kill(); inflateTween?.Kill(); offsetTween?.Kill(); eyeTween?.Kill();
        poseTween = reactTween = inflateTween = offsetTween = eyeTween = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        var pos = transform.position;
        float measured = dt > 0f ? (pos - lastPos).magnitude / dt : 0f;
        lastPos = pos;
        Speed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : measured;
    }

    private void LateUpdate()
    {
        EnsureInit();
        if (Visual == null || frozen) return;
        Apply();
    }

    private void Apply()
    {
        float y = Mathf.Max(0.05f, poseY * reactY * ProcY);
        float xz = 1f / Mathf.Sqrt(y);
        var s = visualScale * inflate;
        Visual.localScale = new Vector3(s.x * xz, s.y * y, s.z * xz);

        Body.localPosition = bodyPos + reactOffset + ProcOffset;
        Body.localRotation = bodyRot * Quaternion.Euler(ProcEuler);

        if (Eyes != null) Eyes.localScale = eyesScale * eyeScale;
        for (int i = 0; i < MaxLegs; i++)
            if (Legs[i] != null)
                Legs[i].localRotation = legRot[i] * Quaternion.Euler(legAngle[i], 0f, 0f);
    }

    // ------------------------------------------------------------ nogi / oczy

    public void SetLegAngle(int i, float degreesX)
    {
        if (i >= 0 && i < MaxLegs) legAngle[i] = degreesX;
    }

    public void SetEyes(float scale, float seconds)
    {
        EnsureInit();
        eyeTween?.Kill();
        if (seconds <= 0f) { eyeScale = scale; return; }
        eyeTween = DOTween.To(() => eyeScale, v => eyeScale = v, scale, seconds).SetEase(Ease.OutBack);
    }

    // ------------------------------------------------------------ skala

    /// <summary>Poza trzymana (przyczajenie, stretch w locie). Zastępuje poprzednią pozę.</summary>
    public void HoldPose(float y, float seconds, Ease ease = Ease.OutQuad)
    {
        EnsureInit();
        poseTween?.Kill();
        if (seconds <= 0f) { poseY = y; return; }
        poseTween = DOTween.To(() => poseY, v => poseY = v, y, seconds).SetEase(ease);
    }

    /// <summary>Puls: do y w seconds, potem powrót do 1 (OutElastic 0,3 s). Zastępuje poprzedni puls.</summary>
    public void Squash(float y, float seconds) => Squash(y, seconds, 0.3f, Ease.OutElastic);

    public void Squash(float y, float seconds, float recoverSeconds, Ease recoverEase)
    {
        EnsureInit();
        reactTween?.Kill();
        var seq = DOTween.Sequence();
        if (seconds <= 0f) reactY = y;
        else seq.Append(DOTween.To(() => reactY, v => reactY = v, y, seconds).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => reactY, v => reactY = v, 1f, recoverSeconds).SetEase(recoverEase));
        reactTween = seq;
    }

    /// <summary>Przyczajenie trzymane do Release (kanał reakcji).</summary>
    public void Crouch(float y, float seconds)
    {
        EnsureInit();
        reactTween?.Kill();
        reactTween = DOTween.To(() => reactY, v => reactY = v, y, Mathf.Max(0.01f, seconds)).SetEase(Ease.OutQuad);
    }

    public void Release(float seconds, Ease ease = Ease.OutElastic)
    {
        EnsureInit();
        reactTween?.Kill();
        reactTween = DOTween.To(() => reactY, v => reactY = v, 1f, Mathf.Max(0.01f, seconds)).SetEase(ease);
    }

    /// <summary>Jednolite nadęcie (bez zachowania objętości, to celowy efekt).</summary>
    public void Inflate(float uniform, float seconds, Ease ease)
    {
        EnsureInit();
        inflateTween?.Kill();
        if (seconds <= 0f) { inflate = uniform; return; }
        inflateTween = DOTween.To(() => inflate, v => inflate = v, uniform, seconds).SetEase(ease);
    }

    /// <summary>Pulsowanie jednolitej skali 1 -> amp -> 1, count razy po period sekund.</summary>
    public void Pulse(float amp, int count, float period)
    {
        EnsureInit();
        inflateTween?.Kill();
        var seq = DOTween.Sequence();
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            seq.Append(DOTween.To(() => inflate, v => inflate = v, amp, period * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(() => inflate, v => inflate = v, 1f, period * 0.5f).SetEase(Ease.InOutSine));
        }
        inflateTween = seq;
    }

    // ------------------------------------------------------------ pozycja

    /// <summary>Szarpnięcie Body o localDir (lokalnie względem rodzica Visual) i powrót (OutBack).</summary>
    public void Punch(Vector3 localDir, float amount) => Kick(localDir.normalized * amount, 0.04f, 0.25f, Ease.OutBack);

    /// <summary>Wychylenie Body o offset w outSeconds, powrót w backSeconds.</summary>
    public void Kick(Vector3 offset, float outSeconds, float backSeconds, Ease backEase)
    {
        EnsureInit();
        offsetTween?.Kill();
        var seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => reactOffset, v => reactOffset = v, offset, Mathf.Max(0.01f, outSeconds)).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => reactOffset, v => reactOffset = v, Vector3.zero, backSeconds).SetEase(backEase));
        offsetTween = seq;
    }

    /// <summary>Kierunek od gracza w przestrzeni lokalnej rodzica Visual (do Punch).</summary>
    public Vector3 DirectionAwayFromPlayerLocal()
    {
        EnsureInit();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || Visual == null) return Vector3.back;
        var d = transform.position - player.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return Vector3.back;
        var parent = Visual.parent != null ? Visual.parent : transform;
        var local = parent.InverseTransformDirection(d.normalized);
        local.y = 0f;
        return local.sqrMagnitude < 0.0001f ? Vector3.back : local.normalized;
    }

    // ------------------------------------------------------------ reset / śmierć

    public void ResetPose()
    {
        EnsureInit();
        KillAll();
        frozen = false;
        poseY = reactY = inflate = eyeScale = 1f;
        reactOffset = Vector3.zero;
        ProcY = 1f; ProcOffset = Vector3.zero; ProcEuler = Vector3.zero;
        for (int i = 0; i < MaxLegs; i++) legAngle[i] = 0f;
        if (Visual != null) Apply();
    }

    /// <summary>Zapadnięcie: Y -> 0,05 i XZ -> 1,4 w 75% czasu, potem do 0. Zamraża rig.</summary>
    public void Collapse(float seconds)
    {
        EnsureInit();
        KillAll();
        frozen = true;
        if (Visual == null) return;
        Visual.DOKill();
        var b = visualScale;
        float t1 = seconds * 0.75f;
        var seq = DOTween.Sequence().SetTarget(Visual);
        seq.Append(Visual.DOScale(new Vector3(b.x * 1.4f, b.y * 0.05f, b.z * 1.4f), t1).SetEase(Ease.OutQuad));
        seq.Append(Visual.DOScale(Vector3.zero, seconds - t1).SetEase(Ease.InQuad));
    }
}

/// <summary>Mnożenie koloru właściwości materiału przez MaterialPropertyBlock (materiały zostają nietknięte).</summary>
public class RendererTint
{
    private readonly Renderer[] renderers;
    private readonly Color[] baseColors;
    private readonly int id;
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

    public RendererTint(Transform root, string property)
    {
        id = Shader.PropertyToID(property);
        var list = new System.Collections.Generic.List<Renderer>();
        var cols = new System.Collections.Generic.List<Color>();
        if (root != null)
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(id)) continue;
                list.Add(r); cols.Add(m.GetColor(id));
            }
        renderers = list.ToArray();
        baseColors = cols.ToArray();
    }

    public bool Any => renderers.Length > 0;

    public void Set(float mul)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            var c = baseColors[i];
            block.SetColor(id, new Color(c.r * mul, c.g * mul, c.b * mul, c.a));
            renderers[i].SetPropertyBlock(block);
        }
    }
}
