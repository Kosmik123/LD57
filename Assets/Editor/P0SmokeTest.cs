using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Test dymny pętli P0 uruchamiany w trybie batch:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod P0SmokeTest.Run -logFile smoke.log
/// (bez -quit: skrypt sam kończy edytor kodem 0 = OK, 1 = błąd). Wynik w Logs/p0-smoke.txt.
/// Scenariusz: poziom 1 załadowany → strefa 1 spawnuje → slime rani gracza → zabicie fali daje serce i XP
/// → strefa 2 → level up z kartami → wybór karty → krata otwarta → poziom 2 → śmierć → ekran śmierci.
/// Z menu: LD57 > Smoke test P0 (wynik w konsoli, edytor zostaje).
/// </summary>
public static partial class P0SmokeTest
{
    const string ScenarioKey = "ld57.smoke.scenario";
    const string ScenePath = "Assets/Scenes/Michal Scene/Michal.unity";
    const string FlagKey = "ld57.smoke.active";
    const string BatchKey = "ld57.smoke.batch";
    static string ResultPath => Path.Combine(Directory.GetCurrentDirectory(), "Logs", SessionState.GetString(ScenarioKey, "p0") + "-smoke.txt");

    static readonly List<string> log = new List<string>();
    static int failures;
    static int exceptions;
    static List<Step> steps;
    static int stepIndex;
    static double stepStart;
    static bool driving;

    class Step
    {
        public string Name;
        public double Wait;                 // sekundy czasu rzeczywistego przed sprawdzeniem
        public Action Act;                  // akcja na początku kroku
        public Func<string> Check;          // null = OK, inaczej opis błędu
    }

    [MenuItem("LD57/Smoke test P0")]
    public static void RunFromMenu()
    {
        SessionState.SetBool(BatchKey, false);
        SessionState.SetString(ScenarioKey, "p0");
        Start();
    }

    public static void Run()
    {
        SessionState.SetBool(BatchKey, true);
        SessionState.SetString(ScenarioKey, "p0");
        Start();
    }

    /// <summary>Pełny run: 8 poziomów, wszystkie strefy, boss, wygrana. Batch: -executeMethod P0SmokeTest.RunFull</summary>
    public static void RunFull()
    {
        SessionState.SetBool(BatchKey, true);
        SessionState.SetString(ScenarioKey, "full");
        Start();
    }

    [MenuItem("LD57/Smoke test FULL (8 poziomów + boss)")]
    public static void RunFullFromMenu()
    {
        SessionState.SetBool(BatchKey, false);
        SessionState.SetString(ScenarioKey, "full");
        Start();
    }

