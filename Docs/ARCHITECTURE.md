# LD57 – architektura implementacji (backlog P0)

Źródło designu: dokument projektowy (link w CLAUDE.md). Ten plik przekłada design na kod: moduły,
kontrakty między nimi, własność plików i liczby. Kod pisany równolegle przez kilka osób/agentów
ma się spotkać na tych kontraktach, więc **sygnatur publicznych z sekcji 3 nie zmieniamy** bez
zapisania tego tutaj.

## 0. Twarde zasady

1. **Tylko pliki `.cs`.** Nie edytujemy `.unity`, `.prefab`, `.asset`, `.meta`, `.controller`,
   grafów Behavior ani `ProjectSettings/`. Wszystko, co wymaga edytora Unity, opisujemy jako
   krok w `Docs/WIRING.md` (sekcja na agenta). Nowe `.cs` bez `.meta` są OK, Unity je wygeneruje.
2. **Każdy plik ma jednego właściciela** (tabela w sekcji 2). Zmiana w cudzym pliku = prośba do
   właściciela albo zapis w raporcie, nie cicha edycja.
3. **Komunikacja między modułami tylko przez kontrakty z sekcji 3** (`GameEvents`, zdarzenia
   komponentów, `SkillSlots`, `WaveZone`). Zero `FindObjectOfType` w pętli gry; raz w `Awake`/`Start`
   dopuszczalne, ale preferowane `[SerializeField]`.
4. **Kompilacja przed raportem:** `bash Tools/compile-check.sh` musi zwrócić `OK`.
5. Styl jak w repo: `[SerializeField] private` + property, zdarzenia `System.Action`, subskrypcja w
   `OnEnable`/odpięcie w `OnDisable`, input w `Update`, fizyka w `FixedUpdate`, NaughtyAttributes
   dozwolone (`[Foldout]`, `[ShowNonSerializedField]`, `[Tag]`). Komentarze po polsku, nazwy po angielsku.
6. Pauza: `Time.timeScale = 0` na ekranie kart i po śmierci. `Invoke`, `WaitForSeconds` i `Update`
   z `Time.deltaTime` zatrzymują się wtedy same, i o to chodzi. UI w pauzie używa
   `Time.unscaledDeltaTime` / `WaitForSecondsRealtime`.
7. Wróg spawnowany musi stanąć na NavMeshu: spawn z wyłączonym `NavMeshAgent`, `Warp` na punkt,
   dopiero potem `enabled = true`. Globalny blackboard `Player` jest ustawiany w `Player.Awake`,
   więc wrogowie spawnowani później go widzą.

## 1. Moduły i przepływ

```
Input ──► PlayerController ──► DamagingBlade(Player) ──► Health(Enemy) ──► EnemyHealth.OnEnemyDied
                │                                                                │
                │ OnDashed/OnDashEnded                                           ├─► Experience ──► GameEvents.OnExperienceGained / OnLevelUp ──► LevelUpScreen ──► SkillSlots
                ▼                                                                ├─► WaveZone (licznik fali) ──► GameEvents.OnWaveCleared ──► HeartPickup
       DashInvulnerability ──► Health(Player).IsInvulnerable                     └─► LevelEnemyTracker ──► GameEvents.OnLevelCleared ──► ExitGate ──► LevelManager ──► GameEvents.OnLevelStarted
                                                                                                                                              │
Behavior graph (Slime) ──► TriggerMeleeAttackAction ──► EnemyMeleeAttack ──► DamagingBlade(Enemy) ──► Health(Player) ──► OnDied ──► GameManager ──► GameEvents.OnPlayerDied ──► DeathScreen ──► restart
```

Trzy obszary, trzech właścicieli:

| Obszar | Zakres | Folder |
| --- | --- | --- |
| **Combat** | obrażenia wrogów z windupem, nietykalność w dashu, śmierć wroga, akcja grafu Behavior | `Scripts/Combat/`, `Scripts/Enemies/`, `Scripts/Custom Behavior Graph Actions/`, `Scripts/PlayerController.cs` |
| **Run flow** | stan runu, śmierć i restart, poziomy, strefy fal, krata, serca | `Scripts/Game/`, `Scripts/Levels/`, `Scripts/Waves/`, `Scripts/Pickups/`, `Scripts/UI/DeathScreen.cs` |
| **Progression** | XP i progi, ekran kart, sloty skilli, implementacje skilli, HUD skilli | `Scripts/Progression/`, `Scripts/Skills/`, `Scripts/UI/LevelUpScreen.cs`, `Scripts/UI/SkillHud.cs` |

