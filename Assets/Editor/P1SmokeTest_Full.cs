using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scenariusz „full”: przejście 8 poziomów przez wszystkie strefy (wrogowie zabijani programowo),
/// karty wybierane automatycznie, boss na poziomie 8, wygrana. Uruchomienie: P0SmokeTest.RunFull
/// (bash Tools/unity-batch.sh smokefull) albo menu LD57 > Smoke test FULL.
/// </summary>
public static partial class P0SmokeTest
{
    static readonly int[] ZonesPerLevel = { 0, 2, 2, 3, 3, 3, 3, 3, 3 };

    static void ResolveLevelUps()
    {
        var screen = Find<LevelUpScreen>();
        if (screen == null) return;
        for (int guard = 0; guard < 20 && Time.timeScale == 0f && (bool)GetField(screen, "isOpen"); guard++)
        {
            SetField(screen, "acceptAfter", 0f); // karencja na klik (inputGrace) nie dotyczy testu
            Call(screen, "Choose", 0);
            // Wybór karty ma animację 0,15 s (czas rzeczywisty) i dopiero po niej Close/RefreshOffer: w teście dokańczamy ją od razu.
            var tweens = GetField(screen, "cardTweens") as DG.Tweening.Sequence[];
            if (tweens != null && tweens[0] != null && tweens[0].IsActive()) tweens[0].Complete(true);
        }
    }

    static string CheckEnemiesPlaced()
    {
        foreach (var e in FindAll<EnemyHealth>())
        {
            var agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool special = e.GetComponent<SpiderController>() != null || e.GetComponent<SpiderMotherBoss>() != null;
            if (agent != null && agent.enabled && !agent.isOnNavMesh && !special)
            {
                bool near = UnityEngine.AI.NavMesh.SamplePosition(e.transform.position, out var nh, 5f, UnityEngine.AI.NavMesh.AllAreas);
                var rb = e.GetComponent<Rigidbody>();
                return $"{e.name} nie stoi na NavMeshu ({e.transform.position}); najbliższy punkt NavMesha {(near ? nh.position.ToString() + " dist " + Vector3.Distance(nh.position, e.transform.position).ToString("F2") : "brak w 5 m")}; rb kinematic={(rb ? rb.isKinematic.ToString() : "-")} vel={(rb ? rb.linearVelocity.ToString() : "-")}; agent height={agent.height} radius={agent.radius} baseOffset={agent.baseOffset}";
            }
        }
        return null;
    }

