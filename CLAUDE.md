# LD57 – roguelite akcji w jaskini (Unity 6)

Gra z Ludum Dare 57 (temat „Depths”, 4–7 kwietnia 2025), rozwijana post-jam.
Wojownik schodzi w otchłań przez 8 poziomów, wybija fale potworów, zdobywa levele
i składa zestaw 3 aktywnych skilli. Śmierć = restart od zera, brak meta-progresji.

Pełny dokument projektowy (esencja, mechanika, pętle, balans, backlog):
https://claude.ai/code/artifact/bc841552-066c-461c-905b-0742f3130763
Dokument jest źródłem prawdy dla designu. Ten plik jest źródłem prawdy dla kodu i workflow.

## Środowisko

- Unity **6000.0.45f1**, URP 17, Unity Behavior 1.0.8, AI Navigation 2.0.6, DOTween, TextMesh Pro, uGUI.
- Platforma: PC Windows, klawiatura + mysz. Stary Input Manager (nie Input System).
- Git LFS dla modeli, tekstur i audio (`.gitattributes`). Po klonie: `git lfs pull`.
- Zespół: Kosmik (kod, AI, walka, UI), hajtuszko (poziomy, modele, materiały, shadery).
  Remote: `https://github.com/Kosmik123/LD57`, branch `master`.

## Sterowanie (osie z `ProjectSettings/InputManager.asset`)

| Akcja | Oś / przycisk | Uwagi |
| --- | --- | --- |
| Ruch | `Horizontal`, `Vertical` (WSAD) | względem `forwardProvider` (kamera) |
| Atak | `Fire1` (LPM) | 0,3 s, włącza collidery `damagingObjects` |
| Dash | `Dash` (Left Shift) | tylko na ziemi, blokuje obrót za kursorem |
| Skok | `Jump` (Space) | **ma zniknąć jako wolny ruch**; Space = skill „uderzenie w ziemię” po jego wybraniu |
| Skille | Q, E, R | **do dodania** (3 sloty) |

## Struktura `Assets/`

```
Scripts/
  PlayerController.cs      ruch, skok (coyote time, input buffer), dash, atak; Rigidbody, FixedUpdate
  Player.cs                SceneSingleton, wpisuje gracza do globalnego blackboardu Behavior
  LookAtCursor.cs          obrót postaci za kursorem (RotationSpeed zmieniany podczas ataku)
  CameraController.cs      kamera z góry, obraca się wokół `Camera Center`
  PlayerAnimations.cs, RotateWithMovement.cs, SmearController.cs (smuga dasha, shader URPSmearWithTrailDelay)
  Combat/
    Health.cs              HP, OnHealthChanged(int delta), OnDied, IsInvulnerable
    EnemyHealth.cs         statyczne OnEnemyDamaged(EnemyHealth, int) dla UI
    DamagingBlade.cs       OnTriggerEnter → Health.ChangeHealth(-damage) po tagu (`damagedTags`)
  UI/UIManager.cs          pasek HP gracza (Image.fillAmount)
  Game/      GameEvents (hub zdarzeń), GameManager (śmierć, wygrana, restart), RunState, ColliderTags
  Levels/    LevelManager (wszystkie poziomy w scenie naraz, przenosi gracza na LevelEntry przez kratę), LevelEnemyTracker, ExitGate
  Waves/     WaveZone (trigger raz → spawn wpisów na NavMeshu → serce po ostatnim → OnWaveCleared)
  Enemies/   EnemyMeleeAttack (windup, autoAttack), EnemyDeath, SpiderController, ShooterController, Projectile(Pool), SpiderMotherBoss
  Progression/Experience (XP 40+13n), Skills/ (SkillDefinition SO, SkillSlots, SkillUser, Behaviours/: Spin, Charge, GroundSlam, AxeThrow, WarCry)
  Combat/    DamagingBlade, DashInvulnerability, HitStop, HitFeedback, DamageFlash; UI/ LevelUpScreen, SkillHud, DeathScreen, ScreenFlash
Editor/      P0Wiring, P1Wiring_*, P2Wiring_*, P3Wiring_*, P4Wiring_*, P5Wiring_*, P5Build, WireAll, P0SmokeTest(+Full, +Combat)
Skills/      5 assetów SkillDefinition;  Prefabs/Enemies: Spider, Shooter, Projectile, Boss Spider Mother (placeholdery z prymitywów)
  Custom Behavior Graph Actions/  RandomPointInRangeAction, RotateTowardsAction (Unity Behavior)
WorldsSpaceDamageIndicators.cs  liczby obrażeń w świecie, ObjectPool + DOTween (leży w Assets/, nie w Scripts/)
Enemy Behaviors/
  Global.asset             globalny blackboard (zmienna `Player`)
  Slime 1/                 Slime Behavior.asset (graf), Slime Settings.asset, SlimeState.cs (Wandering/Chasing/Attacking)
Prefabs/
  Player.prefab            HP 50, moveSpeed 8, jumpSpeed 8, dash 20 m/s na 4 m, attackDuration 0,3, damage 1
  Enemies/_Enemy Base.prefab (tag Enemy), Slime Enemy (3 HP), Spider (4 HP), Shooter (4 HP), Boss Spider Mother (120 HP)
  Levels/Level 1..8.prefab + Nav Meshes/NavMesh Level N.asset, _Level Base.prefab
  UI/UI Canvas, Status Bar, Stats
Scenes/
  Michal Scene/Michal.unity   JEDYNA scena gry (stara GameScene usunięta 14 października 2026)
Models/Cave.fbx, Materials/ (Ground.shadergraph, Blur, smear shader), Volumetric/, Settings/ (RP assets PC i Mobile)
Bipolar/                   własna biblioteka Kosmika: SceneSingleton, SerializedInterface, RandomFloat
NaughtyAttributes/, SerializeReferenceExtensions/, RPG Tiny Hero Duo/  paczki zewnętrzne – nie edytować
```