## 2. Własność plików

| Plik | Właściciel | Stan |
| --- | --- | --- |
| `Scripts/Combat/Health.cs` | Combat | kontrakt gotowy (IsInvulnerable, IsDead); nie zmieniać sygnatur |
| `Scripts/Combat/EnemyHealth.cs` | Combat | kontrakt gotowy (OnEnemyDied, ExperienceReward) |
| `Scripts/Combat/DamagingBlade.cs` | Combat | do poprawy: null-safe, opcjonalny odrzut |
| `Scripts/Combat/DashInvulnerability.cs` | Combat | nowy |
| `Scripts/Enemies/EnemyMeleeAttack.cs` | Combat | nowy |
| `Scripts/Enemies/EnemyDeath.cs` | Combat | nowy: jedyny, który niszczy wroga |
| `Scripts/Custom Behavior Graph Actions/TriggerMeleeAttackAction.cs` | Combat | nowy |
| `Scripts/PlayerController.cs` | Combat | kontrakt gotowy (JumpEnabled, OnDashEnded, OnLanded); Combat dodaje cooldown dasha |
| `Scripts/Game/GameEvents.cs` | koordynator | kontrakt gotowy, zamrożony |
| `Scripts/Game/GameManager.cs` | Run flow | nowy |
| `Scripts/Game/RunState.cs` | Run flow | nowy (numer poziomu, czas runu, statystyki do ekranu wyniku) |
| `Scripts/Levels/LevelManager.cs` | Run flow | nowy |
| `Scripts/Levels/LevelDefinition.cs` | Run flow | nowy (ScriptableObject: prefab poziomu, numer) |
| `Scripts/Levels/LevelEnemyTracker.cs` | Run flow | nowy |
| `Scripts/Levels/ExitGate.cs` | Run flow | nowy |
| `Scripts/Levels/LevelEntry.cs` | Run flow | nowy (marker punktu wejścia gracza w prefabie poziomu) |
| `Scripts/Waves/WaveZone.cs` | Run flow | kontrakt gotowy (pola publiczne), ciało do napisania |
| `Scripts/Pickups/HeartPickup.cs` | Run flow | nowy |
| `Scripts/UI/DeathScreen.cs` | Run flow | nowy |
| `Scripts/Progression/Experience.cs` | Progression | nowy |
| `Scripts/Skills/SkillDefinition.cs` | Progression | kontrakt gotowy |
| `Scripts/Skills/SkillSlots.cs` | Progression | kontrakt gotowy, działa; Progression może dodać ciała, nie zmieniać sygnatur |
| `Scripts/Skills/SkillUser.cs` | Progression | nowy: czyta Q/E/R/Space, odpala skill, liczy cooldowny |
| `Scripts/Skills/Behaviours/*.cs` | Progression | nowe: SpinSkill, ChargeSkill, GroundSlamSkill (+ AxeThrow, WarCry jeśli czas) |
| `Scripts/UI/LevelUpScreen.cs`, `Scripts/UI/SkillHud.cs` | Progression | nowe |
| `Scripts/UI/UIManager.cs` | nikt | bez zmian |
| `Scripts/Player.cs`, `LookAtCursor.cs`, `PlayerAnimations.cs`, `CameraController.cs` | nikt | bez zmian |
| `Scripts/PlayerDash.cs`, `SimpleCharacterMovement.cs` | nikt | martwe, nie dotykać, nie usuwać (P2) |

## 3. Kontrakty (zamrożone)

### 3.1 Health (gotowe)
```csharp
public event Action<int> OnHealthChanged;   // delta; nie wywoływane gdy obrażenia zablokowane
public event Action OnDied;                 // raz, gdy HP spadło do 0
public int MaxHealth, CurrentHealth;
public bool IsInvulnerable { get; set; }    // ujemne zmiany ignorowane
public bool IsDead;
public void ChangeHealth(int change); public void FullHeal();
```

