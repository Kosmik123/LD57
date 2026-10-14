using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wejście wroga do sceny (dodawane przez WaveZone w momencie spawnu): Visual rośnie z 0,1 i wyjeżdża spod ziemi,
/// chmura pyłu i plask. W tym czasie agent stoi, a EnemyMeleeAttack jest wyłączony. Z VisualRig współpracuje
/// przez Inflate (rig sam składa skalę Visual w LateUpdate), a na końcu daje Squash.
/// </summary>
public class SpawnEntrance : MonoBehaviour
{
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float startScale = 0.1f;
    [SerializeField] private float startDepth = 0.6f;

    private NavMeshAgent agent;
    private EnemyMeleeAttack melee;
    private VisualRig rig;
    private Transform visual;
    private float baseY;
    private float endTime;
    private bool running;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        melee = GetComponent<EnemyMeleeAttack>();
        rig = GetComponent<VisualRig>();
        visual = transform.Find("Visual");
        if (visual == null)
        {
            var r = GetComponentInChildren<Renderer>();
            if (r != null && r.transform != transform)
            {
                var p = r.transform.parent;
                visual = (p == null || p == transform) ? r.transform : p;
            }
        }

        running = true;
        endTime = Time.time + duration;
        if (melee != null)
            melee.enabled = false;
        HoldAgent();

        SpawnDust(transform.position);
        Sfx.Play(SfxKind.EnemyDie, transform.position, 0.4f);

        if (visual == null)
        {
            Invoke(nameof(Finish), duration);
            return;
        }

        baseY = visual.localPosition.y;
        var pos = visual.localPosition;
        visual.localPosition = new Vector3(pos.x, baseY - startDepth, pos.z);
        visual.DOLocalMoveY(baseY, duration).SetEase(Ease.OutQuad).SetTarget(visual);

        if (rig != null)
        {
            rig.EnsureInit();
            rig.Inflate(startScale, 0f, Ease.Linear);
            rig.Inflate(1f, duration, Ease.OutBack);
        }
        else
        {
            var baseScale = visual.localScale;
            visual.localScale = baseScale * startScale;
            visual.DOScale(baseScale, duration).SetEase(Ease.OutBack).SetTarget(visual);
        }
        Invoke(nameof(Finish), duration);
    }

    private void Update()
    {
        // kontrolery (strzelec, pająk) potrafią wznowić agenta same: trzymaj go w miejscu do końca wejścia
        if (running)
            HoldAgent();
    }

    private void HoldAgent()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    private void Finish()
    {
        if (!running)
            return;
        running = false;
        if (visual != null)
            visual.localPosition = new Vector3(visual.localPosition.x, baseY, visual.localPosition.z);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
        if (melee != null)
            melee.enabled = true;
        if (rig != null)
            rig.Squash(0.85f, 0.1f);
    }

    private void OnDestroy()
    {
        if (visual != null)
            visual.DOKill();
    }

    private static void SpawnDust(Vector3 position)
    {
        var go = new GameObject("Spawn Dust");
        go.transform.position = position + Vector3.up * 0.1f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startColor = new Color(0.45f, 0.4f, 0.34f, 0.8f);
        main.gravityModifier = -0.05f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);

        ps.Play();
    }
}
