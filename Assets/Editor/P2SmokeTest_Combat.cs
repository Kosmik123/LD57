using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Scenariusz „combat”: wykonanie każdego skilla na prawdziwych wrogach, strzelec trafia gracza,
/// pająk zeskakuje i goni, boss przechodzi stany, plus zrzuty ekranu z kamery gry do Logs/shots/.
/// Uruchomienie: P0SmokeTest.RunCombat (bash Tools/unity-batch.sh smokecombat) albo menu LD57.
/// </summary>
public static partial class P0SmokeTest
{
    const string SlimePrefabPath = "Assets/Prefabs/Enemies/Slime Enemy.prefab";
    const string SpiderPrefabPath = "Assets/Prefabs/Enemies/Spider.prefab";
    const string ShooterPrefabPath = "Assets/Prefabs/Enemies/Shooter.prefab";
    const string BossPrefabPath = "Assets/Prefabs/Enemies/Boss Spider Mother.prefab";

    public static void RunCombat()
    {
        SessionState.SetBool(BatchKey, true);
        SessionState.SetString(ScenarioKey, "combat");
        Start();
    }

    [MenuItem("LD57/Smoke test COMBAT (skille, wrogowie, boss, zrzuty)")]
    public static void RunCombatFromMenu()
    {
        SessionState.SetBool(BatchKey, false);
        SessionState.SetString(ScenarioKey, "combat");
        Start();
    }

    // ---------------------------------------------------------------- zrzuty ekranu