### 3.2 EnemyHealth (gotowe)
```csharp
public static event Action<EnemyHealth, int> OnEnemyDamaged; // (wróg, obrażenia > 0)
public static event Action<EnemyHealth> OnEnemyDied;         // PRZED Destroy
public int ExperienceReward;  // slime 10, pająk 15, strzelec 20, boss 0
public Health Health;
```
Zasada: **tylko `EnemyDeath` (Combat) niszczy wroga**, po `OnEnemyDied`, z opóźnieniem na animację
(0,5 s). Tracker i Experience słuchają `OnEnemyDied`, nigdy `OnDestroy`.

### 3.3 PlayerController (gotowe)
```csharp
public event Action OnDashed, OnDashEnded, OnAttack, OnLanded;
public bool JumpEnabled { get; set; }   // domyślnie false; GroundSlam ustawia true
public bool IsGrounded, IsDashing; public Vector2 LocalDirection;
```
Combat dodał: `dashCooldown` 0,8 s (pole + `public float DashCooldownRemaining`), dash blokowany
w cooldownie, `public void RequestJump()`. **Skok wyłącznie przez `RequestJump()`**: oś `Jump` nie jest
czytana, bo spacja skakałaby z pominięciem cooldownu GroundSlam. `SkillUser` obsługuje spację.
Triggery sprawdzają tag przez `ColliderTags.IsPlayer/IsEnemy` (collidery gracza są na dzieciach bez tagu).
`public bool ForceDash(Vector3 dir, float distance, float speed)`: ruch skilla Charge idzie przez kontroler,
bo `FixedUpdate` nadpisuje `linearVelocity` co krok; podnosi `OnDashed`, więc szarża jest nietykalna
i nie zużywa cooldownu zwykłego dasha. Spawny stref są dziećmi strefy, więc giną razem z poziomem.

### 3.4 GameEvents (gotowe, zamrożone)
Patrz `Scripts/Game/GameEvents.cs`. Kto podnosi co:

| Zdarzenie | Podnosi | Słucha |
| --- | --- | --- |
| `OnExperienceGained(int)` | Experience | HUD |
| `OnLevelUp(int heroLevel)` | Experience | LevelUpScreen (pauza + karty) |
| `OnWaveCleared(WaveZone)` | WaveZone | HeartPickup spawn (WaveZone sam), audio |
| `OnLevelStarted(int)` | LevelManager | LevelEnemyTracker (reset), HUD |
| `OnLevelCleared(int)` | LevelEnemyTracker | ExitGate (otwórz), audio |
| `OnPlayerDied` | GameManager | DeathScreen |
| `OnRunWon` | GameManager (po śmierci wroga z `IsBoss`) | DeathScreen w trybie wygranej |

`GameManager.RestartRun()` woła `GameEvents.ResetEvents()`, potem `SceneManager.LoadScene(SceneManager.GetActiveScene().name)`.

### 3.5 WaveZone (pola gotowe, ciało: Run flow)
```csharp
public SpawnEntry[] Entries;  // EnemyPrefab, SpawnPoint, Count
public int TotalEnemyCount; public bool IsTriggered, IsCleared;
```
Zachowanie: `OnTriggerEnter` z tagiem `Player` → spawn wszystkich wpisów (zasada 0.7) → zapamiętaj
`EnemyHealth` spawnowanych → przy każdym `OnEnemyDied` z tej listy zmniejsz licznik → 0 ⇒
`IsCleared = true`, serce w pozycji ostatniego wroga, `GameEvents.RaiseWaveCleared(this)`.
Collider triggera na tym samym obiekcie, `isTrigger = true`.

### 3.6 LevelEnemyTracker i ExitGate (Run flow)
Tracker w prefabie poziomu (`_Level Base`): w `Start` zbiera `GetComponentsInChildren<WaveZone>()`,
sumuje `TotalEnemyCount`, liczy `OnEnemyDied` wrogów spawnowanych przez te strefy (WaveZone
udostępnia `event Action<WaveZone, EnemyHealth> OnEnemySpawned`, dodaj do kontraktu WaveZone),
gdy zabitych == suma ⇒ `GameEvents.RaiseLevelCleared(levelNumber)`.
ExitGate: słucha `OnLevelCleared`, otwiera (animacja/wyłączenie collidera, dźwięk), trigger z tagiem
`Player` ⇒ `LevelManager.Instance.GoToNextLevel()`.