## Konwencje kodu

- C#, `[SerializeField] private` + property do odczytu, `[Foldout]`/`[ShowNonSerializedField]` z NaughtyAttributes.
- Zdarzenia jako `public event System.Action<...>`; UI subskrybuje w `OnEnable`, odpina w `OnDisable`.
- Input czytany w `Update`, fizyka w `FixedUpdate`. Gracz jest na Rigidbody, nie na CharacterController.
- Wrogowie: Unity Behavior (graf + blackboard), nawigacja NavMeshAgent. Nowe akcje grafu do
  `Scripts/Custom Behavior Graph Actions/`.
- Tagi: gracz trafia obiekty z tagiem `Enemy`. Wrogowie mają ranić gracza przez `DamagingBlade`
  z tagiem `Player` w `damagedTags` (jeszcze nie ma).
- Stany i konfiguracja jako ScriptableObject lub blackboard, nie magiczne liczby w kodzie.
- Nie commitować `Library/`, `Logs/`, `UserSettings/` (są w `.gitignore`). Pliki `.meta` zawsze razem z assetem.

## Znane pułapki

- **NavMesh nie skaluje się z prefabem poziomu.** Poziomy to pierścienie wokół środka kamery; NavMesh
  `NavMesh Level N.asset` jest pieczony w skali 1. Scena miała poziom 1 w skali 1,5 i gracz spadał w pustkę.
  Poziom 1 jest teraz w skali 1 (prefab). Skalować tylko w FBX i przepiekać NavMesh.
- **Poziomy używają siatek z `Assets/Models/Cave_new.fbx`** (eksport z 9 maja 2025, nazwy `Level_N.00x`),
  podpiętych przez `P3Wiring_NewCave`. Stary `Cave.fbx` (kwiecień) zostaje w repo, ale nic na niego nie wskazuje.
  Po kolejnym eksporcie jaskini: podmienić `Cave_new.fbx` i uruchomić `bash Tools/unity-batch.sh newcave`
  (przepina siatki po numerze poziomu, przepieka NavMeshe, rozstawia poziomy 1–8 od nowa, czyli NADPISUJE
  ręczne rozstawienie).
- **Geometria jamowej jaskini:** poziom 1 ma ściany nad podłogą, poziomy 2–8 to bryły leżące POD podłogą (podłoga = górna
  powierzchnia, ściany idą w dół). Poziome raycasty nad podłogą na poziomach 2–8 nic nie trafiają; pochodnie stoją tam na
  słupach przy krawędzi NavMesha (`P2Wiring_Ambience`, `WallLightSpot.Standing`). `Physics.queriesHitBackfaces = true`
  ustawia `GameManager.Awake` (normalne częściowo na zewnątrz). Diagnostyka w batchu: `bash Tools/unity-batch.sh exec P9Diag.WallProbe`.
- Collidery gracza są na dzieciach bez tagu; tag `Player` jest na roocie. W triggerach używać
  `ColliderTags.IsPlayer(other)`, nie `other.CompareTag`.
