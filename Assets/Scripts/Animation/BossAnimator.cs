using DG.Tweening;
using UnityEngine;

/// <summary>
/// Boss Matka Pająk (P12): pozy per stan (OnStateChanged) i zdarzenia akcji (OnAction), animacja proceduralna przez VisualRig.
/// Kanały: ProcEuler (pochylenie/przechył ciała), ProcOffset (opuszczenie ciała), SetLegAngle (osobno każda noga), SetEyes, Pulse/Squash.
/// Ryk: ciało w tył, dwie przednie nogi w górę, oczy powiększone, puls. Kucanie: ciało w dół, puls odwłoka. Celowanie siecią: ciało
/// lekko w górę, nogi nieruchome. Tupnięcie: przednie nogi w górę i w dół ze squashem. Kulanie: ciało przechylone na stronę
/// zniszczonych nóg, krok sztywniejszy. Przewrócenie: ciało na bok, nogi zwinięte. Chód jak dotąd (sinus na parach nóg).
/// Zniszczone nogi są ukrywane przez bossa (SetActive(false)); animator tylko pomija je w pętli.
/// </summary>
[RequireComponent(typeof(VisualRig))]
public class BossAnimator : MonoBehaviour
{
    [Header("Chód")]
    [SerializeField] private float legAmplitude = 25f;
    [SerializeField] private float chargeLegBoost = 1.6f;
    [SerializeField] private float legFrequency = 6f;
    [SerializeField, Tooltip("Prędkość odpowiadająca pełnej amplitudzie kroku (m/s).")] private float walkReferenceSpeed = 3f;

    [Header("Ryk")]
    [SerializeField] private float roarPitch = -25f;
    [SerializeField] private float roarFrontLegs = -55f;
    [SerializeField] private float roarEyes = 1.6f;
    [SerializeField] private float roarPulse = 1.12f;
    [SerializeField] private float roarSeconds = 1.2f;

    [Header("Pozy akcji")]
    [SerializeField] private float crouchDepth = 0.3f;
    [SerializeField] private float crouchFold = 14f;
    [SerializeField] private float aimPitch = -8f;
    [SerializeField] private float stompLift = -60f;
    [SerializeField] private float stompDrop = 25f;
    [SerializeField] private float chargePitch = 8f;
    [SerializeField] private float stunDrop = 0.2f;

    [Header("Kulenie i przewrócenie")]
    [SerializeField] private float limpTiltPerLeg = 3f;
    [SerializeField] private float limpDropPerLeg = 0.05f;
    [SerializeField] private float collapseRoll = 60f;
    [SerializeField] private float collapseDrop = 0.9f;
    [SerializeField] private float collapseLegFold = -50f;

    [SerializeField] private float blendRate = 9f;

    private VisualRig rig;
    private SpiderMotherBoss boss;
    private SpiderMotherBoss.State state = SpiderMotherBoss.State.Walk;

    // cele pozy (wygładzane)
    private float tPitch, tRoll, tDrop, tFold, tLiftWeight;
    private float tLift;
    private bool legsPaused;
    private int frontMask; // bity nóg uniesionych (0 = brak)

    // wartości bieżące
    private float pitch, roll, drop, fold, lift, liftWeight, walkBlend, legPhase, clock;
    private float roarUntil, stompDropUntil;
    private Vector3 limpDir;
    private int lostLegs;
    private readonly bool[] deadLeg = new bool[VisualRig.MaxLegs];

    private const int Front2 = (1 << 1) | (1 << 7);
    private const int Front3 = (1 << 7) | (1 << 0) | (1 << 1);

    private void Awake()
    {
        rig = GetComponent<VisualRig>();
        boss = GetComponent<SpiderMotherBoss>();
        rig.EnsureInit();
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.OnStateChanged += StateChanged;
        boss.OnAction += ActionHappened;
        boss.OnLegDestroyed += LegDestroyed;
    }

    private void OnDisable()
    {
        if (boss != null)
        {
            boss.OnStateChanged -= StateChanged;
            boss.OnAction -= ActionHappened;
            boss.OnLegDestroyed -= LegDestroyed;
        }
        rig.ProcEuler = Vector3.zero;
        rig.ProcOffset = Vector3.zero;
    }