### 3.7 LevelManager (Run flow)
```csharp
public static LevelManager Instance;   // SceneSingleton z Bipolar.Prototyping jak Player
public int CurrentLevel;               // 1–8
public void StartRun();                // ładuje poziom 1
public void GoToNextLevel();           // niszczy obecny prefab, instancjuje kolejny, przenosi gracza na LevelEntry, RaiseLevelStarted
```
Lista poziomów: `LevelDefinition[]` w inspektorze (8 assetów albo bezpośrednio 8 prefabów; prościej:
`GameObject[] levelPrefabs`). Gracz NIE jest częścią prefabu poziomu; jest w scenie i jest przenoszony
(`rb.position = entry.position`, `rb.linearVelocity = Vector3.zero`).

### 3.8 Experience (Progression)
```csharp
public int HeroLevel;   // od 0; level up ⇒ +1
public int CurrentXp, XpToNextLevel;
```
Próg do kolejnego levelu: `40 + 13 * HeroLevel` (0→1: 40, 1→2: 53, …). Słucha `EnemyHealth.OnEnemyDied`
⇒ `+ExperienceReward` ⇒ `RaiseExperienceGained`; przy przekroczeniu progu (może kilka naraz)
`RaiseLevelUp(HeroLevel)` dla każdego. Nadwyżka XP przechodzi dalej.

### 3.9 SkillSlots i SkillDefinition (gotowe)
Patrz pliki. `SkillSlots.GetOfferableSkills()` zwraca pulę kart; `LevelUpScreen` losuje do 3 różnych,
`Grant(def)` po wyborze. Gdy oferta ma < 3 pozycji, pokaż tyle, ile jest; gdy 0, pokaż jedną kartę
„+10 maks. HP” (Progression: `Health` gracza przez `SerializeField`; na razie bez zmiany MaxHealth w
kontrakcie, więc karta „+10 HP” leczy 10; zapisz w raporcie jako deviation jeśli wolisz dodać `AddMaxHealth`).

### 3.10 Skille (Progression)
`SkillUser` (na graczu) w `Update`: dla slotu `i` klawisz `SkillSlots.SlotKeys[i]`, dla spacji
`SpaceSlotKey`; odpala `ISkillBehaviour` wybrany po `SkillKind`, ustawia `CooldownRemaining`.
```csharp
public interface ISkillBehaviour { void Execute(SkillContext ctx); }
public struct SkillContext { public PlayerController Player; public Transform Origin; public SkillTier Tier; public int TierIndex; public LayerMask EnemyLayers; }
```
Trafianie wrogów: `Physics.OverlapSphere` / `OverlapCapsule` po tagu `Enemy` → `Health.ChangeHealth(-Damage)`;
odrzut przez `NavMeshAgent.Move` lub chwilowe wyłączenie agenta i `Rigidbody`, jak prościej.
GroundSlam: przy `Grant` ustawia `PlayerController.JumpEnabled = true`; `Execute` = skok (symulacja
`Jump` przez publiczną metodę `PlayerController.RequestJump()`, którą dodaje Combat: ustawia
`jumpRequested = true`), a w `OnLanded` zadaje obrażenia w promieniu. Dopóki `RequestJump` nie
istnieje, Progression używa `Input` spacji przez `JumpEnabled` i tylko nasłuchuje `OnLanded`.

### 3.11 Boss
`EnemyHealth` dostaje `[SerializeField] bool isBoss` + `public bool IsBoss` (Combat). GameManager
słucha `OnEnemyDied` i gdy `IsBoss` ⇒ `RaiseRunWon`.

## 4. Liczby (z dokumentu projektowego, sekcje 5 i 8)

| Obiekt | HP | Obrażenia | Windup | Prędkość | XP |
| --- | --- | --- | --- | --- | --- |
| Gracz | 50 (prefab) | atak 2 (dziś 1 w prefabie: zmiana w WIRING) | | 8 m/s, dash 20 m/s na 4 m, cooldown 0,8 s | |
| Slime | 6 | 10 | 0,6 s, okno trafienia 0,2 s, cooldown ataku 1,2 s | 3 m/s | 10 |
| Pająk (P1) | 4 | 8 | 0,4 s | 6 m/s | 15 |
| Strzelec (P1) | 4 | 6/pocisk | 0,8 s | 2 m/s | 20 |
| Boss (P1) | 120 | 15 | 1,0 s | 4 m/s | 0 |
| Serce | | leczy 15 | | | |