- Zawieszony Unity w batchu (log stoi na starcie): `taskkill //F //IM Unity.exe` i uruchomić ponownie.
- `PlayerController.Invoke(nameof(ResetJump), 0.4f)` i `Invoke(StopAttack)` – zmiana timeScale
  (pauza na ekran kart) zatrzyma te Invoke'i; przy pauzie użyć `Time.timeScale = 0` świadomie.
- `DamagingBlade` zakłada, że collider ma `attachedRigidbody`; obiekt bez Rigidbody rzuci NullReference.
- Jedyna scena to **Michal.unity**; `GameScene.unity`, `PlayerDash.cs` i `SimpleCharacterMovement.cs` zostały usunięte.

## Kluczowe decyzje designu (skrót, szczegóły w dokumencie)

- Poziom = droga do kraty zejścia z 2–3 strefami fal; krata otwiera się po wybiciu wszystkich fal.
- XP za zabójstwa, ok. 12 level upów na run, level up = pauza + 3 losowe karty.
- 3 sloty skilli (Q, E, R) + uderzenie w ziemię na Space. Skill ulepszany 3 razy, trzecie = ewolucja
  (wygląd, obrażenia, zasięg). Pula: wirowanie, szarża, rzut toporem, krzyk bojowy, uderzenie w ziemię.
- Dwie klasy od rundy 10: Wojownik (miecz, Spin/Charge/AxeThrow/WarCry/GroundSlam) i Mag (pocisk, Fireball/FrostNova/
  ArcaneOrbs/Blink/ChainLightning); wybór w menu startowym.
- Wrogowie: slime (korytarze), pająk (spawn na ścianie, zeskok, od poziomu 3), strzelec (komory,
  od poziomu 5), boss: matka pająków na poziomie 8. Atak zawsze z zapowiedzią; dash daje nietykalność.
- Serce (+15 HP) po ostatnim wrogu każdej fali. Sygnały końca fali tylko w świecie (krata z hukiem), bez HUD.
- Liczby balansu (HP, obrażenia, cooldowny, XP) są **propozycjami** w dokumencie; po zmianie w prefabach
  zaktualizować dokument.

## Stan P1 (14 października 2026)

Pająk, strzelec, boss (matka pająków), rzut toporem, krzyk bojowy, hit stop, błysk, wstrząs kamery: kod jest,
prefaby placeholderowe (prymitywy) tworzy `wireall`. Poziomy 2–8 mają układ liczony automatem z NavMesha.
Pełny test (`smokefull`) przechodzi 8 poziomów do wygranej: PASS, 62 kroki, 0 błędów (14 października 2026).
Rozstawienie poziomów 2–8 liczone jest w tymczasowej scenie na żywym NavMeshu (`P1Wiring_Levels`), po
przepieczeniu NavMeshy (`P1Wiring_NavMesh`); kolejność: `navmesh` → `levels` → `smokefull`. Bez kodu nadal: audio, modele wrogów, gradient
światła, menu startowe, nazwa gry.

## Tura „dopracowanie” (od 14 października 2026)