    // ------------------------------------------------------------ zdarzenia

    private void StateChanged(string newState)
    {
        if (!System.Enum.TryParse(newState, out SpiderMotherBoss.State parsed))
            return; // "Phase2"/"Phase3": ryk idzie osobno przez OnAction("Roar")
        state = parsed;

        tPitch = 0f; tRoll = 0f; tDrop = 0f; tFold = 0f; legsPaused = false; frontMask = 0; tLift = 0f; tLiftWeight = 0f;
        float eyes = 1f;
        switch (parsed)
        {
            case SpiderMotherBoss.State.Intro:
                StartRoar();
                break;
            case SpiderMotherBoss.State.LayEggs:
                tDrop = crouchDepth;
                tFold = crouchFold;
                rig.Pulse(1.14f, 3, 0.55f);
                eyes = 0.9f;
                break;
            case SpiderMotherBoss.State.WebShot:
                tPitch = aimPitch;
                legsPaused = true;
                eyes = 1.3f;
                break;
            case SpiderMotherBoss.State.Stomp:
                tPitch = -10f;
                frontMask = Front3;
                tLift = stompLift;
                tLiftWeight = 1f;
                break;
            case SpiderMotherBoss.State.Charge:
                tPitch = roarPitch * 0.6f;
                frontMask = Front2;
                tLift = roarFrontLegs;
                tLiftWeight = 1f;
                eyes = 1.4f;
                break;
            case SpiderMotherBoss.State.Stun:
                tDrop = stunDrop;
                tFold = 8f;
                legsPaused = true;
                eyes = 0.5f;
                break;
            case SpiderMotherBoss.State.Collapse:
                tRoll = collapseRoll;
                tDrop = collapseDrop;
                tFold = collapseLegFold;
                legsPaused = true;
                eyes = 0.15f;
                rig.Squash(0.6f, 0.25f, 0.9f, Ease.OutBounce);
                break;
        }
        rig.SetEyes(eyes, 0.25f);
    }

    private void ActionHappened(string action)
    {
        switch (action)
        {
            case "Roar":
                StartRoar();
                break;
            case "ChargeStart":
                // Szarża: ryk się kończy, ciało w przód, nogi biegną.
                roarUntil = 0f;
                frontMask = 0;
                tLiftWeight = 0f;
                tPitch = chargePitch;
                legsPaused = false;
                rig.SetEyes(1.2f, 0.15f);
                break;
            case "StompImpact":
                stompDropUntil = Time.time + 0.25f;
                tPitch = 6f;
                rig.Squash(0.7f, 0.05f, 0.4f, Ease.OutElastic);
                rig.Kick(new Vector3(0f, -0.25f, 0.2f), 0.05f, 0.35f, Ease.OutBack);
                break;
            case "WebFire":
                rig.Kick(new Vector3(0f, 0.05f, -0.35f), 0.05f, 0.3f, Ease.OutBack);
                break;
            case "EggLaid":
                rig.Pulse(1.2f, 1, 0.3f);
                break;
            case "LegBroken":
                rig.Squash(0.86f, 0.04f, 0.4f, Ease.OutElastic);
                rig.Kick(new Vector3(0f, -0.2f, 0f), 0.04f, 0.3f, Ease.OutBack);
                break;
        }
    }

    private void StartRoar()
    {
        roarUntil = Time.time + roarSeconds;
        rig.Pulse(roarPulse, 3, 0.4f);
        rig.SetEyes(roarEyes, 0.2f);
    }