Progi XP: `40 + 13 * HeroLevel`. 12 level upów w runie. Fale wg sekcji 8.1 dokumentu (dane dla
level designu, nie dla kodu).

Skille (tier 0 / ulepszenia / ewolucja):

| Skill | Slot | Baza | Ulepszenie 1, 2 | Ewolucja |
| --- | --- | --- | --- | --- |
| Spin | Q/E/R | 1 s, promień 2,5 m, 2 dmg co 0,25 s, cd 5 s | +0,5 m, +1 dmg/tick | tornado: ciągnie wrogów 2 s |
| Charge | Q/E/R | 6 m w linii, 5 dmg, przebija, cd 4 s | +2 m, +2 dmg | ślad ognia 3 s, 2 dmg/s |
| GroundSlam | Space | promień 3 m, 4 dmg, odrzut 3 m, cd 6 s | +1 m, +2 dmg | ogłuszenie 1 s w promieniu |
| AxeThrow | Q/E/R | 10 m, wraca, 3 dmg w obie strony, cd 3 s | +3 m, +1 dmg | dwa topory, 30° |
| WarCry | Q/E/R | promień 4 m, odrzut 4 m, ogłuszenie 1 s, 0 dmg, cd 8 s | +1 m, +0,5 s | 3 dmg, promień 6 m |

Wartości wpisuje się w assety `SkillDefinition` (WIRING), kod czyta `SkillTier`.

## 5. Weryfikacja

- `bash Tools/compile-check.sh` → `OK`. Kompiluje Assembly-CSharp Roslynem z Unity 6000.0.2f1
  (projekt jest na 6000.0.45f1; ta sama linia API, różnica bez znaczenia dla tych skryptów).
- Nie ma testów automatycznych; test ręczny wg `Docs/WIRING.md` w scenie `Michal.unity`.

## 6. Raport z pracy (format dla każdego właściciela)

1. Pliki utworzone/zmienione.
2. Kroki w edytorze Unity: komponent → obiekt/prefab → pole → wartość; dla grafu Behavior: który węzeł,
   gdzie w grafie, jakie zmienne blackboardu.
3. Odstępstwa od kontraktu (jeśli były) i dlaczego.
4. Co nie działa / co wymaga decyzji.

## 7. Dodatki P1 (14 października 2026)

| Plik | Właściciel | Rola |
| --- | --- | --- |
| `Scripts/Game/ColliderTags.cs` | koordynator | tag przez Rigidbody/root, bo collidery gracza są na dzieciach |
| `Scripts/Enemies/SpiderController.cs` | Wrogowie P1 | Perched → Dropping (parabola, nietykalny) → Chasing; atak przez EnemyMeleeAttack |
| `Scripts/Enemies/ShooterController.cs`, `Projectile.cs`, `ProjectilePool.cs` | Wrogowie P1 | dystans 8 m, windup 0,8 s, pocisk 10 m/s / 6 dmg z puli |
| `Scripts/Enemies/SpiderMotherBoss.cs` | Boss | OnCeiling → Drop → Charge → Summon → ReturnToCeiling; `IsBoss` kończy run |
| `Scripts/Combat/HitStop.cs`, `HitFeedback.cs`, `DamageFlash.cs`, `Scripts/CameraShake.cs`, `Scripts/UI/ScreenFlash.cs` | Boss | feel; HitStop używa timeScale 0,0001, żeby nie nadpisać pauzy |
| `Scripts/Skills/Behaviours/AxeThrowSkill.cs`, `WarCrySkill.cs` | Skille P1 | rejestrowane w `SkillUser.Awake` |
| `Assets/Editor/*` | koordynator | automaty podpięcia i testów, patrz CLAUDE.md „Jak testować” |

Decyzje: `PlayerController.ForceDash` (szarża), skok tylko przez `RequestJump`, spawny stref dociągane do NavMesha
(`NavMesh.SamplePosition` 2,5 m) i niszczone w `WaveZone.OnDestroy`, poziomy w skali 1 z NavMeshem przepieczonym
z colliderów (`P1Wiring_NavMesh`).

## 8. Kontrakty tury „dopracowanie” (grafika, animacja, AI)