Plan w dokumencie projektowym, sekcja 16 (grafika, animacja, AI) z kierunkiem artystycznym z referencji (16.D).
Zrobione w turze 1: bryły wrogów z prymitywów pod `Visual` (slime z oczami, pająk i boss z 8 nogami, strzelec
ze świecącym rdzeniem), telegrafy na ziemi (`Telegraph` + `TelegraphRenderer`), post-processing i mgła per poziom
(`LevelAmbience`), światła z migotaniem, animacja proceduralna (`Animation/*Animator`, `VisualRig`, `HitReaction`),
AI: slime w stadzie bez grafu Behavior (`SlimeController`, otaczanie, odskok, alarm `EnemyAlert`), pająk zasadzka
i ucieczka, strzelec kiting z linią wzroku, boss z fazami 60/30 % (`OnPhaseChanged`). Liczby obrażeń mniejsze,
z rozrzutem i zanikaniem. Galeria zrzutów do feedbacku: artifact „LD57 Zrzuty z testów” (publikowany po każdej rundzie).
Zrobione w turze 2 (`P4Wiring_*`, w `Wire ALL`): smuga miecza i iskry (`Player/SwordTrail`), powidoki dasha
(`DashAfterimage`), animacje skilli na `Body` (`PlayerSkillAnimator`), czerwona winieta przy niskim HP, kamera
z wyprzedzeniem i zoomem (`CameraController.lookAhead/ZoomMultiplier/PunchZoom`, punch przy trafieniu bossa),
HUD HP z opóźnionym ubytkiem (`UI/HealthBarView`, stary `Status Bar` wyłączony), pasek bossa, paski HP nad wrogami,
„!” przy alarmie, karty z animacją, puls ikon, drżenie kraty, puls serca, fale w dwóch pulsach (0,8 s) z wejściem
wrogów spod ziemi (`Waves/SpawnEntrance`) i slow-mo po fali (`WaveDirector`).
Runda 3 (`P5Wiring_*`, w `Wire ALL`): menu startowe z nazwą gry (`GameTitle.Name` = „Głębia”, roboczo) i sterowaniem,
pauza na Esc, dekoracje poziomów z prymitywów (`Levels/CaveDecor`: pajęczyny, kokony, gniazdo, szkielet, pająki-propy
wg gradientu z sekcji 11 dokumentu), audio otoczenia (`Audio/AmbienceController`: kapanie, szelest, warkot, cisza przed
bossem), build Windows z batcha (`P5Build`). Usunięte: `GameScene.unity`, `PlayerDash.cs`, `SimpleCharacterMovement.cs`.
Runda 4 (po playteście hajtuszko, 14 października 2026: „mało przeciwników, mają być masy; trafiony ma mignąć na biało
i odskoczyć”): hordy (`P6Wiring_Balance`: poziom 1 = 18 slime'ów w 2 strefach, poziom 8 = 54 + boss; slime 3 HP / 4 XP,
pająk 4 HP / 9 XP, strzelec 4 HP / 12 XP), `DamageFlash` podmienia materiał na biały Unlit (0,1 s), `Combat/Knockback`
animuje odrzut 0,9 m (boss 0,15×), `VFX/HitSplash` rozbryzg w kolorze wroga przy każdym trafieniu, promień spawnu
rośnie z liczebnością. Tabela składu w `P1Wiring_Levels` i `P6Wiring_Balance` musi być ta sama.
Runda 5 (feedback: „6 wrogów naraz, mają się spamować z okolicy; nie mogę wbić levela; slime ma się rozpłaszczać
i skakać”): `WaveZone` spawnuje strumieniem (`maxAlive` = 7 + numer poziomu, co 0,9 s po 2 wrogów z punktów NavMesha
6–11 m od gracza, najlepiej zza rogu; testy ustawiają `WaveZone.InstantSpawnForTests`), liczebności ×1,3 (poziom 1 = 24,
poziom 8 = 63 + boss), slime 5 XP. Slime: `EnemyMeleeAttack.leap` = rozpłaszczenie w windupie (0,5 s) z telegrafem
w miejscu lądowania, skok po łuku 0,35 s, zgniecenie w promieniu 1,3 m za 10; zasięg skoku 3,5 m. Mapa: `Cave_new.fbx`
podmieniony na jamowy `Cave.fbx` z pulpitu (Level_1..8, 186 KB) i zmigrowany przez `newcave`.
Runda 6: **wszystkie 8 poziomów stoi w scenie naraz** (`LevelManager.showAllLevels`, instancje w `Levels`), gracz widzi
głębsze poziomy w dole, przejście tylko przez kratę (teleport na `LevelEntry` kolejnego). Skutki: `ExitGate` otwiera się
tylko na `OnLevelCleared` własnego poziomu, `LevelAmbience` stosuje RenderSettings tylko dla bieżącego poziomu, testy
szukają stref/kraty/wejścia w `LevelManager.CurrentInstance` (helpery `Zones`, `CurrentTracker/Gate/Entry` w P0SmokeTest).
Ekran kart ma karencję 0,5 s na klik po otwarciu (`LevelUpScreen.inputGrace`), żeby klik z walki nie wybierał karty.
Na jamowej mapie poziomy leżą obok siebie na podobnej wysokości (nie jeden pod drugim), więc `WaveZone` odpala się
tylko, gdy jej poziom jest bieżący, uzbraja się 1 s po starcie i pierwsza strefa stoi ≥ 14 m od wejścia (`P1Wiring_Levels`).
Tryby batcha: `levelsforce` = `P1Wiring_Levels.RunAll` (NADPISZ wszystkie 8), test P0 trzyma gracza nietykalnego od hordy.
Migracja `newcave` kopiuje pozę węzła `Level_N` z FBX na `Model` i `Collider` prefabu (root w zerze), więc układ jest
jak w pliku: stos w dół (poziom 1 na y 0, poziom 8 na y −73). Dźwięk ciosu miecza: 3 nagrania `Assets/Audio/Sword/sword_N.wav`
(pocięte z pliku od hajtuszko) w `ProceduralSfxBank.attackVariants`, wpina `P6Wiring_SwordAudio`.
Runda 8: `Levels/FallGuard` (na `Level Manager`, wpina `P7Wiring_FallGuard`): spadnięcie > 4 m poniżej wejścia bieżącego
poziomu = powrót na ostatnie bezpieczne miejsce i −5 HP, więc nie da się zeskoczyć na niższe poziomy. `VFX/SkillImpactVfx`:
wir dostaje obracający się łuk ostrza (mesh w kodzie) i błysk pierścienia, uderzenie w ziemię falę uderzeniową, pęknięcia,
pył, wstrząs i hit stop. Poziom 1 ma 2 pająki do testów (tabela składu: (24, 2, 0)).
Runda 9 (feel): `Player/PlayerFeel` (pył kroków i lądowania, smugi i stretch dasha, anticipation + wypad ciosu, krople
przy niskim HP), dłuższa smuga miecza z białym rdzeniem, `VFX/DeathEffects` (slime: galareta + kałuża, pająk: odłamki
nóg, strzelec: błysk rdzenia + światło, boss: wszystko ×3 + wstrząs), `VFX/WorldVfx` (serce, pocisk ze smugą i iskrami,
pył i błysk kraty), puls telegrafu na końcu windupu, `UI/XpBarView` (zastępuje pasek XP ze SkillHud), `UI/LevelUpBurst`
(„LEVEL UP” nad graczem, złoty pierścień, iskry, złoty błysk ekranu), `UI/HitDirectionIndicator` (czerwony klin w stronę
źródła obrażeń), `UI/WaveClearedToast` („Czysto” w świecie), liczby obrażeń: mocny cios ≥ 5 i „N!” przy zabójstwie.
Runda 10: **druga klasa Mag** (`Classes/PlayerClass` SO: `Assets/Classes/Warrior.asset`, `Mage.asset`; `PlayerClassApplier` na
graczu podmienia model (Female z paczki, tint niebieski), wyłącza hitboxy miecza (`PlayerController.MeleeHitboxes`), włącza
`Player/RangedAttack` z pociskami `PlayerBolt`, podmienia pulę skilli: Fireball, FrostNova, ArcaneOrbs, Blink (Space),
ChainLightning w `Assets/Skills/Mage/`; wybór w menu startowym, `ClassSelection.ChosenName` przeżywa restart; domyślnie
Wojownik, więc testy bez zmian). Poziomy: strefy przesuwane do sal (ocena 12 punktów na okręgu 6 m + krawędź ≥ 3 m),
spawny ≥ 1,5 m od krawędzi, trigger dopasowany do sali, pochodnie (1–4), grzyby (5–6), stalagmity, rumowisko, kły na 8,
słupy przy kracie (`P1Wiring_Levels`, `P2Wiring_Ambience`, `Levels/Torch`). Teren: proceduralna skała
(`Assets/Graphics/Generated`, `VFX/ProceduralTextures`) i 4 materiały `Assets/Materials/Cave/Cave_{{Warm,Grey,Cold,Violet}}`
na `Model` poziomów (`Ground.mat` zostaje jako asset); post: rozbudowane presety `LevelAmbience` (bloom, kontrast, winieta,
grain, chromatic, lift/gamma/gain), `VFX/PostFxPulse` (puls przy obrażeniach, level upie, śmierci), mgła wolumetryczna
była już w `PC_Renderer`.
Następne: feedback z galerii, balans (boss, poziom 8), podmiana placeholderów na assety, mgła wolumetryczna (feature na `PC_Renderer`).