    static void Start()
    {
        SessionState.SetBool(FlagKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (!SessionState.GetBool(FlagKey, false)) return;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying)
            BeginDriving();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            BeginDriving();
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FlagKey, false))
        {
            SessionState.SetBool(FlagKey, false);
            if (SessionState.GetBool(BatchKey, false))
                EditorApplication.Exit(failures == 0 && exceptions == 0 ? 0 : 1);
        }
    }

    static void BeginDriving()
    {
        if (driving) return;
        driving = true;
        log.Clear();
        failures = 0;
        exceptions = 0;
        Application.logMessageReceived += OnLog;
        // Test gra w grę przez kilka minut: wycisz audio, żeby nie leciało na słuchawki.
        AudioListener.volume = 0f;
        WaveZone.InstantSpawnForTests = true; // bez strumienia: testy liczą i wybijają całą strefę naraz
        LevelPopulation.DisabledForTests = true; // bez wrogów krążących po poziomie: testy liczą tylko strefy
        BossEncounter.DisabledForTests = true; // boss do playtestów na poziomie 1 nie może kończyć runu w testach
        EliteDirector.DisabledForTests = true; // elity i skrzynie (ekran reliktów pauzuje) poza testami
        var scenario = SessionState.GetString(ScenarioKey, "p0");
        steps = scenario == "full" ? BuildFullSteps() : scenario == "combat" ? BuildCombatSteps() : BuildSteps();
        stepIndex = -1;
        NextStep();
        EditorApplication.update += Drive;
        Log("start, scena: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            exceptions++;
            Log($"[{type}] {condition}");
        }
    }

    static void Log(string msg)
    {
        log.Add(msg);
        Debug.Log("[P0Smoke] " + msg);
    }

    static void NextStep()
    {
        stepIndex++;
        stepStart = EditorApplication.timeSinceStartup;
        if (stepIndex >= steps.Count) return;
        try
        {
            steps[stepIndex].Act?.Invoke();
        }
        catch (Exception e)
        {
            failures++;
            var trace = string.Join(" | ", (e.StackTrace ?? string.Empty).Split((char)10).Take(8).Select(l => l.Trim()));
            Log("FAIL " + steps[stepIndex].Name + ": wyjątek w akcji: " + e.Message + " @ " + trace);
        }
    }

    static void Drive()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish();
            return;
        }
        if (stepIndex >= steps.Count)
        {
            Finish();
            return;
        }
        var step = steps[stepIndex];
        if (EditorApplication.timeSinceStartup - stepStart < step.Wait) return;

        string error = null;
        try { error = step.Check?.Invoke(); }
        catch (Exception e) { error = "wyjątek: " + e.Message; }

        if (error == null) Log($"OK   {step.Name}");
        else { failures++; Log($"FAIL {step.Name}: {error}"); }
        NextStep();
    }

    static void Finish()
    {
        EditorApplication.update -= Drive;
        Application.logMessageReceived -= OnLog;
        driving = false;
        var summary = $"WYNIK: {(failures == 0 && exceptions == 0 ? "PASS" : "FAIL")} (kroki z błędem: {failures}, błędy/wyjątki w logu: {exceptions})";
        log.Add(summary);
        Debug.Log("[P0Smoke] " + summary);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllLines(ResultPath, log);
        }
        catch (Exception e) { Debug.LogWarning("[P0Smoke] nie zapisano wyniku: " + e.Message); }

        AudioListener.volume = 1f;
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    // ---------------------------------------------------------------- scenariusz

    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static T[] FindAll<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    static GameObject Player => GameObject.FindGameObjectWithTag("Player");
    /// <summary>Wszystkie poziomy stoją w scenie naraz: strefy, krata, wejście i tracker szukane w bieżącej instancji.</summary>
    static GameObject CurrentLevelRoot => Find<LevelManager>() != null ? Find<LevelManager>().CurrentInstance : null;
    static WaveZone[] Zones => (CurrentLevelRoot != null ? CurrentLevelRoot.GetComponentsInChildren<WaveZone>(true) : FindAll<WaveZone>()).OrderBy(z => z.name).ToArray();
    static LevelEnemyTracker CurrentTracker() => CurrentLevelRoot != null ? CurrentLevelRoot.GetComponentInChildren<LevelEnemyTracker>(true) : Find<LevelEnemyTracker>();
    static ExitGate CurrentGate() => CurrentLevelRoot != null ? CurrentLevelRoot.GetComponentInChildren<ExitGate>(true) : Find<ExitGate>();
    static LevelEntry CurrentEntry() => CurrentLevelRoot != null ? CurrentLevelRoot.GetComponentInChildren<LevelEntry>(true) : Find<LevelEntry>();

    static void Teleport(Vector3 pos)
    {
        var rb = Player.GetComponent<Rigidbody>();
        rb.position = pos;
        rb.transform.position = pos;
        rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    static List<Step> BuildSteps()
    {
        int xpBefore = 0;
        int hpBefore = 0;
        bool invulnerabilityHeld = false;
        Vector3 exitPos = Vector3.zero;

        return new List<Step>
        {
            new Step { Name = "pozycja gracza po 0,3 s", Wait = 0.3, Check = () => { Log("gracz@0.3s: " + Player.transform.position + " lvl=" + (Find<LevelManager>()?.CurrentLevel ?? -1) + " instancje poziomu: " + FindAll<LevelEnemyTracker>().Length); return null; } },
            new Step { Name = "diagnostyka sceny", Wait = 1.2, Check = () =>
            {
                Log("gracz: " + Player.transform.position + " komponenty: " + string.Join(",", Player.GetComponents<Component>().Select(c => c.GetType().Name)));
                foreach (var t in FindAll<LevelEnemyTracker>()) Log($"tracker {HPath(t.transform)} lvl={t.LevelNumber} total={t.TotalEnemies} scale={t.transform.lossyScale}");
                foreach (var e in FindAll<LevelEntry>()) Log($"entry {HPath(e.transform)} pos={e.transform.position}");
                foreach (var z in FindAll<WaveZone>()) Log($"zone {HPath(z.transform)} pos={z.transform.position} scale={z.transform.lossyScale} triggered={z.IsTriggered} total={z.TotalEnemyCount} box={z.GetComponent<BoxCollider>()?.bounds}");
                foreach (var g in FindAll<ExitGate>()) Log($"gate {HPath(g.transform)} pos={g.transform.position} open={g.IsOpen}");
                Log("Experience: " + FindAll<Experience>().Length + ", SkillSlots: " + FindAll<SkillSlots>().Length + ", LevelManager: " + FindAll<LevelManager>().Length + ", GameManager: " + FindAll<GameManager>().Length);
                var lm = Find<LevelManager>();
                if (lm != null) Log("LevelManager.CurrentLevel=" + lm.CurrentLevel);
                foreach (var e in FindAll<EnemyHealth>()) Log($"enemy {HPath(e.transform)} pos={e.transform.position}");
                var entryT = CurrentEntry()?.transform;
                if (entryT != null)
                {
                    var origin = entryT.position + Vector3.up * 5f;
                    if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                        Log($"raycast z Level Entry: trafia {HPath(hit.collider.transform)} w {hit.point} (layer {hit.collider.gameObject.layer})");
                    else
                        Log("raycast z Level Entry: NIC pod wejściem w promieniu 100 m");
                }
                foreach (var mc in FindAll<MeshCollider>())
                    Log($"meshcollider {HPath(mc.transform)} enabled={mc.enabled} mesh={(mc.sharedMesh ? mc.sharedMesh.name : "NULL")} readable={(mc.sharedMesh ? mc.sharedMesh.isReadable.ToString() : "-")} verts={(mc.sharedMesh ? mc.sharedMesh.vertexCount : 0)} meshBounds={(mc.sharedMesh ? mc.sharedMesh.bounds.ToString() : "-")} bounds={mc.bounds} layer={mc.gameObject.layer}");
                // Sonda podłogi: siatka raycastów z y=60 w dół, bez warstwy gracza (6)
                var floor = new List<string>();
                int mask = ~(1 << 6);
                for (int x = -200; x <= 200; x += 20)
                    for (int z = -200; z <= 200; z += 20)
                        if (Physics.Raycast(new Vector3(x, 60, z), Vector3.down, out var fh, 200f, mask, QueryTriggerInteraction.Ignore))
                            floor.Add($"({x},{fh.point.y:F1},{z})");
                Log($"sonda podłogi, trafienia {floor.Count}, x>=40: " + string.Join(" ", floor.Where(f => int.Parse(f.Substring(1, f.IndexOf(',') - 1)) >= 40)));
                var entryPos = CurrentEntry().transform.position;
                bool prevBack = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                string back = Physics.Raycast(entryPos + Vector3.up * 5f, Vector3.down, out var bh, 100f, mask, QueryTriggerInteraction.Ignore) ? $"{HPath(bh.collider.transform)} y={bh.point.y:F2} normal={bh.normal}" : "nic";
                Physics.queriesHitBackfaces = prevBack;
                Log("raycast w dół z backfaces: " + back);
                string up = Physics.Raycast(entryPos + Vector3.down * 2f, Vector3.up, out var uh, 100f, mask, QueryTriggerInteraction.Ignore) ? $"{HPath(uh.collider.transform)} y={uh.point.y:F2} normal={uh.normal}" : "nic";
                Log("raycast w górę spod wejścia: " + up);
                var around = Physics.OverlapSphere(entryPos, 3f, mask, QueryTriggerInteraction.Ignore);
                Log("collidery w promieniu 3 m od wejścia: " + string.Join(", ", around.Select(c => HPath(c.transform))));
                var lvlCollider = FindAll<MeshCollider>().FirstOrDefault(c => c.sharedMesh != null);
                if (lvlCollider != null)
                {
                    try
                    {
                        var mesh = lvlCollider.sharedMesh;
                        var verts = mesh.vertices;
                        var localEntry = lvlCollider.transform.InverseTransformPoint(entryPos);
                        var near = verts.Where(v => Vector3.Distance(new Vector3(v.x, 0, v.z), new Vector3(localEntry.x, 0, localEntry.z)) < 6f).ToList();
                        Log($"wierzchołki siatki w 6 m (lokalnie) od wejścia {localEntry}: {near.Count}; y: " + string.Join(" ", near.Take(30).Select(v => v.y.ToString("F1"))));
                        Log($"tris={mesh.triangles.Length / 3} bounds lokalne={mesh.bounds}");
                    }
                    catch (Exception ex) { Log("odczyt wierzchołków: " + ex.Message); }
                }
                var ec = FindAll<EnemyHealth>().FirstOrDefault();
                if (ec != null && Physics.Raycast(ec.transform.position + Vector3.up * 2, Vector3.down, out var eh, 50f, mask, QueryTriggerInteraction.Ignore))
                    Log($"pod slime'em {ec.transform.position}: {HPath(eh.collider.transform)} y={eh.point.y:F2}");
                else if (ec != null) Log($"pod slime'em {ec.transform.position}: nic");
                var rb = Player.GetComponent<Rigidbody>();
                Log($"rb kinematic={rb.isKinematic} detect={rb.detectCollisions} vel={rb.linearVelocity} gravity={Physics.gravity} ignore(6,0)={Physics.GetIgnoreLayerCollision(6, 0)} timeScale={Time.timeScale}");
                foreach (var c in Player.GetComponentsInChildren<Collider>()) Log($"player collider {HPath(c.transform)} enabled={c.enabled} trigger={c.isTrigger} layer={c.gameObject.layer}");
                return null;
            }},
            new Step { Name = "poziom 1 załadowany, gracz na Level Entry", Wait = 0.1, Check = () =>
            {
                var lm = Find<LevelManager>();
                if (lm == null) return "brak LevelManager";
                if (lm.CurrentLevel != 1) return "CurrentLevel = " + lm.CurrentLevel;
                var entry = CurrentEntry();
                if (entry == null) return "brak LevelEntry";
                float d = Vector3.Distance(Player.transform.position, entry.transform.position);
                if (d > 2f) return $"gracz {d:F1} m od Level Entry";
                var tracker = CurrentTracker();
                if (tracker == null) return "brak LevelEnemyTracker";
                int expectedTotal = Zones.Sum(z => z.TotalEnemyCount);
                if (tracker.TotalEnemies != expectedTotal) return $"TotalEnemies = {tracker.TotalEnemies}, strefy sumują {expectedTotal}";
                if (Zones.Length != 2) return "stref: " + Zones.Length;
                var gate = CurrentGate();
                if (gate == null) return "brak ExitGate";
                exitPos = gate.transform.position;
                if (FindAll<EnemyHealth>().Length != 0) return "wrogowie przed wejściem w strefę: " + FindAll<EnemyHealth>().Length;
                return null;
            }},
            new Step { Name = "strefa 1 spawnuje wszystkich swoich wrogów", Wait = 1.0,
                Act = () => Teleport(Zones[0].transform.position),
                Check = () =>
            {
                var zone = Zones[0];
                Log("gracz po teleporcie: " + Player.transform.position);
                foreach (var z in FindAll<WaveZone>()) Log($"zone {z.name} triggered={z.IsTriggered}");
                foreach (var e in FindAll<EnemyHealth>()) Log($"enemy {HPath(e.transform)} pos={e.transform.position} onNavMesh={e.GetComponent<UnityEngine.AI.NavMeshAgent>()?.isOnNavMesh}");
                if (!zone.IsTriggered) return "strefa nie odpaliła";
                var enemies = FindAll<EnemyHealth>();
                if (enemies.Length != zone.TotalEnemyCount) return $"wrogów: {enemies.Length}, strefa ma {zone.TotalEnemyCount}";
                foreach (var e in enemies)
                {
                    var agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    bool wallSpawner = e.GetComponent<SpiderController>() != null || e.GetComponent<SpiderMotherBoss>() != null; // pająk siedzi na ścianie
                    if (agent != null && !agent.isOnNavMesh && !wallSpawner) return $"{e.name} nie stoi na NavMeshu";
                    if (e.GetComponent<EnemyMeleeAttack>() == null) return $"{e.name} bez EnemyMeleeAttack";
                }
                return null;
            }},
            new Step { Name = "slime rani gracza (windup + cios)", Wait = 2.5,
                Act = () =>
                {
                    // horda 12 slime'ów (skok za 10) zabiłaby gracza w 2,5 s: atakuje tylko pierwszy
                    bool first = true;
                    foreach (var e in FindAll<EnemyHealth>()) { var m = e.GetComponent<EnemyMeleeAttack>(); if (m != null) { m.enabled = first; first = false; } }
                    Player.GetComponent<Health>().FullHeal();
                    var enemy = FindAll<EnemyHealth>()[0];
                    hpBefore = Player.GetComponent<Health>().CurrentHealth;
                    Teleport(enemy.transform.position + enemy.transform.forward * 0.9f);
                },
                Check = () =>
            {
                int hp = Player.GetComponent<Health>().CurrentHealth;
                if (hp >= hpBefore) return $"HP nie spadło ({hpBefore} → {hp})";
                return null;
            }},
            new Step { Name = "nietykalność blokuje obrażenia", Wait = 0.0,
                Act = () =>
                {
                    // Sprawdzenie natychmiast w akcji: slime obok może trafić w czasie oczekiwania.
                    var h = Player.GetComponent<Health>();
                    hpBefore = h.CurrentHealth;
                    h.IsInvulnerable = true;
                    h.ChangeHealth(-10);
                    invulnerabilityHeld = h.CurrentHealth == hpBefore;
                    // od tego miejsca gracz zostaje nietykalny: horda 12 slime'ów (skok za 10) zabiłaby go przed sercem
                    h.IsInvulnerable = true;
                },
                Check = () => invulnerabilityHeld ? null : "HP spadło mimo IsInvulnerable" },
            new Step { Name = "wybicie fali 1: serce + XP (level upy rozstrzygane)", Wait = 1.0,
                Act = () =>
                {
                    xpBefore = Find<Experience>().CurrentXp;
                    foreach (var e in FindAll<EnemyHealth>()) e.Health.ChangeHealth(-100);
                },
                Check = () =>
            {
                ResolveLevelUps(); // hordy: po pierwszej fali może już być level up (pauza), karty wybieramy od razu
                if (!Zones[0].IsCleared) return "strefa 1 nie wyczyszczona";
                if (Find<HeartPickup>() == null) return "brak serca";
                var xp = Find<Experience>();
                if (xp.CurrentXp == xpBefore && xp.HeroLevel == 0) return $"XP nie wzrosło ({xp.CurrentXp})";
                int alive = FindAll<EnemyHealth>().Count(e => !e.Health.IsDead);
                LogTimeState("po ResolveLevelUps w fali 1");
                if (alive != 0) return "żywi wrogowie po wybiciu: " + alive;
                return null;
            }},
            new Step { Name = "serce leczy", Wait = 0.5,
                Act = () =>
                {
                    var h = Player.GetComponent<Health>();
                    h.FullHeal();
                    h.IsInvulnerable = false;
                    h.ChangeHealth(-20);
                    h.IsInvulnerable = true;
                    hpBefore = h.CurrentHealth;
                    Teleport(Find<HeartPickup>().transform.position);
                },
                Check = () =>
            {
                var heart = Find<HeartPickup>();
                var screen = Find<LevelUpScreen>();
                LogTimeState("w kroku serca");
                Log($"serce: timeScale={Time.timeScale}, gracz {Player.transform.position}, serce {(heart ? heart.transform.position.ToString() : "brak")}, HP {Player.GetComponent<Health>().CurrentHealth}/{hpBefore}, ekran kart open={GetField(screen, "isOpen")} pending={GetField(screen, "pending")}, PlayerController enabled={Player.GetComponent<PlayerController>().enabled}, rb kinematic={Player.GetComponent<Rigidbody>().isKinematic}");
                return Player.GetComponent<Health>().CurrentHealth > hpBefore ? null : "HP nie wzrosło po sercu";
            }},
            new Step { Name = "strefa 2 + wybicie: level up otwiera 3 karty i pauzuje", Wait = 1.5,
                Act = () => Teleport(Zones[1].transform.position),
                Check = () =>
            {
                Log($"strefa 2: timeScale={Time.timeScale}, triggered={Zones[1].IsTriggered}, gracz {Player.transform.position}, strefa {Zones[1].transform.position}, box={Zones[1].GetComponent<BoxCollider>()?.bounds}");
                int aliveNow = FindAll<EnemyHealth>().Count(e => !e.Health.IsDead);
                if (aliveNow != Zones[1].TotalEnemyCount) return $"wrogów po wejściu w strefę 2: {aliveNow}, strefa ma {Zones[1].TotalEnemyCount}";
                var xpNow = Find<Experience>();
                xpNow.AddXp(Mathf.Max(0, xpNow.XpToNextLevel - xpNow.CurrentXp - 1)); // wybicie strefy ma na pewno dać level up
                foreach (var e in FindAll<EnemyHealth>()) e.Health.ChangeHealth(-100);
                return null;
            }},
            new Step { Name = "ekran kart otwarty", Wait = 0.5, Check = () =>
            {
                var xp = Find<Experience>();
                if (xp.HeroLevel < 1) return "HeroLevel = " + xp.HeroLevel;
                if (Time.timeScale != 0f) return "timeScale = " + Time.timeScale;
                var screen = Find<LevelUpScreen>();
                var root = (GameObject)GetField(screen, "root");
                if (root == null || !root.activeSelf) return "root ekranu kart nieaktywny";
                var offer = (System.Collections.IList)GetField(screen, "offer");
                if (offer.Count != 3) return "kart w ofercie: " + offer.Count;
                return null;
            }},
            new Step { Name = "wybór kart daje skill i wznawia grę", Wait = 0.3,
                Act = () => ResolveLevelUps(),
                Check = () =>
            {
                if (Time.timeScale != 1f) return "timeScale = " + Time.timeScale;
                var slots = Player.GetComponent<SkillSlots>();
                if (slots.Slots.Count + (slots.SpaceSkill != null ? 1 : 0) < 1) return "brak skilla po wyborze";
                return null;
            }},
            new Step { Name = "krata otwarta po wybiciu poziomu", Wait = 0.5, Check = () =>
            {
                var tracker = CurrentTracker();
                if (tracker.RemainingEnemies != 0) return "pozostali wrogowie: " + tracker.RemainingEnemies;
                var gate = CurrentGate();
                if (!gate.IsOpen) return "krata zamknięta";
                return null;
            }},
            new Step { Name = "przejście przez kratę ładuje poziom 2", Wait = 1.5,
                Act = () => Teleport(exitPos),
                Check = () =>
            {
                var lm = Find<LevelManager>();
                if (lm.CurrentLevel != 2) return "CurrentLevel = " + lm.CurrentLevel;
                if (CurrentTracker() == null || CurrentTracker().LevelNumber != 2) return "tracker poziomu 2 nie istnieje";
                // wszystkie poziomy stoją w scenie naraz: serce z poziomu 1 zostaje tam, gdzie było (nie jest błędem)
                return null;
            }},
            new Step { Name = "śmierć gracza: pauza i ekran śmierci", Wait = 0.5,
                Act = () => { var h = Player.GetComponent<Health>(); h.IsInvulnerable = false; h.ChangeHealth(-1000); },
                Check = () =>
            {
                var gm = Find<GameManager>();
                if (!gm.IsRunOver) return "IsRunOver = false";
                if (Time.timeScale != 0f) return "timeScale = " + Time.timeScale;
                var death = Find<DeathScreen>();
                var root = (GameObject)GetField(death, "root");
                if (root == null || !root.activeSelf) return "panel śmierci nieaktywny";
                return null;
            }},
            new Step { Name = "restart runu: scena przeładowana, poziom 1, pełne HP", Wait = 2.0,
                Act = () => Find<GameManager>().RestartRun(),
                Check = () =>
            {
                if (Time.timeScale != 1f) return "timeScale = " + Time.timeScale;
                var lm = Find<LevelManager>();
                if (lm == null || lm.CurrentLevel != 1) return "CurrentLevel = " + (lm?.CurrentLevel ?? -1);
                var gm = Find<GameManager>();
                if (gm == null || gm.IsRunOver) return "GameManager po restarcie: IsRunOver";
                var hp = Player.GetComponent<Health>();
                if (hp.CurrentHealth != hp.MaxHealth) return $"HP {hp.CurrentHealth}/{hp.MaxHealth}";
                if (Zones.Any(z => z.IsTriggered))
                {
                    foreach (var z in Zones.Where(z => z.IsTriggered))
                        Log($"po restarcie odpalona {HPath(z.transform)} pos={z.transform.position} box={z.GetComponent<BoxCollider>()?.bounds}, gracz {Player.transform.position}, wejście {CurrentEntry()?.transform.position}");
                    return "strefy odpalone po restarcie";
                }
                if (FindAll<EnemyHealth>().Length != 0) return "wrogowie po restarcie: " + FindAll<EnemyHealth>().Length;
                var xp = Find<Experience>();
                if (xp.HeroLevel != 0 || xp.CurrentXp != 0) return $"XP po restarcie: lvl {xp.HeroLevel}, xp {xp.CurrentXp}";
                return null;
            }},
        };
    }

    static string HPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }

    static void LogTimeState(string where)
    {
        var gm = Find<GameManager>(); var screen = Find<LevelUpScreen>(); var pause = Find<PauseMenu>(); var hs = Find<HitStop>(); var wd = Find<WaveDirector>();
        Log($"czas [{where}]: timeScale={Time.timeScale}, GM started={gm?.IsRunStarted} over={gm?.IsRunOver}, karty open={(screen ? GetField(screen, "isOpen") : "-")} pending={(screen ? GetField(screen, "pending") : "-")} busy={(screen ? GetField(screen, "busy") : "-")}, pauza={(pause ? GetField(pause, "paused") : "brak")}, hitstop running={(hs ? (GetField(hs, "running") != null).ToString() : "brak")}, waveDirector={(wd ? "jest" : "brak")} HeroLevel={Find<Experience>()?.HeroLevel}");
    }

    static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (f == null) throw new System.MissingFieldException(target.GetType().Name, name);
        f.SetValue(target, value);
    }

    static object GetField(object target, string name)
    {
        var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f?.GetValue(target);
    }

    static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        m.Invoke(target, args);
    }
}