- **Visual root:** każdy prefab wroga ma dziecko `Visual`; modele proceduralne (prymitywy, nogi) żyją pod nim, animacja
  (squash/stretch, kołysanie) skaluje i obraca TYLKO `Visual`. Collidery zostają na roocie wroga, nigdy na `Visual`
  (dziś Spider/Shooter/Boss mają collider na prymitywie: grafika przenosi go na root przy podmianie bryły).
  `PlaceholderVisual` tworzy kulę tylko, gdy nie ma żadnego renderera.
- **Telegrafy:** AI woła `Telegraph.Circle/Cone/Line(..., seconds)` w chwili decyzji o ataku (`seconds` = windup);
  grafika implementuje `ITelegraphRenderer` i ustawia `Telegraph.Renderer` (komponent w scenie). Bez implementacji
  wywołania są no-op.
- **Zdarzenia dla animacji (AI ich nie usuwa):** `EnemyMeleeAttack.OnWindupStarted/OnAttackFinished`,
  `ShooterController.OnWindupStarted/OnShot`, `SpiderController.Current` (odpytywane), `SpiderMotherBoss.OnStateChanged(string)`,
  `SpiderMotherBoss.OnTelegraph(Vector3 punktLądowania)`, `Health.OnHealthChanged`, `EnemyHealth.OnEnemyDied`,
  `NavMeshAgent.velocity` (cykl chodu). Nowe stany AI mogą dojść, istniejące zdarzenia zostają.
- **Slime bez grafu Behavior:** ruch slime'a przechodzi do `SlimeController` w kodzie (stado, otaczanie, odskok),
  `BehaviorGraphAgent` w `Slime Enemy.prefab` wyłączony przez skrypt wiringu AI; `EnemyMeleeAttack` zostaje do ciosu.
- **Post-processing:** profil Volume per poziom przez `LevelAmbience`; kamera musi mieć `renderPostProcessing = true`
  (ustawia skrypt wiringu grafiki, nie edytujemy `PC_Renderer.asset`). Telegrafy jako płaskie quady, nie decale URP.
- **Zdarzenia tury 2 (gracz):** `DamagingBlade.OnHit(Vector3 punkt)`, `SkillUser.OnSkillExecuted(SkillKind, SkillTier)`,
  `CameraShake.ShakeScaled(int dmg)`, `CameraController.SetZoom/PunchZoom/ZoomMultiplier`. Animacje skilli ruszają tylko
  `Body` gracza (dziecko roota); root obraca `LookAtCursor` i dash.
- **Fale w pulsach:** `WaveZone` spawnuje połowę każdego wpisu od razu, resztę po `secondPulseDelay` (0,8 s); każdy spawn
  podnosi `OnEnemySpawned`, więc `LevelEnemyTracker` liczy poprawnie. Widoczny punkt spawnu jest przesuwany za róg
  (nie dla pająka i bossa). Każdy wróg dostaje `SpawnEntrance` (0,45 s bez ruchu i ataku).
- **UI tury 2:** pasek HP gracza to `HealthBarView` w `Game UI` (stary `Status Bar` wyłączony, `UIManager` zostaje);
  pasek bossa i paski nad wrogami to osobne menedżery w scenie; wszystkie animacje UI na czasie rzeczywistym.
- **Start i pauza (runda 3):** `GameManager.IsRunStarted/StartRun()/OnRunStarted`, pole `autoStart` (false w scenie: gra czeka
  na `StartMenu`). `PauseMenu` (Esc) pauzuje tylko w trakcie runu przy `timeScale > 0`; wznowienie przywraca zapamiętany
  `timeScale`. Oba menu wyłączają `PlayerController` na czas pokazania, bo ten czyta input także przy `timeScale = 0`.
  Nazwa gry w jednym miejscu: `GameTitle.Name` („Głębia”, tytuł roboczy).
- **Dekoracje:** `CaveDecor` na prefabie poziomu generuje w `Start` placeholdery (pajęczyny, kokony, gniazdo, szkielet,
  pająki-propy) z seeda = numer poziomu, bez colliderów, pod dzieckiem `Decor`; pola `*Prefab` do podmiany na modele.
- **Audio otoczenia:** `AmbienceController` + `ProceduralAmbienceBank` (obiekt `Ambience`), miks per `OnLevelStarted`,
  cisza przed bossem na poziomie 8, wyciszenie po śmierci/wygranej; pola `AudioClip` do podmiany na nagrania.