## Backlog P0 (zamknięcie pętli)

Kod P0 jest napisany, podpięty automatem (`P0Wiring`) i przetestowany testem dymnym w Play Mode
(14 października 2026). Graf Behavior slime'a nie wymaga zmian: `EnemyMeleeAttack.autoAttack` atakuje sam.
Do zrobienia ręcznie: level design poziomów 2–8 (`Docs/WIRING.md` §5) i poprawa rozstawienia na poziomie 1.

1. Obrażenia od slime'a: `EnemyMeleeAttack` + `TriggerMeleeAttackAction` (kod jest, graf do podpięcia).
2. Śmierć gracza: `GameManager` + `DeathScreen` (kod jest, scena do podpięcia).
3. Nietykalność w dashu: `DashInvulnerability` (kod jest, komponent do dodania).
4. Fale i krata: `WaveZone`, `LevelEnemyTracker`, `ExitGate`, `HeartPickup` (kod jest, prefaby poziomów do rozstawienia).
5. Poziomy: `LevelManager` (kod jest, 8 prefabów do wpisania).
6. XP i karty: `Experience`, `LevelUpScreen`, `SkillSlots`, `SkillUser`, Spin/Charge/GroundSlam (kod jest, assety i UI do zrobienia).
7. Space = GroundSlam przez `PlayerController.RequestJump()`; oś `Jump` nie jest już czytana. AxeThrow i WarCry bez kodu (P1).