    static List<Step> BuildFullSteps()
    {
        var steps = new List<Step>
        {
            new Step { Name = "start: poziom 1, pula skilli, pula pocisków", Wait = 1.5,
                Act = () =>
                {
                    // Pełny run testuje pętlę poziomów, nie przeżywalność: gracz jest nietykalny.
                    var h = Player != null ? Player.GetComponent<Health>() : null;
                    if (h != null) h.IsInvulnerable = true;
                },
                Check = () =>
            {
                var lm = Find<LevelManager>();
                if (lm == null || lm.CurrentLevel != 1) return "CurrentLevel = " + (lm?.CurrentLevel ?? -1);
                var slots = Player.GetComponent<SkillSlots>();
                if (slots == null) return "brak SkillSlots";
                Log("pula skilli: " + string.Join(", ", slots.Pool.Select(d => d ? d.DisplayName : "null")));
                if (slots.Pool.Count < 5) return "pula skilli ma " + slots.Pool.Count + " pozycji, oczekiwano 5";
                var user = Player.GetComponent<SkillUser>();
                var behaviours = (System.Collections.IDictionary)GetField(user, "behaviours");
                if (behaviours.Count < 5) return "zarejestrowane behavioury: " + behaviours.Count;
                if (Find<ProjectilePool>() == null) return "brak ProjectilePool w scenie";
                if (Find<HitFeedback>() == null) Log("uwaga: brak HitFeedback w scenie");
                if (Find<ProceduralSfxBank>() == null) return "brak ProceduralSfxBank (audio) w scenie";
                int tiersWithFx = slots.Pool.Where(d => d != null).Sum(d => d.Tiers.Count(t => t != null && t.EffectPrefab != null));
                Log($"tiery z EffectPrefab: {tiersWithFx}/{slots.Pool.Count * 4}");
                if (tiersWithFx < slots.Pool.Count * 4) return "nie wszystkie tiery skilli mają EffectPrefab";
                if (Find<LevelAmbience>() == null) return "brak LevelAmbience na poziomie 1";
                if (Find<WorldSign>() == null) Log("uwaga: brak napisu sterowania na poziomie 1");
                if (Player.GetComponentInChildren<PlayerLamp>() == null) Log("uwaga: brak PlayerLamp na graczu");
                var entry = CurrentEntry();
                if (entry == null || Vector3.Distance(Player.transform.position, entry.transform.position) > 2f) return "gracz nie na Level Entry";
                return null;
            }},
        };

        for (int level = 1; level <= 8; level++)
        {
            int lv = level;
            int zoneCount = ZonesPerLevel[lv];
            steps.Add(new Step { Name = $"poziom {lv}: załadowany, {zoneCount} strefy, gracz na podłodze", Wait = 1.0, Check = () =>
            {
                var lm = Find<LevelManager>();
                if (lm.CurrentLevel != lv) return "CurrentLevel = " + lm.CurrentLevel;
                var tracker = CurrentTracker();
                if (tracker == null || tracker.LevelNumber != lv) return "tracker poziomu " + lv + " nie istnieje";
                if (Zones.Length != zoneCount) return $"stref: {Zones.Length}, oczekiwano {zoneCount}";
                if (CurrentGate() == null) return "brak ExitGate";
                var entryNow = CurrentEntry();
                if (entryNow != null && Player.transform.position.y < entryNow.transform.position.y - 4f) return $"gracz spadł: {Player.transform.position}, wejście {entryNow.transform.position}";
                bool prevBack = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true; // poziom 8 ma płaską siatkę z normalnymi w dół
                bool floor = Physics.Raycast(Player.transform.position + Vector3.up, Vector3.down, out var hit, 10f, ~(1 << 6), QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = prevBack;
                if (!floor)
                {
                    // Diagnostyka: co jest wokół gracza i czy coś jest nad nim (gracz wpadł pod siatkę?).
                    var near = Physics.OverlapSphere(Player.transform.position, 3f, ~(1 << 6), QueryTriggerInteraction.Ignore)
                        .Select(c => $"{c.name}[{c.GetType().Name}] d={Vector3.Distance(c.ClosestPoint(Player.transform.position), Player.transform.position):F2}");
                    bool above = Physics.Raycast(Player.transform.position, Vector3.up, out var upHit, 30f, ~(1 << 6), QueryTriggerInteraction.Ignore);
                    var entry = CurrentEntry();
                    Log($"brak podłogi: gracz {Player.transform.position}, wejście {(entry ? entry.transform.position.ToString() : "brak")}, nad graczem: {(above ? upHit.collider.name + " " + upHit.point : "nic")}, w 3 m: {string.Join(", ", near)}");
                    return "brak podłogi pod graczem " + Player.transform.position;
                }
                Log($"poziom {lv}: wrogów do wybicia {tracker.TotalEnemies}, gracz {Player.transform.position}");
                Shot($"10-level-{lv}"); // widok z kamery gry na starcie poziomu: klimat, światło, dekoracje
                var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
                var surf = tracker.GetComponentInChildren<Unity.AI.Navigation.NavMeshSurface>(true);
                Log($"poziom {lv}: navmesh runtime {tri.vertices.Length} wierzchołków, surface data = {(surf && surf.navMeshData ? surf.navMeshData.name : "NULL")}, enabled={(surf ? surf.enabled.ToString() : "-")}");
                foreach (var z in Zones)
                    foreach (var en in z.Entries)
                    {
                        if (en.SpawnPoint == null) continue;
                        bool ok = UnityEngine.AI.NavMesh.SamplePosition(en.SpawnPoint.position, out var sh, 2.5f, UnityEngine.AI.NavMesh.AllAreas);
                        if (!ok) Log($"poziom {lv} {z.name}: spawn {en.SpawnPoint.name} ({en.EnemyPrefab?.name}) w {en.SpawnPoint.position} POZA NavMeshem");
                    }
                return null;
            }});

            for (int z = 0; z < zoneCount; z++)
            {
                int zi = z;
                steps.Add(new Step { Name = $"poziom {lv} strefa {zi + 1}: spawn", Wait = 1.2,
                    Act = () => { ResolveLevelUps(); Teleport(Zones[zi].transform.position + Vector3.up * 0.5f); },
                    Check = () =>
                {
                    var zone = Zones[zi];
                    if (!zone.IsTriggered) return "strefa nie odpaliła przy " + zone.transform.position;
                    var enemies = FindAll<EnemyHealth>();
                    if (enemies.Length < zone.TotalEnemyCount) return $"wrogów {enemies.Length}, strefa ma spawnować {zone.TotalEnemyCount}";
                    var placed = CheckEnemiesPlaced();
                    if (placed != null) return placed;
                    Log($"poziom {lv} strefa {zi + 1}: {enemies.Length} wrogów: " + string.Join(", ", enemies.Select(e => e.name.Replace("(Clone)", "")).Distinct()));
                    return null;
                }});
                steps.Add(new Step { Name = $"poziom {lv} strefa {zi + 1}: wybicie, serce, karty", Wait = 1.0,
                    Act = () =>
                    {
                        foreach (var e in FindAll<EnemyHealth>()) if (!e.IsBoss) { e.Health.IsInvulnerable = false; e.Health.ChangeHealth(-1000); }
                    },
                    Check = () =>
                {
                    ResolveLevelUps();
                    if (!Zones[zi].IsCleared && FindAll<EnemyHealth>().All(e => e.IsBoss) == false) return "strefa nie wyczyszczona, żywi: " + FindAll<EnemyHealth>().Length;
                    if (Time.timeScale == 0f) return "gra nadal w pauzie po kartach";
                    return null;
                }});
            }

            if (lv < 8)
            {
                steps.Add(new Step { Name = $"poziom {lv}: krata otwarta i zejście", Wait = 1.5,
                    Act = () =>
                    {
                        ResolveLevelUps();
                        var gate = CurrentGate();
                        if (gate != null && gate.IsOpen) Teleport(gate.transform.position + Vector3.up * 0.5f);
                    },
                    Check = () =>
                {
                    var lm = Find<LevelManager>();
                    if (lm.CurrentLevel != lv + 1) return $"CurrentLevel = {lm.CurrentLevel}, krata {(CurrentGate()?.IsOpen.ToString() ?? "brak")}, pozostali wrogowie {CurrentTracker()?.RemainingEnemies}";
                    return null;
                }});
            }
        }

        steps.Add(new Step { Name = "poziom 8: boss żyje i przechodzi stany", Wait = 4.0,
            Act = () => ResolveLevelUps(),
            Check = () =>
        {
            var boss = FindAll<EnemyHealth>().FirstOrDefault(e => e.IsBoss);
            if (boss == null) return "brak bossa na poziomie 8";
            var ctrl = boss.GetComponent<SpiderMotherBoss>();
            Log($"boss HP {boss.Health.CurrentHealth}/{boss.Health.MaxHealth}, kontroler {(ctrl ? "jest" : "BRAK")}, pozycja {boss.transform.position}");
            return null;
        }});
        steps.Add(new Step { Name = "poziom 8: śmierć bossa = wygrana", Wait = 1.0,
            Act = () =>
            {
                foreach (var e in FindAll<EnemyHealth>()) { e.Health.IsInvulnerable = false; e.Health.ChangeHealth(-10000); }
            },
            Check = () =>
        {
            var gm = Find<GameManager>();
            if (!gm.IsRunOver) return "IsRunOver = false";
            if (Time.timeScale != 0f) return "timeScale = " + Time.timeScale;
            var death = Find<DeathScreen>();
            var root = (GameObject)GetField(death, "root");
            if (root == null || !root.activeSelf) return "ekran końcowy nieaktywny";
            Log("ekran końcowy: " + ((TMPro.TMP_Text)GetField(death, "title"))?.text);
            return null;
        }});
        return steps;
    }
}
