using DG.Tweening;
using UnityEngine;

/// <summary>
/// Feel gracza: pył kroków, lądowanie, dash (smugi, stretch), cios (wypad Body, wiatr), krople przy niskim HP.
/// Cząsteczki i materiały budowane w kodzie, po jednym ParticleSystemie na rodzaj (World space).
/// Body animowane przez DOTween (własne tweeny, resetowane do bazy przed nowym).
/// </summary>
public class PlayerFeel : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Rigidbody body3d;
    [SerializeField] private Health health;
    [SerializeField, Tooltip("Dziecko roota z modelem (to samo co w PlayerSkillAnimator).")]
    private Transform body;
    [SerializeField] private PlayerSkillAnimator skillAnimator;

    [SerializeField] private float runSpeedThreshold = 2f;
    [SerializeField] private float stepInterval = 0.22f;
    [SerializeField, Range(0f, 1f)] private float lowHealthFraction = 0.3f;
    [SerializeField] private float lowHealthInterval = 0.8f;

    private static readonly Color DustColor = new Color(0.62f, 0.58f, 0.5f, 0.7f);
    private static readonly Color DustLight = new Color(0.75f, 0.71f, 0.62f, 0.7f);

    private GameObject fxRoot;
    private ParticleSystem dust, streaks, drops;
    private Vector3 baseScale, basePos;
    private Tween scaleTween, posTween;
    private float stepTimer, lowHealthTimer, runTime, minFallSpeed;
    private bool wasRunning, lowHealth;
    private Vector3 lastRunDir = Vector3.forward;
    private Vector3 lastDashDir = Vector3.forward;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (body3d == null) body3d = GetComponent<Rigidbody>();
        if (health == null) health = GetComponent<Health>();
        if (body == null) body = transform.Find("Body");
        if (skillAnimator == null) skillAnimator = GetComponent<PlayerSkillAnimator>();
        if (body != null) { baseScale = body.localScale; basePos = body.localPosition; }

        fxRoot = new GameObject("Player Feel FX");
        dust = MakeSystem("Dust", ParticleSystemRenderMode.Billboard, 200, 0f, true);
        streaks = MakeSystem("Speed Streaks", ParticleSystemRenderMode.Stretch, 100, 0f, false);
        var r = streaks.GetComponent<ParticleSystemRenderer>();
        r.lengthScale = 6f; r.velocityScale = 0.05f;
        drops = MakeSystem("Blood Drops", ParticleSystemRenderMode.Billboard, 40, 2.5f, false);
    }

    private ParticleSystem MakeSystem(string name, ParticleSystemRenderMode mode, int max, float gravity, bool grow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(fxRoot.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.startSpeed = 0f; main.startLifetime = 0.4f; main.startSize = 0.2f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f) : AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = SwordTrail.CreateTransparentMaterial("PlayerFeel " + name + " (runtime)", Color.white);
        psr.renderMode = mode;
        return ps;
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnAttack += Player_OnAttack;
            player.OnDashed += Player_OnDashed;
            player.OnDashEnded += Player_OnDashEnded;
            player.OnLanded += Player_OnLanded;
        }
        if (health != null)
        {
            health.OnHealthChanged += Health_OnHealthChanged;
            lowHealth = IsLow();
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnAttack -= Player_OnAttack;
            player.OnDashed -= Player_OnDashed;
            player.OnDashEnded -= Player_OnDashEnded;
            player.OnLanded -= Player_OnLanded;
        }
        if (health != null) health.OnHealthChanged -= Health_OnHealthChanged;
        KillTweens();
        ResetBody();
    }

    private void OnDestroy()
    {
        if (fxRoot != null) Destroy(fxRoot);
    }

    private void KillTweens()
    {
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        if (posTween != null && posTween.IsActive()) posTween.Kill();
        scaleTween = posTween = null;
    }

    private void ResetBody()
    {
        if (body == null) return;
        body.localScale = baseScale;
        body.localPosition = basePos;
    }

    private bool IsLow() => health != null && !health.IsDead && health.CurrentHealth < lowHealthFraction * health.MaxHealth;

    private void Health_OnHealthChanged(int delta)
    {
        lowHealth = IsLow();
        if (!lowHealth) lowHealthTimer = 0f;
    }

    // ---------- Update: kroki, spadanie, krople ----------

    private void Update()
    {
        if (player == null || body3d == null) return;
        var v = body3d.linearVelocity;
        if (!player.IsGrounded) minFallSpeed = Mathf.Min(minFallSpeed, v.y);

        var flat = new Vector3(v.x, 0f, v.z);
        float speed = flat.magnitude;
        bool running = player.IsGrounded && !player.IsDashing && speed > runSpeedThreshold;
        if (running)
        {
            lastRunDir = flat / speed;
            runTime += Time.deltaTime;
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = stepInterval;
                Puff(FeetPos(), -lastRunDir, Random.Range(3, 5), 0.2f, 0.9f);
            }
        }
        else
        {
            if (wasRunning && runTime > 0.3f && player.IsGrounded && !player.IsDashing && speed < 1f)
                Puff(FeetPos(), lastRunDir, 1, 0.45f, 1.6f); // ślizgnięcie
            runTime = 0f;
            stepTimer = 0f;
        }
        wasRunning = running;

        if (lowHealth && health != null && !health.IsDead)
        {
            lowHealthTimer -= Time.deltaTime;
            if (lowHealthTimer <= 0f)
            {
                lowHealthTimer = lowHealthInterval;
                for (int i = 0; i < 2; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.position + Vector3.up * Random.Range(0.8f, 1.2f) + Random.insideUnitSphere * 0.15f,
                        velocity = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f)),
                        startColor = new Color(0.75f, 0.05f, 0.05f, 1f),
                        startSize = Random.Range(0.06f, 0.1f),
                        startLifetime = 0.6f
                    };
                    drops.Emit(ep, 1);
                }
            }
        }
    }

    private Vector3 FeetPos() => transform.position + Vector3.up * 0.1f;

    /// <summary>Obłoczek pyłu: count cząstek, lekko w górę i w kierunku dir (zwykle do tyłu).</summary>
    private void Puff(Vector3 pos, Vector3 dir, int count, float size, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            var v = dir * Random.Range(0.3f, 1f) * speed + Vector3.up * Random.Range(0.4f, 1f) + Random.insideUnitSphere * 0.25f;
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = pos + Random.insideUnitSphere * 0.1f,
                velocity = v,
                startColor = Color.Lerp(DustColor, DustLight, Random.value),
                startSize = size * Random.Range(0.8f, 1.3f),
                startLifetime = Random.Range(0.3f, 0.45f)
            }, 1);
        }
    }

    // ---------- Lądowanie ----------

    private void Player_OnLanded()
    {
        float fall = Mathf.Abs(minFallSpeed);
        minFallSpeed = 0f;
        float t = fall > 0.5f ? Mathf.Clamp01(fall / 14f) : 0.4f; // brak danych o spadaniu = stała siła
        int count = Mathf.RoundToInt(Mathf.Lerp(8f, 12f, t));
        for (int i = 0; i < count; i++)
        {
            float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = FeetPos() + d * 0.2f,
                velocity = d * Random.Range(1.8f, 3.2f) + Vector3.up * 0.4f,
                startColor = Color.Lerp(DustColor, DustLight, Random.value),
                startSize = Random.Range(0.22f, 0.34f),
                startLifetime = Random.Range(0.35f, 0.5f)
            }, 1);
        }
        // Przy skillu GroundSlam squash robi PlayerSkillAnimator.
        if (body == null || (skillAnimator != null && skillAnimator.SlamPending)) return;
        float y = Mathf.Lerp(0.92f, 0.8f, t);
        float xz = 1f + (1f - y) * 0.6f;
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        body.localScale = Vector3.Scale(baseScale, new Vector3(xz, y, xz));
        scaleTween = body.DOScale(baseScale, 0.3f).SetEase(Ease.OutElastic);
    }

    // ---------- Dash ----------

    private void Player_OnDashed()
    {
        var dir = transform.forward; dir.y = 0f;
        if (body3d != null)
        {
            var v = body3d.linearVelocity; v.y = 0f;
            if (v.sqrMagnitude > 0.01f) dir = v.normalized;
        }
        dir.Normalize();
        lastDashDir = dir;
        Puff(FeetPos(), -dir, 6, 0.3f, 3.5f);
        Streaks(dir, Random.Range(6, 9));

        if (body == null) return;
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        var ld = body.InverseTransformDirection(dir);
        var s = new Vector3(StretchAxis(ld.x), StretchAxis(ld.y), StretchAxis(ld.z));
        scaleTween = body.DOScale(Vector3.Scale(baseScale, s), 0.05f).SetEase(Ease.OutQuad);
    }

    private static float StretchAxis(float c) => Mathf.Lerp(0.9f, 1.15f, Mathf.Abs(c));

    private void Player_OnDashEnded()
    {
        Puff(FeetPos(), lastDashDir, 4, 0.28f, 2f);
        if (body == null) return;
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        scaleTween = body.DOScale(baseScale, 0.25f).SetEase(Ease.OutBack);
    }

    /// <summary>Smugi prędkości wzdłuż dir (biało, alfa 0,5, 0,15 s). Wołane przy dashu i szarży.</summary>
    public void Streaks(Vector3 dir, int count)
    {
        if (streaks == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();
        var side = Vector3.Cross(Vector3.up, dir);
        for (int i = 0; i < count; i++)
        {
            var pos = transform.position + Vector3.up * Random.Range(0.2f, 1.4f)
                      + side * Random.Range(-0.5f, 0.5f) - dir * Random.Range(0f, 0.8f);
            streaks.Emit(new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = -dir * Random.Range(2f, 5f),
                startColor = new Color(1f, 1f, 1f, 0.5f),
                startSize = Random.Range(0.03f, 0.06f),
                startLifetime = 0.15f
            }, 1);
        }
    }

    // ---------- Cios ----------

    /// <summary>Gest rzucania czaru (klasy dystansowe): Body lekko w tył 0,05 s, do przodu 0,1 s (mniejszy niż cios), powrót.</summary>
    private void CastGesture()
    {
        if (body == null) return;
        var fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        if (posTween != null && posTween.IsActive()) posTween.Kill();
        body.localPosition = basePos;
        var parent = body.parent != null ? body.parent : transform;
        var ld = parent.InverseTransformDirection(fwd);
        ld.y = 0f; ld.Normalize();
        var seq = DOTween.Sequence();
        seq.Append(body.DOLocalMove(basePos - ld * 0.04f, 0.05f).SetEase(Ease.OutQuad));
        seq.Append(body.DOLocalMove(basePos + ld * 0.06f, 0.1f).SetEase(Ease.OutQuad));
        seq.Append(body.DOLocalMove(basePos, 0.15f).SetEase(Ease.InOutQuad));
        posTween = seq;
    }

    private void Player_OnAttack()
    {
        if (player != null && !player.MeleeHitboxes) { CastGesture(); return; } // mag: bez wypadu ciosu i wiatru ostrza
        var fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        if (body != null)
        {
            if (posTween != null && posTween.IsActive()) posTween.Kill();
            body.localPosition = basePos;
            var parent = body.parent != null ? body.parent : transform;
            var ld = parent.InverseTransformDirection(fwd);
            ld.y = 0f; ld.Normalize();
            var seq = DOTween.Sequence();
            seq.Append(body.DOLocalMove(basePos - ld * 0.06f, 0.05f).SetEase(Ease.OutQuad));
            seq.Append(body.DOLocalMove(basePos + ld * 0.12f, 0.08f).SetEase(Ease.OutQuad));
            seq.Append(body.DOLocalMove(basePos, 0.2f).SetEase(Ease.InOutQuad));
            posTween = seq;
        }

        // Wiatr ciosu: smugi w łuku przed graczem, styczne do łuku.
        int n = Random.Range(4, 7);
        var origin = transform.position + Vector3.up * 0.9f;
        for (int i = 0; i < n; i++)
        {
            float ang = Mathf.Lerp(-55f, 55f, (i + Random.value * 0.5f) / n);
            var radial = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
            var tangent = Vector3.Cross(Vector3.up, radial);
            streaks.Emit(new ParticleSystem.EmitParams
            {
                position = origin + radial * Random.Range(0.9f, 1.3f) + Vector3.up * Random.Range(-0.2f, 0.2f),
                velocity = tangent * 5f,
                startColor = new Color(1f, 1f, 1f, 0.6f),
                startSize = Random.Range(0.04f, 0.07f),
                startLifetime = 0.12f
            }, 1);
        }
    }
}