    static string ShotsDir => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "shots");

    static void ShotClose(string name) => Shot(name, true);

    static void Shot(string name, bool closeUp = false)
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Log("zrzut " + name + ": brak kamery"); return; }
            var prevPos = cam.transform.position;
            var prevRot = cam.transform.rotation;
            if (closeUp && Player != null)
            {
                // Zbliżenie: 7 m nad graczem, 5 m za nim, patrzy na gracza. Przywracane po renderze.
                var back = -Flat(Player.transform.forward) * 5f;
                cam.transform.position = Player.transform.position + Vector3.up * 7f + back;
                cam.transform.LookAt(Player.transform.position + Vector3.up * 0.8f);
            }
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            Directory.CreateDirectory(ShotsDir);
            var path = Path.Combine(ShotsDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            if (closeUp) { cam.transform.position = prevPos; cam.transform.rotation = prevRot; }
            Log("zrzut: " + path);
        }
        catch (System.Exception e) { Log("zrzut " + name + " nieudany: " + e.Message); }
    }

    // ---------------------------------------------------------------- pomocnicze

    static GameObject SpawnEnemy(string prefabPath, Vector3 position, bool snapToNavMesh = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Log("brak prefabu " + prefabPath); return null; }
        if (snapToNavMesh && NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas)) position = hit.position;
        var go = Object.Instantiate(prefab, position, Quaternion.identity);
        if (go.TryGetComponent<NavMeshAgent>(out var agent) && snapToNavMesh)
        {
            agent.enabled = false;
            go.transform.position = position;
            agent.enabled = true;
            agent.Warp(position);
        }
        return go;
    }

    static Vector3 Ahead(float meters) => Player.transform.position + Flat(Player.transform.forward) * meters;
    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward; }

    static int TotalEnemyHp() => FindAll<EnemyHealth>().Where(e => !e.IsBoss).Sum(e => e.Health.CurrentHealth);

    static void KillAll()
    {
        foreach (var e in FindAll<EnemyHealth>()) { e.Health.IsInvulnerable = false; e.Health.ChangeHealth(-100000); }
    }

    static void HealPlayer() => Player.GetComponent<Health>().FullHeal();

    static void DisableEnemyAttacks()
    {
        foreach (var a in FindAll<EnemyMeleeAttack>()) a.enabled = false;
    }

    static object SkillContextFor(SkillKind kind, out ISkillBehaviour behaviour)
    {
        var user = Player.GetComponent<SkillUser>();
        var behaviours = (IDictionary<SkillKind, ISkillBehaviour>)GetField(user, "behaviours");
        behaviours.TryGetValue(kind, out behaviour);
        var slots = Player.GetComponent<SkillSlots>();
        var def = slots.Pool.First(d => d != null && d.Kind == kind);
        var controller = Player.GetComponent<PlayerController>();
        return new SkillContext
        {
            Player = controller,
            Origin = controller.transform,
            Tier = def.GetTier(0),
            TierIndex = 0,
            EnemyLayers = ~0,
            Runner = user,
            AimDirection = Flat(controller.transform.forward),
        };
    }

    static string ExecuteSkill(SkillKind kind)
    {
        var ctx = (SkillContext)SkillContextFor(kind, out var behaviour);
        if (behaviour == null) return "brak behaviour dla " + kind;
        behaviour.Execute(ctx);
        return null;
    }

    static List<Step> BuildCombatSteps()
    {
        int hpBefore = 0, enemyHpBefore = 0, enemiesBefore = 0;
        bool spiderWallFound = false;
        Vector3 posBefore = Vector3.zero;
        var bossStates = new List<string>();

        return new List<Step>
        {
            new Step { Name = "poziom 1 załadowany", Wait = 1.5, Check = () =>
            {
                var lm = Find<LevelManager>();
                if (lm == null || lm.CurrentLevel != 1) return "CurrentLevel = " + (lm?.CurrentLevel ?? -1);
                Shot("01-level1-start");
                // Test walki: level up nie może pauzować gry, a strefy nie mogą spawnować własnych fal.
                var lvl = Find<LevelUpScreen>();
                if (lvl != null) lvl.enabled = false;
                foreach (var z in FindAll<WaveZone>()) foreach (var c in z.GetComponents<Collider>()) c.enabled = false;
                Log("ekran kart i strefy wyłączone na czas testu walki");
                // diagnostyka biomu: shader siatki poziomu, mech, drzewa
                var lmBiom = Find<LevelManager>();
                var levelRoot = lmBiom != null ? lmBiom.CurrentInstance : null;
                if (levelRoot != null)
                {
                    var model = levelRoot.transform.Find("Model");
                    var mr = model != null ? model.GetComponent<MeshRenderer>() : null;
                    var mat = mr != null ? mr.sharedMaterial : null;
                    var topTex = mat != null && mat.HasProperty("_TopMap") ? mat.GetTexture("_TopMap") as Texture2D : null;
                    string topInfo = "brak";
                    if (topTex != null)
                    {
                        try { var px = topTex.GetPixels32(); long r = 0, g = 0, b = 0; foreach (var c in px) { r += c.r; g += c.g; b += c.b; } topInfo = $"{topTex.name} {topTex.width}px avg=({r / px.Length},{g / px.Length},{b / px.Length})"; }
                        catch (System.Exception e) { topInfo = topTex.name + " (nieczytelna: " + e.Message + ")"; }
                    }
                    int trees = 0, bushes = 0, stones = 0, torches = 0;
                    foreach (var t in levelRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Tree")) trees++; else if (t.name.StartsWith("Bush")) bushes++; else if (t.name.StartsWith("Stone")) stones++; else if (t.name.StartsWith("Torch ")) torches++;
                    }
                    var decor = levelRoot.GetComponentInChildren<CaveDecor>();
                    Log($"biom: materiał={(mat ? mat.name : "brak")} shader={(mat ? mat.shader.name : "brak")} supported={(mat ? mat.shader.isSupported.ToString() : "-")} topColor={(mat && mat.HasProperty("_TopColor") ? mat.GetColor("_TopColor").ToString() : "-")} topThr={(mat && mat.HasProperty("_TopThreshold") ? mat.GetFloat("_TopThreshold").ToString("F2") : "-")} topMap={topInfo}; drzewa {trees}, krzaki {bushes}, kamienie {stones}, pochodnie {torches}, CaveDecor={(decor != null)}; mesh normal[0]={(mr != null && mr.GetComponent<MeshFilter>()?.sharedMesh != null && mr.GetComponent<MeshFilter>().sharedMesh.normals.Length > 0 ? mr.GetComponent<MeshFilter>().sharedMesh.normals[0].ToString() : "-")}");
                    ShotClose("01-level1-close");
                }
                return null;
            }},
            new Step { Name = "skille przyznane: Spin, Charge, AxeThrow w slotach, GroundSlam na spacji", Wait = 0.2,
                Act = () =>
                {
                    var slots = Player.GetComponent<SkillSlots>();
                    foreach (var kind in new[] { SkillKind.Spin, SkillKind.Charge, SkillKind.AxeThrow, SkillKind.GroundSlam })
                        slots.Grant(slots.Pool.First(d => d != null && d.Kind == kind));
                    // test walki potrzebuje szerokiej podłogi: środek strefy 1 (jej trigger jest wyłączony krok wcześniej)
                    if (Zones.Length > 0) Teleport(Zones[0].transform.position + Vector3.up * 0.3f);
                },
                Check = () =>
            {
                var slots = Player.GetComponent<SkillSlots>();
                if (slots.Slots.Count != 3) return "slotów: " + slots.Slots.Count;
                if (slots.SpaceSkill == null) return "brak GroundSlam na spacji";
                if (!Player.GetComponent<PlayerController>().JumpEnabled) return "JumpEnabled = false po GroundSlam";
                return null;
            }},
            new Step { Name = "ustawka: wszyscy wrogowie z bliska", Wait = 0.6,
                Act = () =>
                {
                    var fwd = Flat(Player.transform.forward);
                    var side = Vector3.Cross(Vector3.up, fwd);
                    SpawnEnemy(SlimePrefabPath, Player.transform.position + fwd * 3f - side * 3f);
                    SpawnEnemy(SpiderPrefabPath, Player.transform.position + fwd * 3f - side * 1f, snapToNavMesh: false);
                    SpawnEnemy(ShooterPrefabPath, Player.transform.position + fwd * 3f + side * 1f);
                    SpawnEnemy(BossPrefabPath, Player.transform.position + fwd * 5f + side * 3f);
                    DisableEnemyAttacks();
                    foreach (var b in FindAll<SpiderMotherBoss>()) b.enabled = false;
                    foreach (var sp in FindAll<SpiderController>()) sp.enabled = false;
                    foreach (var sh in FindAll<ShooterController>()) sh.enabled = false;
                },
                Check = () =>
            {
                ShotClose("00-lineup-close");
                Shot("00-lineup");
                // Nie zabijaj przez Health: śmierć bossa kończy run (IsBoss). Po prostu usuń obiekty.
                foreach (var e in FindAll<EnemyHealth>()) Object.Destroy(e.gameObject);
                return null;
            }},
            new Step { Name = "telegrafy: okrąg, stożek, linia widoczne", Wait = 0.45,
                Act = () =>
                {
                    var p = Player.transform.position;
                    var f = Flat(Player.transform.forward);
                    Telegraph.Circle(p + f * 2f, 1.5f, 1.0f);
                    Telegraph.Cone(p + f * 4f, Vector3.Cross(Vector3.up, f), 40f, 3f, 1.0f);
                    Telegraph.Line(p - f * 1f, p - f * 1f + Vector3.Cross(f, Vector3.up) * 4f, 1.0f);
                },
                Check = () =>
            {
                ShotClose("08-telegraphs-close");
                var tr = Find<TelegraphRenderer>();
                if (tr == null) return "brak TelegraphRenderer w scenie";
                var active = (System.Collections.IList)GetField(tr, "active");
                var renderers = tr.GetComponentsInChildren<Renderer>(true);
                int enabledCount = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
                var first = renderers.FirstOrDefault(r => r.gameObject.activeInHierarchy);
                Log($"telegrafy: aktywne markery {active.Count}, renderery włączone {enabledCount}/{renderers.Length}, Telegraph.Renderer={(Telegraph.Renderer != null)}, pierwszy: {(first ? first.name + " pos=" + first.transform.position + " bounds=" + first.bounds.size + " shader=" + first.sharedMaterial?.shader?.name + " queue=" + first.sharedMaterial?.renderQueue : "brak")}, gracz {Player.transform.position}");
                if (active.Count < 3) return "markery aktywne: " + active.Count;
                return null;
            }},
            new Step { Name = "UI tury 2: pasek HP wroga, „!”, HUD, powidoki dasha", Wait = 0.4,
                Act = () =>
                {
                    var slime = SpawnEnemy(SlimePrefabPath, Ahead(2.5f));
                    DisableEnemyAttacks();
                    if (slime != null) { var eh = slime.GetComponent<EnemyHealth>(); eh.Health.IsInvulnerable = false; eh.Health.ChangeHealth(-1); }
                    EnemyAlert.Raise(Player.transform.position);
                    var marks0 = Find<AlertMarkers>();
                    var alerts = FindAll<EnemyAlert>().Select(a => $"{a.name}@{Vector3.Distance(a.transform.position, Player.transform.position):F1}m act={a.isActiveAndEnabled}");
                    Log($"UI2 po Raise: „!” live={(marks0 ? ((System.Collections.IList)GetField(marks0, "live")).Count : -1)}, pool={(marks0 ? ((System.Collections.ICollection)GetField(marks0, "pool")).Count : -1)}, markers enabled={(marks0 ? marks0.isActiveAndEnabled.ToString() : "brak")}, EnemyAlert: [{string.Join(", ", alerts)}]");
                    Player.GetComponent<PlayerController>().ForceDash(Flat(Player.transform.forward) * -1f, 3f, 12f);
                },
                Check = () =>
            {
                ShotClose("09-ui-world-close");
                var bars = Find<EnemyHealthBars>();
                var marks = Find<AlertMarkers>();
                var hud = Find<HealthBarView>();
                var bossBar = Find<BossHealthBar>();
                var trail = Find<SwordTrail>();
                int barCount = bars != null ? ((System.Collections.IDictionary)GetField(bars, "active")).Count : -1;
                int markCount = marks != null ? ((System.Collections.IList)GetField(marks, "live")).Count : -1;
                int afterimages = GameObject.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m => m.name == "Afterimage");
                bool bossPanel = bossBar != null && (GetField(bossBar, "panel") as GameObject) != null && ((GameObject)GetField(bossBar, "panel")).activeSelf;
                bool hudHealth = hud != null && GetField(hud, "health") != null;
                bool trailMade = trail != null && GetField(trail, "trail") != null;
                var smr = Player.GetComponentInChildren<SkinnedMeshRenderer>();
                var firstGhost = GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).FirstOrDefault(m => m.name == "Afterimage");
                if (smr != null) Log($"UI2: gracz SMR {smr.name} pos={smr.transform.position} lossy={smr.transform.lossyScale} bounds={smr.bounds.size} localBounds={smr.localBounds.size}");
                if (firstGhost != null) Log($"UI2: powidok pos={firstGhost.transform.position} scale={firstGhost.transform.lossyScale} bounds={firstGhost.bounds.size} meshBounds={firstGhost.GetComponent<MeshFilter>().sharedMesh.bounds.size}");
                if (bossBar != null)
                {
                    var bound = GetField(bossBar, "boss") as EnemyHealth;
                    var panelGo = GetField(bossBar, "panel") as GameObject;
                    var bosses = FindAll<EnemyHealth>().Where(e => e.IsBoss).Select(e => $"{e.name} dead={e.Health.IsDead} hp={e.Health.CurrentHealth} active={e.gameObject.activeInHierarchy}");
                    Log($"UI2: BossHealthBar x{FindAll<BossHealthBar>().Length}, bound={(bound ? bound.name + " dead=" + bound.Health.IsDead : "null")}, panel={(panelGo ? panelGo.name + " self=" + panelGo.activeSelf + " hier=" + panelGo.activeInHierarchy : "null")}, bossy w scenie: [{string.Join("; ", bosses)}]");
                }
                Log($"UI2: paski wrogów {barCount}, „!” {markCount}, powidoki {afterimages}, HUD HP podpięty={hudHealth}, panel bossa widoczny={bossPanel} (ma być false), smuga miecza={trailMade}, dash={Player.GetComponent<PlayerController>().IsDashing}");
                KillAll();
                if (bars == null || marks == null || hud == null || bossBar == null) return "brak menedżera UI tury 2 w scenie";
                if (barCount < 1) return "brak paska HP nad trafionym wrogiem";
                if (markCount < 1) return "brak „!” po EnemyAlert.Raise";
                if (!hudHealth) return "HealthBarView bez referencji do Health gracza";
                if (bossPanel) return "panel bossa widoczny bez bossa";
                if (afterimages < 1) return "brak powidoków dasha";
                return null;
            }},
            new Step { Name = "zrzut: impakt wiru i uderzenia w ziemię", Wait = 0.15,
                Act = () =>
                {
                    SkillImpactVfx.SpinArc(Player.transform, 3f, 0.8f, new Color(1f, 0.75f, 0.3f));
                    SkillImpactVfx.Shockwave(Player.transform.position + Flat(Player.transform.forward) * 3f, 3f, new Color(1f, 0.8f, 0.45f));
                },
                Check = () => { ShotClose("11-impact-close"); return null; } },
            new Step { Name = "zrzut: śmierć slime'a, pająka i strzelca z bliska", Wait = 0.25,
                Act = () =>
                {
                    var fwd = Flat(Player.transform.forward);
                    var side = Vector3.Cross(Vector3.up, fwd);
                    var a = SpawnEnemy(SlimePrefabPath, Player.transform.position + fwd * 2.5f - side * 2f);
                    var b = SpawnEnemy(SpiderPrefabPath, Player.transform.position + fwd * 2.5f, snapToNavMesh: false);
                    var c = SpawnEnemy(ShooterPrefabPath, Player.transform.position + fwd * 2.5f + side * 2f);
                    foreach (var go in new[] { a, b, c })
                        if (go != null) { var h = go.GetComponent<Health>(); h.IsInvulnerable = false; h.ChangeHealth(-1000); }
                },
                Check = () => { ShotClose("12-deaths-close"); KillAll(); return null; } },
            new Step { Name = "Spin rani 3 slime'y wokół gracza", Wait = 1.5,
                Act = () =>
                {
                    for (int i = 0; i < 3; i++)
                        SpawnEnemy(SlimePrefabPath, Player.transform.position + Quaternion.Euler(0, 120 * i, 0) * Vector3.forward * 1.8f);
                    DisableEnemyAttacks();
                    enemyHpBefore = TotalEnemyHp();
                    var err = ExecuteSkill(SkillKind.Spin);
                    if (err != null) Log("Spin: " + err);
                },
                Check = () =>
            {
                Shot("02-spin");
                int hp = TotalEnemyHp();
                if (hp >= enemyHpBefore) return $"HP wrogów {enemyHpBefore} → {hp}, Spin nie zranił";
                Log($"Spin: HP wrogów {enemyHpBefore} → {hp}");
                KillAll();
                return null;
            }},
            new Step { Name = "Charge przenosi gracza i rani slime'a na torze", Wait = 1.0,
                Act = () =>
                {
                    SpawnEnemy(SlimePrefabPath, Ahead(2.5f));
                    SpawnEnemy(SlimePrefabPath, Ahead(4.5f));
                    DisableEnemyAttacks();
                    enemyHpBefore = TotalEnemyHp();
                    posBefore = Player.transform.position;
                    var err = ExecuteSkill(SkillKind.Charge);
                    if (err != null) Log("Charge: " + err);
                },
                Check = () =>
            {
                float moved = Vector3.Distance(Flat(Player.transform.position) * 0 + Player.transform.position, posBefore);
                int hp = TotalEnemyHp();
                Log($"Charge: przesunięcie {moved:F1} m, HP wrogów {enemyHpBefore} → {hp}");
                KillAll();
                if (moved < 3f) return $"gracz przesunął się tylko {moved:F1} m";
                if (hp >= enemyHpBefore) return "slime na torze szarży nie zraniony";
                return null;
            }},
            new Step { Name = "GroundSlam: gracz odrywa się od ziemi", Wait = 0.35,
                Act = () =>
                {
                    for (int i = 0; i < 2; i++)
                        SpawnEnemy(SlimePrefabPath, Player.transform.position + Quaternion.Euler(0, 180 * i, 0) * Vector3.forward * 1.5f);
                    DisableEnemyAttacks();
                    enemyHpBefore = TotalEnemyHp();
                    posBefore = Player.transform.position;
                    var pc = Player.GetComponent<PlayerController>();
                    Log($"GroundSlam: IsGrounded={pc.IsGrounded} JumpEnabled={pc.JumpEnabled} timeScale={Time.timeScale}");
                    var err = ExecuteSkill(SkillKind.GroundSlam);
                    if (err != null) Log("GroundSlam: " + err);
                },
                Check = () =>
            {
                var rb = Player.GetComponent<Rigidbody>();
                Log($"GroundSlam po 0,35 s: y {posBefore.y:F2} → {Player.transform.position.y:F2}, vy={rb.linearVelocity.y:F1}");
                if (Player.transform.position.y < posBefore.y + 0.3f && rb.linearVelocity.y < 1f) return "gracz nie skoczył";
                return null;
            }},
            new Step { Name = "GroundSlam: obrażenia przy lądowaniu", Wait = 2.0, Check = () =>
            {
                int hp = TotalEnemyHp();
                Log($"GroundSlam: HP wrogów {enemyHpBefore} → {hp}, gracz y={Player.transform.position.y:F2}");
                KillAll();
                if (hp >= enemyHpBefore) return "brak obrażeń przy lądowaniu (skok nie wykonany?)";
                return null;
            }},
            new Step { Name = "AxeThrow rani slime'a 4 m przed graczem", Wait = 2.0,
                Act = () =>
                {
                    SpawnEnemy(SlimePrefabPath, Ahead(3f));
                    SpawnEnemy(SlimePrefabPath, Ahead(5f));
                    DisableEnemyAttacks();
                    enemyHpBefore = TotalEnemyHp();
                    var err = ExecuteSkill(SkillKind.AxeThrow);
                    if (err != null) Log("AxeThrow: " + err);
                },
                Check = () =>
            {
                int hp = TotalEnemyHp();
                Log($"AxeThrow: HP wrogów {enemyHpBefore} → {hp}");
                KillAll();
                if (hp >= enemyHpBefore) return "topór nie trafił";
                return null;
            }},
            new Step { Name = "WarCry odrzuca slime'a", Wait = 1.0,
                Act = () =>
                {
                    var go = SpawnEnemy(SlimePrefabPath, Ahead(1.5f));
                    DisableEnemyAttacks();
                    posBefore = go != null ? go.transform.position : Vector3.zero;
                    var err = ExecuteSkill(SkillKind.WarCry);
                    if (err != null) Log("WarCry: " + err);
                },
                Check = () =>
            {
                var e = FindAll<EnemyHealth>().FirstOrDefault();
                if (e == null) return "slime zniknął";
                float moved = Vector3.Distance(e.transform.position, posBefore);
                var agent = e.GetComponent<NavMeshAgent>();
                Log($"WarCry: slime przesunięty o {moved:F1} m, isStopped={(agent ? agent.isStopped.ToString() : "-")}");
                KillAll();
                if (moved < 0.8f && !(agent != null && agent.isStopped)) return "brak odrzutu i brak ogłuszenia";
                return null;
            }},
            new Step { Name = "slime: windup z bliska", Wait = 0.35,
                Act = () =>
                {
                    HealPlayer();
                    hpBefore = Player.GetComponent<Health>().CurrentHealth;
                    var slime = SpawnEnemy(SlimePrefabPath, Ahead(2f));
                    if (slime != null) Teleport(slime.transform.position + slime.transform.forward * 0.9f);
                },
                Check = () => { ShotClose("03-slime-windup-close"); return null; } },
            new Step { Name = "slime atakuje: windup i cios ranią gracza", Wait = 2.7,
                Check = () =>
            {
                Shot("03-slime-attack");
                int hp = Player.GetComponent<Health>().CurrentHealth;
                KillAll();
                if (hp >= hpBefore) return $"HP gracza {hpBefore} → {hp}";
                Log($"slime: HP gracza {hpBefore} → {hp}");
                return null;
            }},
            new Step { Name = "strzelec: celowanie z bliska", Wait = 0.6,
                Act = () =>
                {
                    HealPlayer();
                    hpBefore = Player.GetComponent<Health>().CurrentHealth;
                    SpawnEnemy(ShooterPrefabPath, Ahead(5f));
                },
                Check = () => { ShotClose("04-shooter-aim-close"); return null; } },
            new Step { Name = "strzelec: pocisk rani gracza z 5 m", Wait = 8.0,
                Check = () =>
            {
                Shot("04-shooter");
                int hp = Player.GetComponent<Health>().CurrentHealth;
                var pool = Find<ProjectilePool>();
                var projectiles = FindAll<Projectile>();
                var shooter = Find<ShooterController>();
                string shooterInfo = shooter == null ? "brak strzelca" : $"strzelec w {shooter.transform.position}, dystans {Vector3.Distance(shooter.transform.position, Player.transform.position):F1} m, agent onMesh={shooter.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh}";
                Log($"strzelec: HP gracza {hpBefore} → {hp}, pociski w scenie {projectiles.Length}, pula {(pool ? "jest" : "BRAK")}; {shooterInfo}; gracz {Player.transform.position}");
                foreach (var pr in projectiles) Log($"  pocisk {pr.name} aktywny={pr.gameObject.activeInHierarchy} pos={pr.transform.position}");
                KillAll();
                if (hp >= hpBefore) return $"gracz nie trafiony (HP {hpBefore} → {hp}, pocisków {projectiles.Length})";
                return null;
            }},
            new Step { Name = "pająk: na czatach z bliska", Wait = 0.25,
                Act = () =>
                {
                    HealPlayer();
                    hpBefore = Player.GetComponent<Health>().CurrentHealth;
                    SpawnEnemy(SpiderPrefabPath, Ahead(7f) + Vector3.up * 3f, snapToNavMesh: false);
                },
                Check = () => { ShotClose("05-spider-perched-close"); return null; } },
            new Step { Name = "pająk: gracz podchodzi na 4,5 m, zeskok, pościg, cios", Wait = 5.75,
                Act = () =>
                {
                    var spider = Find<SpiderController>();
                    if (spider != null)
                    {
                        var toSpider = Flat(spider.transform.position - Player.transform.position);
                        Teleport(spider.transform.position - toSpider * 4.5f + Vector3.zero);
                    }
                },
                Check = () =>
            {
                Shot("05-spider");
                ShotClose("05-spider-close");
                var spider = Find<SpiderController>();
                if (spider == null) return "brak pająka";
                var agent = spider.GetComponent<NavMeshAgent>();
                int hp = Player.GetComponent<Health>().CurrentHealth;
                var state = spider.Current;
                bool onMesh = agent.enabled && agent.isOnNavMesh;
                Log($"pająk: stan {state}, agent enabled={agent.enabled} onNavMesh={onMesh}, dystans {Vector3.Distance(spider.transform.position, Player.transform.position):F1} m, HP gracza {hpBefore} → {hp}");
                KillAll();
                if (state == SpiderController.State.Perched) return "pająk nie zeskoczył";
                if (!onMesh) return "pająk po zeskoku nie jest na NavMeshu";
                return null;
            }},
            new Step { Name = "boss: stany i zeskok w 8 s", Wait = 8.0,
                Act = () =>
                {
                    HealPlayer();
                    hpBefore = Player.GetComponent<Health>().CurrentHealth;
                    enemiesBefore = FindAll<EnemyHealth>().Length;
                    Player.GetComponent<Health>().IsInvulnerable = true; // boss ma przejść cykl, gracz ma przeżyć
                    var go = SpawnEnemy(BossPrefabPath, Ahead(5f));
                    var boss = go != null ? go.GetComponent<SpiderMotherBoss>() : null;
                    if (boss != null) boss.OnStateChanged += s => bossStates.Add(s);
                    if (boss != null) boss.OnTelegraph += p => ShotClose("06-boss-telegraph-close");
                },
                Check = () =>
            {
                Shot("06-boss");
                ShotClose("06-boss-close");
                var boss = Find<SpiderMotherBoss>();
                if (boss == null) return "brak bossa";
                int hp = Player.GetComponent<Health>().CurrentHealth;
                int enemies = FindAll<EnemyHealth>().Length;
                Log($"boss: stany [{string.Join(" → ", bossStates)}], HP gracza {hpBefore} → {hp}, wrogów {enemiesBefore} → {enemies}, pozycja {boss.transform.position}");
                Player.GetComponent<Health>().IsInvulnerable = false;
                if (bossStates.Count < 2) return "boss nie zmienia stanów: " + string.Join(",", bossStates);
                return null;
            }},
            new Step { Name = "boss: zniszczenie 5 nóg = przewrócenie", Wait = 2.8,
                Act = () =>
                {
                    var boss = Find<SpiderMotherBoss>();
                    if (boss == null) { Log("nogi bossa: brak bossa"); return; }
                    int destroyed = 0;
                    foreach (var leg in boss.Legs)
                    {
                        if (destroyed >= 5) break;
                        if (leg == null || leg.Health == null || leg.Health.IsDead) continue;
                        leg.Damage(leg.MaxHealth);
                        destroyed++;
                    }
                    Log($"nogi bossa: zniszczono {destroyed}, żywych {boss.AliveLegs}, do zniszczenia {boss.LegsToDestroy}");
                },
                Check = () =>
            {
                ShotClose("16-boss-legs-close");
                var boss = Find<SpiderMotherBoss>();
                var gm = Find<GameManager>();
                bool dead = boss == null || boss.GetComponent<Health>().IsDead;
                Log($"nogi bossa: boss {(boss ? "jest" : "brak")}, collapsing={(boss ? boss.IsCollapsing.ToString() : "-")}, martwy={dead}, IsRunOver={gm.IsRunOver}");
                if (boss != null && !boss.IsCollapsing && !dead) return $"boss po utracie 5 nóg nie przewraca się (żywych nóg {boss.AliveLegs})";
                return null;
            }},
            new Step { Name = "pająk na ścianie: perch, bieg po ścianie, orientacja", Wait = 1.2,
                Act = () =>
                {
                    SpiderSpawnHelper.Reset();
                    spiderWallFound = false;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath);
                    var entryT = CurrentEntry() != null ? CurrentEntry().transform.position : Player.transform.position;
                    var cands = new List<Vector3> { Player.transform.position, entryT };
                    foreach (var z in Zones) cands.Add(z.transform.position);
                    foreach (var c in cands)
                    {
                        if (!SpiderSpawnHelper.FindWallAbove(c, out var wallPt, out var wallN, 20f, true)) continue;
                        var standAt = wallPt + wallN * 6f;
                        if (NavMesh.SamplePosition(standAt, out var h, 3f, NavMesh.AllAreas)) standAt = h.position;
                        Teleport(standAt + Vector3.up * 0.3f);
                        var toWall = Flat(wallPt - Player.transform.position);
                        if (toWall.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.LookRotation(toWall);
                        SpiderSpawnHelper.SpawnForTest(prefab, wallPt + wallN * 2f, Player.transform.position, false);
                        Log($"pająk-ściana: ściana {wallPt} normalna {wallN}, gracz {Player.transform.position}, tryb {SpiderSpawnHelper.LastSpawnMode}");
                        spiderWallFound = true;
                        break;
                    }
                    if (!spiderWallFound) Log("pająk-ściana: brak ściany w 20 m od gracza, wejścia i stref");
                },
                Check = () =>
            {
                ShotClose("15-spider-wall-close");
                var sp = Find<SpiderController>();
                float dot = sp != null ? Vector3.Dot(sp.transform.up, sp.SurfaceNormal) : -2f;
                Log($"pająk-ściana: tryb={SpiderSpawnHelper.LastSpawnMode} perches={SpiderSpawnHelper.WallPerches} wallWalks={SpiderSpawnHelper.WallWalks} stan={(sp ? sp.Current.ToString() : "brak")} onSurface={(sp ? sp.OnSurface.ToString() : "-")} dot(up,normalna)={dot:F2} pos={(sp ? sp.transform.position.ToString() : "-")}");
                KillAll();
                if (!spiderWallFound) return "brak ściany w 20 m (geometria poziomu 1)";
                if (SpiderSpawnHelper.LastSpawnMode != "wall") return "pająk nie na ścianie, tryb: " + SpiderSpawnHelper.LastSpawnMode;
                if (SpiderSpawnHelper.WallPerches < 1) return "WallPerches = 0";
                if (SpiderSpawnHelper.WallWalks < 1) return "pająk nie zaczął biec po ścianie (gracz 6 m od ściany)";
                if (sp != null && sp.Current == SpiderController.State.WallWalk && dot < 0.9f) return $"pająk na ścianie nie jest zorientowany do niej (dot {dot:F2})";
                return null;
            }},
            new Step { Name = "mag: klasa, pocisk w bossa, powrót do wojownika", Wait = 0.7,
                Act = () =>
                {
                    var applier = Player.GetComponent<PlayerClassApplier>();
                    if (applier == null) return;
                    applier.Apply("Mage");
                    var target = SpawnEnemy(SlimePrefabPath, Ahead(3f));
                    DisableEnemyAttacks();
                    if (target != null)
                    {
                        var to = Flat(target.transform.position - Player.transform.position);
                        if (to.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.LookRotation(to);
                        var th = target.GetComponent<Health>(); th.IsInvulnerable = false;
                        enemyHpBefore = th.CurrentHealth;
                    }
                    var ranged = Player.GetComponent<RangedAttack>();
                    if (ranged != null) Call(ranged, "Player_OnAttack");
                },
                Check = () =>
            {
                ShotClose("13-mage-close");
                var applier = Player.GetComponent<PlayerClassApplier>();
                var ranged = Player.GetComponent<RangedAttack>();
                var bolts = FindAll<PlayerBolt>().Count(b => b.gameObject.activeInHierarchy);
                var slime = FindAll<EnemyHealth>().FirstOrDefault(e => !e.IsBoss && e.GetComponent<SlimeController>() != null);
                int hp = slime != null ? slime.Health.CurrentHealth : 0;
                var smr = Player.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.gameObject.activeInHierarchy).Select(r => r.name);
                Log($"mag: applier={(applier != null)}, RangedAttack enabled={(ranged != null && ranged.enabled)}, MeleeHitboxes={Player.GetComponent<PlayerController>().MeleeHitboxes}, pociski aktywne {bolts}, slime HP {enemyHpBefore} → {hp} (null={slime == null}), pula skilli {Player.GetComponent<SkillSlots>().Pool.Count}, renderery: {string.Join(", ", smr)}");
                if (applier != null) applier.Apply("Warrior");
                foreach (var e in FindAll<EnemyHealth>()) if (!e.IsBoss) { e.Health.IsInvulnerable = false; e.Health.ChangeHealth(-1000); }
                if (applier == null) return "brak PlayerClassApplier na graczu";
                if (ranged == null) return "brak RangedAttack na graczu";
                if (slime != null && hp >= enemyHpBefore) return $"pocisk maga nie trafił slime'a 3 m przed graczem (HP {enemyHpBefore} → {hp})";
                return null;
            }},
            new Step { Name = "sprzątanie: boss zabity = wygrana", Wait = 1.0,
                Act = () => KillAll(),
                Check = () =>
            {
                var gm = Find<GameManager>();
                int lvl = Find<LevelManager>() != null ? Find<LevelManager>().CurrentLevel : 1;
                // od rundy 13 boss pokonany przed poziomem 8 (playtest) nie kończy runu; wygrana tylko na dnie
                if (lvl >= 8 && !gm.IsRunOver) return "boss zabity na poziomie 8, ale IsRunOver = false";
                if (lvl < 8 && gm.IsRunOver) return $"boss zabity na poziomie {lvl} zakończył run (ma trwać dalej)";
                Shot("07-win");
                return null;
            }},
        };
    }
}