## Architektura i kontrakty

`Docs/ARCHITECTURE.md` jest źródłem prawdy dla modułów, własności plików i kontraktów
(`GameEvents`, `Health`, `EnemyHealth`, `PlayerController`, `WaveZone`, `SkillSlots`, `SkillDefinition`).
Sygnatur publicznych z sekcji 3 tego pliku nie zmieniamy bez zapisania tego tam.
Kroki wymagające edytora Unity (prefaby, graf Behavior, assety) są spisane w `Docs/WIRING.md`.

## Jak testować

- **Kompilacja bez Unity:** `bash Tools/compile-check.sh` (Roslyn z Unity Hub + DLL-ki z `Library/`).
  Zwraca `OK` albo listę błędów CS, osobno dla Assembly-CSharp i Assembly-CSharp-Editor.
- **Unity w batchu** (Unity 6000.0.45f1 z Hub, aktywna licencja, zamknięty edytor), wszystko też w menu **LD57**:
  - `bash Tools/unity-batch.sh wireall` — pełne podpięcie P0 + P1 + P2 (`Assets/Editor/WireAll.cs`: gracz, wróg bazowy,
    serce, scena, UI, skille, pająk, strzelec, pocisk, boss, feel, poziomy 2–8, klimat, audio, VFX).
    **WireAll to bootstrap: bezpieczny do ponownego uruchomienia, nigdy nie nadpisuje istniejącej pracy**
    (poziomy ze strefami, ręcznie wstawione EffectPrefab, istniejący LevelAmbience są pomijane). Nadpisanie
    tylko przez warianty menu z napisem NADPISZ.
  - `bash Tools/unity-batch.sh navmesh` — przepieka NavMesh poziomów 1–8 z colliderów (`P1Wiring_NavMesh.cs`),
    assety `Nav Meshes/NavMesh Level N (auto).asset`. Uruchamiać po każdej zmianie geometrii poziomu.
  - `bash Tools/unity-batch.sh levels` — rozstawia wejście, strefy, spawny i kratę na poziomach 2–8 z NavMesha
    (`P1Wiring_Levels.cs`). **Nadpisuje ręczne rozstawienie**, więc po pracy level designera już nie uruchamiać.
  - `bash Tools/unity-batch.sh smoke` — test dymny P0 (15 kroków z restartem po śmierci, `Logs/p0-smoke.txt`).
    Testy wyciszają audio (`AudioListener.volume = 0`); dźwięki proceduralne słychać tylko w Play w edytorze.
  - `bash Tools/unity-batch.sh smokefull` — pełny run: 8 poziomów, wszystkie strefy, boss, wygrana
    (`Logs/full-smoke.txt`).
  - `bash Tools/unity-batch.sh smokecombat` — test walki: każdy skill wykonany na prawdziwych wrogach, slime
    i strzelec ranią gracza, pająk zeskakuje, boss przechodzi cykl stanów (`Logs/combat-smoke.txt`) plus
    **zrzuty ekranu z kamery gry** w `Logs/shots/*.png` (do obejrzenia bez otwierania Unity).
    Testy `smoke*` działają z grafiką (bez `-nographics`), żeby zrzuty się renderowały.
  - `bash Tools/unity-batch.sh build` — build Windows x64 do `Builds/Win64/LD57.exe` (`P5Build`), wynik w `Logs/unity-build.log`.
- Otworzyć `Assets/Scenes/Michal Scene/Michal.unity`, Play, slime'y są już rozstawione.
- Nie ma testów automatycznych. Test Framework jest w manifeście, ale bez testów.
- Build: Settings/Build Profiles (PC_RPAsset dla PC).