    private void LegDestroyed(BossLeg leg)
    {
        int i = leg.Index;
        if (i >= 0 && i < deadLeg.Length) deadLeg[i] = true;
        // Kierunek zniszczonej strony: suma kierunków martwych nóg (nogi stoją co 45 stopni wokół osi Y, Leg_0 do przodu).
        limpDir = Vector3.zero;
        lostLegs = 0;
        for (int k = 0; k < deadLeg.Length; k++)
        {
            if (!deadLeg[k]) continue;
            lostLegs++;
            float a = k * 45f * Mathf.Deg2Rad;
            limpDir += new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
        if (limpDir.sqrMagnitude > 0.01f) limpDir.Normalize();
    }

    // ------------------------------------------------------------ klatka

    private void Update()
    {
        if (!rig.HasVisual || boss == null) return;
        float dt = Time.deltaTime;
        clock += dt;
        float k = 1f - Mathf.Exp(-blendRate * dt);

        bool roaring = Time.time < roarUntil && state != SpiderMotherBoss.State.Collapse;
        bool stompDropping = Time.time < stompDropUntil;

        // cele zależne od ryku/uderzenia nadpisują poza stanu
        float goalPitch = tPitch, goalDrop = tDrop, goalFold = tFold, goalLift = tLift, goalWeight = tLiftWeight;
        int mask = frontMask;
        if (roaring)
        {
            goalPitch = roarPitch;
            mask = Front2;
            goalLift = roarFrontLegs;
            goalWeight = 1f;
        }
        if (stompDropping)
        {
            goalLift = stompDrop;
            goalWeight = 1f;
        }

        // kulenie: przechył na stronę zniszczonych nóg i obniżenie ciała (nie podczas przewracania)
        float limp = state == SpiderMotherBoss.State.Collapse ? 0f : Mathf.Min(lostLegs, 5);
        Vector3 tilt = Vector3.zero;
        if (limp > 0f)
        {
            float amount = limpTiltPerLeg * limp;
            // obrót wokół osi (vz, 0, -vx) opuszcza stronę v (rozpisane jako kąty Eulera ciała)
            tilt = new Vector3(amount * limpDir.z, 0f, -amount * limpDir.x);
            goalDrop += limpDropPerLeg * limp;
        }

        float sway = 0f;
        if (state == SpiderMotherBoss.State.Stun)
            sway = 5f * Mathf.Sin(clock * 9f);

        pitch = Mathf.Lerp(pitch, goalPitch, k);
        roll = Mathf.Lerp(roll, tRoll, k * (state == SpiderMotherBoss.State.Collapse ? 0.35f : 1f));
        drop = Mathf.Lerp(drop, goalDrop, k * (state == SpiderMotherBoss.State.Collapse ? 0.4f : 1f));
        fold = Mathf.Lerp(fold, goalFold, k);
        lift = Mathf.Lerp(lift, goalLift, k * (stompDropping ? 2.5f : 1f));
        liftWeight = Mathf.Lerp(liftWeight, goalWeight, k);

        // limp bob: nierówny krok, im mniej nóg tym mocniej
        float bob = 0f;
        if (limp > 0f && boss.MoveSpeed > 0.3f)
            bob = -0.04f * limp * Mathf.Abs(Mathf.Sin(clock * 5f));

        rig.ProcEuler = new Vector3(pitch + tilt.x, 0f, roll + tilt.z + sway);
        rig.ProcOffset = new Vector3(0f, -drop + bob, 0f);

        // nogi
        float speedK = Mathf.Clamp(boss.MoveSpeed / Mathf.Max(0.1f, walkReferenceSpeed), 0f, 2f);
        bool walking = !legsPaused && boss.MoveSpeed > 0.3f;
        walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt * 6f);
        legPhase += Mathf.PI * 2f * legFrequency * speedK * dt * (limp > 0f ? 0.85f : 1f);
        float amp = legAmplitude * (state == SpiderMotherBoss.State.Charge ? chargeLegBoost : 1f) * Mathf.Min(speedK, 1.5f) * walkBlend;
        for (int i = 0; i < VisualRig.MaxLegs; i++)
        {
            if (deadLeg[i]) continue;
            float angle = fold + amp * Mathf.Sin(legPhase + ((i & 1) == 0 ? 0f : Mathf.PI));
            if ((mask & (1 << i)) != 0)
                angle = Mathf.Lerp(angle, lift, liftWeight);
            else if (limp > 0f && NearDead(i))
                angle += 8f * limp; // nogi po zniszczonej stronie krótsze/niżej: kulejąca podpora
            rig.SetLegAngle(i, angle);
        }
    }

    private bool NearDead(int i)
    {
        return deadLeg[(i + 1) & 7] || deadLeg[(i + 7) & 7];
    }
}
