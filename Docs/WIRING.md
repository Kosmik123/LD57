# LD57 – podpięcie w edytorze Unity (po backlogu P0)

Kod z `Docs/ARCHITECTURE.md` jest napisany i kompiluje się, ale nic nie działa, dopóki nie zostanie
podpięte w edytorze. Ten plik to lista kroków w kolejności, w jakiej dają testowalny wynik.
Triggery sprawdzają tag przez `ColliderTags` (Rigidbody lub root), więc collidery-dzieci bez tagu są OK.

Scena robocza: `Assets/Scenes/Michal Scene/Michal.unity`. Po każdym etapie: Play i test z opisu.

## 0. Automat (zrobione 14 października 2026)

Sekcje 1–4 wykonuje skrypt edytorowy `Assets/Editor/P0Wiring.cs` (menu **LD57 > Wire P0** albo
`bash Tools/unity-batch.sh wire`). Jest idempotentny. Test dymny całej pętli: **LD57 > Smoke test P0**
albo `bash Tools/unity-batch.sh smoke` (wynik w `Logs/p0-smoke.txt`, 14 kroków: spawn fali, windup
i cios, nietykalność, serce, XP, karty, krata, poziom 2, śmierć). Ręczne kroki poniżej zostają jako
opis tego, co automat ustawia, i jako instrukcja dla poziomów 2–8.

Trzy decyzje podjęte przy automacie, ważne dla level designu:

1. **Poziom 1 jest w skali 1, nie 1,5.** Scena miała instancję poziomu w skali 1,5 w X/Z, ale NavMesh
   `NavMesh Level 1.asset` i pozycje slime'ów były ze skali 1. Poziom to pierścień wokół środka, więc
   przy 1,5 punkt wejścia (100, 1, 0) wisiał w pustce i gracz spadał. Skalować tylko w FBX, nigdy transformem.
2. **Poziomy są przepięte na majowy eksport `Assets/Models/Cave_new.fbx`** (decyzja z 14 października 2026,
   skrypt `P3Wiring_NewCave`, batch `newcave`: siatki po numerze poziomu, NavMesh, rozstawienie 1–8). Kwietniowy
   `Cave.fbx` zostaje w repo nieużywany.
3. **Strefy, wejście i krata na poziomie 1** są rozstawione automatycznie z pozycji pięciu slime'ów, które
   leżały w scenie (wejście = pozycja gracza, krata = najdalszy slime, dwie strefy w 1/3 i 2/3 drogi).
   To rozstawienie do testu, nie design: do poprawy ręcznie wg sekcji 5.

## 1. Wartości w istniejących prefabach (5 minut)

| Prefab | Komponent | Pole | Było | Ma być | Dlaczego |
| --- | --- | --- | --- | --- | --- |
| `Player.prefab` | DamagingBlade (hitbox ataku) | damage | 1 | 2 | 3 ciosy na slime'a (ARCHITECTURE §4) |
| `Player.prefab` | PlayerController | jumpEnabled | – | false | skok tylko przez skill GroundSlam |
| `Player.prefab` | PlayerController | dashCooldown | – | 0,8 | cooldown dasha |
| `_Enemy Base.prefab` | Health | maxHealth | 5 | 6 | 3 ciosy po 2 |
| `_Enemy Base.prefab` | EnemyHealth | experienceReward | – | 10 (slime) | XP |
| `_Enemy Base.prefab` | EnemyHealth | isBoss | – | false | boss ustawia true w swoim wariancie |

Tag `Player` jest już na roocie `Player.prefab` (warstwa `Player`, nr 6). Strefy fal, krata i serca
szukają tego tagu. Wrogowie mają tag `Enemy`, ale w projekcie nie ma warstwy `Enemy` (są: Player,
Ground). Jeśli skille używają maski `enemyLayers`, albo dodaj warstwę `Enemy` w Tags and Layers
i ustaw ją na `_Enemy Base.prefab`, albo zostaw maskę na `Everything` i polegaj na filtrze po tagu.

## 2. Combat

### Player.prefab
1. Dodaj komponent `DashInvulnerability` na root gracza. Pola `Player Controller` i `Health` uzupełniają się
   same z `GetComponent` w `Awake` (można też przeciągnąć ręcznie).
2. `PlayerController` > Dash: nowe pole `Dash Cooldown` = 0.8 (domyślne).
3. Pole `damage` w `DamagingBlade` używanym przez gracza (obiekty z `damagingObjects`): zmień z 1 na **2**.
4. `Jump Enabled` zostaje odznaczone (spacja nic nie robi do czasu GroundSlam).

### _Enemy Base.prefab
1. `Health`: `Max Health` = **6** (i `Current Health` 6).
2. Dodaj dziecko `Attack Hitbox` (pusty GameObject, pozycja przed wrogiem, np. (0, 0.5, 1) lokalnie):
   - Collider (Sphere/Box) z `Is Trigger` = true, rozmiar pokrywający zasięg ciosu (~1 m).
   - Rigidbody na hitboxie niepotrzebny: gracz ma Rigidbody, więc trigger go wykryje.
   - `DamagingBlade`: `Damaged Tags` = `Player`, `Damage` = **10**, `Knockback` = 0 (opcjonalnie 1).
   - **Domyślnie wyłączony** (odznacz checkbox aktywności obiektu).
3. Na root wroga dodaj `EnemyMeleeAttack`: `Windup` 0.6, `Active Time` 0.2, `Cooldown` 1.2, `Range` 1.5,
   `Damaging Object` = dziecko `Attack Hitbox`.
4. Na root wroga dodaj `EnemyDeath`: `Destroy Delay` = 0.5 (wymaga `Health` – jest).
5. `EnemyHealth`: nowe pole `Is Boss` (domyślnie false; true tylko dla bossa).
6. Warstwy: upewnij się, że macierz kolizji pozwala hitboxowi wroga (trigger) wykrywać gracza.

### Graf `Enemy Behaviors/Slime 1/Slime Behavior.asset`
1. Otwórz graf. Znajdź gałąź stanu `Attacking` (po przełączeniu `SlimeState`).
2. Wstaw węzeł **Action > Combat > TriggerMeleeAttack** (story: "[Self] attacks [Target] with windup"):
   - `Self` = zmienna `Self` (GameObject) z blackboardu grafu,
   - `Target` = zmienna `Player` z globalnego blackboardu (`Global.asset`) – typ GameObject.
   - Jeśli blackboard grafu nie ma `Self` jako GameObject, przypisz wbudowaną zmienną Self agenta.
3. Węzeł zastępuje dotychczasowy węzeł/wait w Attacking; po Success/Failure graf wraca do pętli
   decyzji (Chasing, gdy gracz poza zasięgiem). Failure = poza zasięgiem lub cooldown.
4. Zasięg ataku 1.5 m jest w `EnemyMeleeAttack.Range`; `Stopping Distance` agenta ustaw ≤ 1.2.
5. Slime Settings: prędkość 3 m/s (sekcja 4 ARCHITECTURE), jeśli jeszcze nie ustawiona.

Test: Play, podejdź do slime'a. Slime staje, „nadmuchuje się” 0,6 s, cios zdejmuje 10 HP z paska.
Dash przez cios: 0 obrażeń. Slime po 3 ciosach znika po 0,5 s.

## 3. Run flow

### 1. Scena Michal.unity

#### Game Manager
1. Nowy pusty obiekt `Game Manager`.
2. Dodaj `RunState` i `GameManager`.
3. `GameManager`: `Player Health` = komponent `Health` obiektu Player; `Run State` = ten sam obiekt (RunState).

#### Level Manager
1. Nowy pusty obiekt `Level Manager`, komponent `LevelManager`.
2. `Level Prefabs` (Size 8): Element 0..7 = `Level 1` … `Level 8` z `Assets/Prefabs/Levels/` w tej kolejności.
3. `Levels Parent` = istniejący obiekt `Levels` w scenie (przeciągnij z Hierarchy).
4. `Player` = Rigidbody obiektu Player.
5. Ręcznie położony poziom pod `Levels`: `LevelManager.Start` usuwa wszystkie dzieci `Levels` i ładuje poziom 1 z prefabu. Nic nie trzeba kasować ręcznie (możesz też usunąć go sam, żeby nie mylił w edytorze).
6. **Pięć slime'ów położonych ręcznie w scenie** (`Slime Enemy`, `Slime Enemy (1)`…`(4)`): usuń je. Są obiektami sceny, nie spawnami stref, więc dają XP, ale nie liczą się do kraty i przeżywają zmianę poziomu. Wrogowie mają istnieć tylko jako wpisy `WaveZone`.

#### UI: Death Screen
W `UI Canvas` utwórz:
- `Death Screen` (obiekt z komponentem `DeathScreen`, zawsze aktywny, bo nasłuchuje zdarzeń).
  - dziecko `Panel` (Image, półprzezroczyste czarne, rozciągnięte na cały ekran), **wyłączone** (domyślnie; i tak wyłącza go `Awake`).
  - w `Panel`: `Title` (TMP_Text, 64 pkt, wyśrodkowany, góra) i `Stats` (TMP_Text, 36 pkt, środek).
- `DeathScreen`: `Root` = `Panel`, `Title` = Title, `Stats` = Stats, `Game Manager` = obiekt `Game Manager`, `Run State` = ten sam RunState (lub puste, weźmie z GameManagera).

### 2. Prefab Heart (`Assets/Prefabs/Pickups/Heart.prefab`)
1. Pusty obiekt `Heart`, dziecko: model serca albo Sphere (skala 0.4, czerwony materiał).
2. Na rootcie: `SphereCollider` (Is Trigger = true, Radius 0.6), `Rigidbody` (Is Kinematic = true, Use Gravity off; potrzebny, żeby trigger reagował), `HeartPickup` (`Heal` 15, `Bob Amplitude` 0.15, `Bob Speed` 3, `Rotation Speed` 90).
3. Zapisz jako prefab.

### 3. Prefab poziomu (`_Level Base.prefab` i warianty `Level 1..8.prefab`)
Edytuj każdy wariant (lub `_Level Base` jeśli warianty są jego Prefab Variants).

1. **Root**: dodaj `LevelEnemyTracker`. `Level Number` = numer wariantu (Level 1 -> 1, … Level 8 -> 8). Dla `_Level Base` zostaw 1 i nadpisz w wariantach.
2. **Level Entry**: pusty dziecko-obiekt `Level Entry` z komponentem `LevelEntry`, ustaw w miejscu startu gracza, lekko nad podłogą (Y +0.5), na NavMeshu. Strzałka gizmo pokazuje kierunek.
3. **Exit Gate** przy zejściu:
   - Pusty obiekt `Exit Gate` z komponentem `ExitGate` i `BoxCollider` o Is Trigger = true (strefa zejścia, wielkość 2x2x2 tuż za kratą), `Trigger` = ten BoxCollider.
   - Dziecko `Bars` (krata, model lub Cube 3x3x0.3), `Closed Visual` = Bars.
   - Dziecko `Blocker` z `BoxCollider` (nie trigger) zamykającym przejście, `Blocking` = ten collider.
   - Dla dźwięku huku: `OnOpened` jest C# event, podepnie go audio (osobne zadanie).
4. **Wave Zone 1..3**: puste obiekty `Wave Zone 1`, `Wave Zone 2`, `Wave Zone 3` (2 strefy na poziomach 1–2, 3 na pozostałych wg designu), każdy:
   - `BoxCollider` Is Trigger = true, rozmiar ok. 6x3x6, na ścieżce gracza.
   - `WaveZone`: `Entries` – Size 1+; `Enemy Prefab` = np. `Slime Enemy`, `Spawn Point` = pusty Transform-dziecko (np. `Spawn A`) stojący na NavMeshu, `Count` 2–4. `Heart Prefab` = prefab `Heart`. `Spread Radius` 0.8.
   - Punkty spawnu (puste dzieci) umieść 8–12 m od triggera, żeby wrogowie nie pojawiali się na graczu.
5. Strefy muszą leżeć na ścieżce krytycznej: gracz nie może dojść do kraty bez przejścia przez każdą strefę (korytarz, wąskie gardło). Strefa pominięta = nigdy nie wykonana fala = `OnLevelCleared` nigdy nie padnie, krata zostanie zamknięta na stałe.
6. Poziom 8: dodaj bossa jako wpis w strefie (prefab z `Is Boss` zaznaczonym w `EnemyHealth`). Krata na poziomie 8 nic nie robi (`GoToNextLevel` ignoruje).
7. Zapisz prefab (Apply).

### 4. Test
Play w `Michal.unity`: ładuje się poziom 1, gracz staje na Level Entry, wejście w strefę spawnuje wrogów, po ostatnim wypada serce, po wszystkich strefach krata znika i zejście ładuje poziom 2. Śmierć -> ekran, R / Space restartuje scenę.

Test: Play, wejdź w strefę 1, fala się spawnuje, po wybiciu spada serce. Po wybiciu wszystkich stref
krata się otwiera, przejście przez nią ładuje poziom 2 i przenosi gracza na Level Entry.
Śmierć: czas staje, ekran z poziomem i czasem, R restartuje scenę.

## 4. Progression

### 1. Assety SkillDefinition

Folder `Assets/Skills/` (nowy). Menu: Create > LD57 > Skill Definition. Tiers = 4 elementy
(0 baza, 1 ulepszenie I, 2 ulepszenie II, 3 ewolucja). Pola tieru: TierName, Description, Cooldown, Damage,
Range, Duration, Knockback, Stun, EffectPrefab (opcjonalny). Ikony (Icon) opcjonalne.

#### Spin  (Kind = Spin, UsesSpaceSlot = false)
| Tier | Cooldown | Damage (na tick 0,25 s) | Range | Duration | Knockback | Stun |
| --- | --- | --- | --- | --- | --- | --- |
| 0 Baza | 5 | 2 | 2.5 | 1 | 0 | 0 |
| 1 Ulepszenie I | 5 | 3 | 3.0 | 1 | 0 | 0 |
| 2 Ulepszenie II | 5 | 4 | 3.5 | 1 | 0 | 0 |
| 3 Ewolucja (tornado) | 5 | 4 | 3.5 | 2 | 0 | 0 |

Ewolucja dodatkowo przyciąga wrogów o 0,5 m na tick (w kodzie).

#### Charge  (Kind = Charge)
| Tier | Cooldown | Damage | Range (dystans) | Duration (ślad) | Knockback | Stun |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 4 | 5 | 6 | 0 | 0 | 0 |
| 1 | 4 | 5 | 8 | 0 | 0 | 0 |
| 2 | 4 | 7 | 8 | 0 | 0 | 0 |
| 3 (ślad ognia) | 4 | 7 | 8 | 3 | 0 | 0 |

Ślad ognia: 2 dmg/s na wroga (w kodzie), co 1 m toru, przez Duration.

#### GroundSlam  (Kind = GroundSlam, **UsesSpaceSlot = true**)
| Tier | Cooldown | Damage | Range (promień) | Duration | Knockback | Stun |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 6 | 4 | 3 | 0.5 | 3 | 0 |
| 1 | 6 | 4 | 4 | 0.5 | 3 | 0 |
| 2 | 6 | 6 | 4 | 0.5 | 3 | 0 |
| 3 (ogłuszenie) | 6 | 6 | 4 | 0.5 | 3 | 1 |

(Duration = czas życia EffectPrefab + 1 s.)

#### AxeThrow i WarCry
Kod ich nie obsługuje (brak `AxeThrowSkill`/`WarCrySkill`). Można utworzyć assety (Kind = AxeThrow / WarCry),
ale wtedy karty pojawią się w ofercie i nic nie zrobią. Na razie NIE wkładaj ich do puli.
Wartości na przyszłość: AxeThrow 10 m / 3 dmg / cd 3, +3 m, +1 dmg, ewolucja dwa topory 30°;
WarCry r 4 m, odrzut 4, stun 1 s, cd 8, +1 m, +0,5 s, ewolucja 3 dmg r 6.

### 2. Gracz (prefab/obiekt Player w scenie)

- `Experience` (Progression/Experience): bez pól.
- `SkillSlots`: pole **Pool** = assety Spin, Charge, GroundSlam (i 5 gdy będą AxeThrow, WarCry).
- `SkillUser`: Slots = SkillSlots z gracza, Player = PlayerController, **Enemy Layers** = warstwa(y) wrogów
  (domyślnie Everything; kod filtruje po tagu `Enemy`, więc Everything też działa).
- Wróg musi mieć tag `Enemy` na obiekcie z `Health` (root) lub colliderze, i collider (nie tylko trigger-less brak).
- Pamiętaj: gracz musi mieć `Rigidbody` (Charge ustawia `linearVelocity`).

### 3. UI (Canvas Michala; scena musi mieć `EventSystem` – jest)

#### Level Up Screen
```
Canvas
  Level Up Screen        (komponent LevelUpScreen; zostaw włączony, root wyłącza się sam)
    Root                 (GameObject, pole `root`; pełnoekranowy półprzezroczysty Image + Horizontal Layout Group)
      Card 1 (Button + Image tło)
        Icon  (Image)
        Title (TMP_Text)
        Description (TMP_Text)
      Card 2 (jak wyżej)
      Card 3 (jak wyżej)
```
Pola: root = Root, Card Buttons = [Card1, Card2, Card3], Card Titles/Descriptions/Icons = odpowiednie dzieci
w tej kolejności, Slots = SkillSlots gracza, Player Health = Health gracza.
Skrypt LevelUpScreen musi być na obiekcie, który **nie jest** wyłączany (nie na Root). Klawisze 1/2/3 też wybierają.

#### Skill HUD
```
Skill HUD (komponent SkillHud)
  Slot Q, Slot E, Slot R, Slot Space   (każdy: root GameObject)
    Icon (Image)
    Cooldown (Image: Image Type = Filled, Fill Method = Radial 360, półprzezroczysty czarny)
    Key (TMP_Text)
    Tier (TMP_Text)
  XP Bar: tło + XP Fill (Image Filled, Horizontal)
  Hero Level (TMP_Text)
```
Pola: Views[0..3] = Q, E, R, Space (root/icon/cooldownFill/key/tier), Slots, Experience (z gracza),
Xp Fill, Hero Level.

Test: po czwartym slime'ie pauza i 3 karty, wybór klawiszem 1/2/3, skill pod Q. Druga karta tego
samego skilla podnosi tier. GroundSlam pod spacją, skok i obrażenia przy lądowaniu.

## 5. Level design na 8 poziomach (hajtuszko)

Dla każdego prefabu `Level N` (wariant `_Level Base`):

1. `LevelEnemyTracker` na roocie: `levelNumber = N`.
2. `Level Entry` w komorze wejściowej, 2 m od miejsca, gdzie gracz „spada” z poprzedniego poziomu.
3. `Exit Gate` w ostatniej komorze: krata (model) + BoxCollider blokujący + BoxCollider trigger za kratą.
4. Strefy fal wg reguły z dokumentu (sekcja 11): poziomy 1–2 dwie strefy (korytarz, komora z kratą),
   poziomy 3–8 trzy strefy (korytarz, komora, komora z kratą). Każda strefa: BoxCollider `isTrigger`
   przez całą szerokość przejścia (żeby nie dało się jej ominąć), punkty spawnu na podłodze na NavMeshu,
   poza zasięgiem wzroku z miejsca wejścia w strefę (za rogiem, 6–10 m).
5. Skład fal (slime / pająk / strzelec) z dokumentu, sekcja 8.1:

| Poziom | Strefy | Slime | Pająk | Strzelec |
| --- | --- | --- | --- | --- |
| 1 | 2 | 4 | 0 | 0 |
| 2 | 2 | 7 | 0 | 0 |
| 3 | 3 | 6 | 3 | 0 |
| 4 | 3 | 7 | 5 | 0 |
| 5 | 3 | 6 | 5 | 3 |
| 6 | 3 | 8 | 6 | 4 |
| 7 | 3 | 9 | 8 | 5 |
| 8 | 3 + boss | 10 | 9 | 6 |

Do czasu, aż pająk i strzelec istnieją (P1), wpisz ich liczby jako slime'y.

6. NavMesh: po każdej zmianie geometrii przepiec `NavMesh Surfaces` w prefabie. Skala poziomu w
   Unity = 1; skalowanie tylko w FBX.

## 6. Kolejność testu całości

1. Sekcja 1 + 2: walka ze slime'em w scenie jak dziś.
2. Sekcja 3 na poziomie 1: strefy, krata, przejście na poziom 2, śmierć i restart.
3. Sekcja 4: level up po czwartym slime'ie, karta, skill.
4. Poziomy 2–8 wg sekcji 5.

## 7. P1: automat i skrypty

Wszystko z P1 podpina jeden przebieg: menu **LD57 > Wire ALL (P0 + P1)** albo `bash Tools/unity-batch.sh wireall`
(kolejno: P0Wiring, P1Wiring_Skills, P1WiringEnemies, P1WiringBoss, P1Wiring_Levels dla poziomów 2–8).
Pełny test przez 8 poziomów i bossa: **LD57 > Smoke test FULL** albo `bash Tools/unity-batch.sh smokefull`
(wynik w `Logs/full-smoke.txt`).

Poziomy 2–8 (`Assets/Editor/P1Wiring_Levels.cs`): wejście, strefy, punkty spawnu i krata są liczone z upieczonego
NavMesha (wejście = punkt NavMesha w kierunku +X od środka, krata = najdalszy osiągalny punkt po ścieżce, strefy
w 1/3 i 2/3 ścieżki, spawny 7–10 m dalej na NavMeshu). Skład fal z dokumentu §8.1, boss w ostatniej strefie
poziomu 8. To rozstawienie testowe: level designer poprawia pozycje ręcznie, a ponowne uruchomienie
`P1Wiring_Levels` nadpisuje je (nie uruchamiać po ręcznych poprawkach).

### Skille P1: rzut toporem i krzyk bojowy

1. Skrypt `P1Wiring_Skills` tworzy `Assets/Skills/AxeThrow.asset` i `WarCry.asset` (4 tiery wg ARCHITECTURE §4) i dopisuje je do `pool` w `SkillSlots` na `Player.prefab`.
2. Uruchom (gdy żadna inna instancja Unity nie działa): menu `LD57 > Wire P1 Skills` albo `Unity.exe -batchmode -quit -projectPath . -executeMethod P1Wiring_Skills.Run -logFile p1skills.log`. Idempotentne.
3. W grze sprawdź: level up oferuje oba skille; topór leci, trafia wroga w obie strony i wraca; tier 3 rzuca dwa topory.
4. Krzyk bojowy odrzuca i ogłusza wrogów w promieniu; tier 3 zadaje 3 obrażenia.
5. Cooldowny w HUD: topór 3 s, krzyk 8 s. Bez EffectPrefab topór to Cube 0,3 m tworzony w kodzie.

### Wrogowie P1: pająk i strzelec

#### Co tworzy skrypt
`Assets/Editor/P1Wiring_Enemies.cs`, menu **LD57 > Wire P1 Enemies** (idempotentne, istniejących assetów nie nadpisuje):

- `Assets/Prefabs/Enemies/Spider.prefab`: wariant `_Enemy Base`, graf Behavior wyłączony, `SpiderController`,
  HP 4, XP 15, agent 6 m/s, windup 0,4 s, hitbox 8 obrażeń, placeholder: spłaszczona sfera (fiolet).
- `Assets/Prefabs/Enemies/Shooter.prefab`: wariant `_Enemy Base`, `ShooterController`, HP 4, XP 20, agent 2 m/s,
  `EnemyMeleeAttack.autoAttack` = false, placeholder: kapsuła (zielona). Pocisk 6 obrażeń, 10 m/s, 3 s życia.
- `Assets/Prefabs/Enemies/Projectile.prefab` (+ materiały `Spider/Shooter/Projectile.mat`).
- W `Michal.unity`: obiekt `Projectile Pool` z `ProjectilePool` (referencja do prefabu pocisku).
  Pocisk działa tylko gdy taki obiekt jest w scenie; inne sceny muszą go mieć też.

Jeśli zmieniasz wartość w prefabie po uruchomieniu, skrypt jej nie cofnie (pomija istniejące prefaby).
Żeby wygenerować od nowa: usuń prefab i uruchom ponownie.

#### Jak uruchomić
Zamknięty/otwarty edytor: menu **LD57 > Wire P1 Enemies**. Batch:
`Unity.exe -batchmode -quit -projectPath . -executeMethod P1WiringEnemies.Run -logFile p1enemies.log`

#### Dodanie do WaveZone.Entries
- **Pająk**: `EnemyPrefab` = Spider, `SpawnPoint` = pusty obiekt na ścianie/suficie komory, ok. 2–3 m nad podłogą
  (poza NavMeshem jest OK). Pająk po spawnie siedzi, a gdy gracz jest bliżej niż 12 m, zeskakuje po paraboli
  na NavMesh najbliżej gracza (maks. 6 m od punktu) i goni. W trakcie zeskoku jest nietykalny.
- **Strzelec**: `EnemyPrefab` = Shooter, `SpawnPoint` na NavMeshu w komorze (z widokiem na środek), najlepiej 8–10 m
  od miejsca, gdzie wejdzie gracz. Trzyma 8 m, ucieka przed 3 m, strzela co 2 s.

### Boss i feel P1

#### Co tworzy skrypt (`Assets/Editor/P1Wiring_Boss.cs`)
- `Assets/Prefabs/Enemies/Boss Spider Mother.prefab` (wariant `_Enemy Base`) plus materiał `.mat`: SpiderMotherBoss, HP 120, `isBoss`, XP 0, agent 4 m/s r=1,2, windup 1,0 s, hitbox 15 obrażeń, kula 3x1,5x3, DamageFlash. Behavior Graph Agent wyłączony. `minionPrefab` = Spider.prefab, a gdy go nie ma: Slime Enemy.prefab (po powstaniu pająka uruchom skrypt ponownie).
- `DamageFlash` w `_Enemy Base.prefab` (dziedziczą slime i pająk).
- `Player.prefab`: knockback wszystkich DamagingBlade = 0,5.
- Scena Michal: `Hit Feedback` (HitStop + HitFeedback z playerHealth), `CameraShake` na Main Camera, `Game UI/Screen Flash`.
Idempotentny. Wymaga wcześniej P0 (Game UI).

#### Uruchomienie
Menu **LD57 > Wire P1 Boss** (zamknięty batch: `Unity.exe -batchmode -quit -projectPath . -executeMethod P1WiringBoss.Run`). Potem zapisz projekt.

#### Boss na poziomie 8
1. Otwórz `Level 8.prefab`, ostatnia `WaveZone` (przed kratą).
2. Dodaj wpis w `entries`: `EnemyPrefab` = Boss Spider Mother, `Count` = 1, `SpawnPoint` = pusty obiekt na PODŁODZE na NavMeshu w centrum areny (boss sam wisi 6 m nad tym punktem, więc sufit musi być wyżej).
3. Boss liczy się do kraty jako zwykły wróg strefy; jego śmierć daje `OnRunWon` (GameManager). Przyzwane pająki NIE są w strefie, więc nie blokują kraty, ale dają XP.

## 8. P2: dźwięk, efekty, klimat (automat)

Wszystko wchodzi w `Wire ALL` (`bash Tools/unity-batch.sh wireall`): `P2Wiring_Ambience`, `P2WiringAudio`, `P2Wiring_Vfx`.
Dźwięki są syntezowane w kodzie (bez plików), efekty to systemy cząstek budowane kodem, klimat to tabela presetów
na poziom. Wszystko do podmiany na prawdziwe assety bez zmian w kodzie: klipy w `ProceduralSfxBank.overrides`,
prefaby w `SkillDefinition.Tiers[i].EffectPrefab`, wartości w `LevelAmbience.Presets`.

### Audio proceduralne P2

- Uruchom menu **LD57 > Wire P2 Audio**: dodaje obiekt `Sfx` do `Michal.unity` (idempotentnie) i AudioListener na Main Camera.
- `ProceduralSfxBank` generuje klipy w `Awake`; głośność główna: pole `masterVolume` (0,6).
- Prawdziwe klipy: w obiekcie `Sfx` rozwiń `Overrides` i wpisz `AudioClip` pod indeksem `SfxKind`
  (0 Dash, 1 Attack, 2 Hit, 3 PlayerHurt, 4 Windup, 5 Shot, 6 GateOpen, 7 Heart, 8 LevelUp, 9 CardPick,
  10 WaveCleared, 11 Death, 12 Win, 13 BossTelegraph, 14 EnemyDie). Ustawiony element zastępuje syntezę.
- Nowy efekt w kodzie: `Sfx.Play(SfxKind.X, pozycja)` (3D) lub `Sfx.Play(SfxKind.X)` (2D); bez banku w scenie nic się nie dzieje.
- Źródła mają `ignoreListenerPause`, więc grają przy `timeScale = 0`.

### VFX skilli i wrogów P2

Menu: `LD57 > Wire P2 VFX` (`Assets/Editor/P2Wiring_Vfx.cs`). Idempotentne, nadpisuje prefaby (można stroić i odpalać ponownie).

#### Co tworzy
- Folder `Assets/Prefabs/VFX/` z prefabami i materiałami (jeden na kolor, URP Particles/Unlit; topór URP/Lit).
- Prefaby: `Spin_T0..T3`, `Charge_T0..T3`, `GroundSlam_T0..T3`, `AxeThrow_T0..T3`, `WarCry_T0..T3`, `EnemyDeath`.
- Przypisuje je do `Tiers[i].EffectPrefab` w `Assets/Skills/{Spin,Charge,GroundSlam,AxeThrow,WarCry}.asset`.
- Ustawia `deathEffect` w `Assets/Prefabs/Enemies/_Enemy Base.prefab` (komponent `EnemyDeath`).
- Efekty (poza toporem) mają `EffectAutoDestroy` (pole `lifetime`, wyliczane z czasu cząstek, min. 3 s).
- Tier 3 (ewolucja): Spin = stożek „Tornado”, Charge = dodatkowy ogień, GroundSlam = linie pęknięć + iskry, AxeThrow = większy topór z ognistym śladem, WarCry = czerwony, promień 6.

#### Podmiana na prawdziwe efekty
Otwórz asset skilla (np. `Assets/Skills/Spin.asset`), w `Tiers[i]` przeciągnij własny prefab do pola `Effect Prefab`. Uwaga: ponowne uruchomienie `Wire P2 VFX` nadpisze to pole prefabami generowanymi, więc po podmianie nie uruchamiaj go ponownie (albo usuń odpowiedni fragment skryptu).
- AxeThrow: `EffectPrefab` to obiekt topora (skill sam go porusza i niszczy), bez autodestrukcji.
- Pozostałe: spawn przez `SkillHit.SpawnEffect`, który i tak niszczy obiekt po `Duration + 1`; `EffectAutoDestroy` jest dodatkową siatką bezpieczeństwa.
- Śmierć wroga: pole `Death Effect` na `EnemyDeath` w `_Enemy Base.prefab`.

### Klimat poziomów, krata, napis P2

- Uruchom `LD57 > Wire P2 Ambience` (idempotentne): dodaje `LevelAmbience` na rootach Level 1..8 (wartosci z `LevelAmbience.Presets`).
- `Exit Gate/Exit Light` (Point, pomaranczowy, wylaczony) przypisany do `ExitGate.exitLight`; krata zjezdza 4 m w 0,6 s.
- Level 1: `Controls Sign` (TextMeshPro + `WorldSign`) lezy na ziemi 2 m przed `Level Entry`.
- `Player.prefab`: dziecko `Lamp` (Point, range 12, intensity 0) z `PlayerLamp`; intensywnosc ustawia `LevelAmbience`.
- Strojenie: edytuj `Presets` w kodzie i uruchom menu ponownie, albo pola na prefabie poziomu.
- Poziomy 1-5 maja lampe 0 (wylaczona), 6-8 wlaczona.

## 9. Tura „dopracowanie” 1: bryły, animacja, telegrafy, AI (automat)

W `Wire ALL`: `P3WiringVisuals` → `P3WiringAnimation` → `P3WiringAI`. Kontrakty w `Docs/ARCHITECTURE.md` §8
(hierarchia `Visual/Body/Eyes/Legs/Leg_i`, `Telegraph.Circle/Cone/Line`, zdarzenia dla animacji). Weryfikacja:
`smokecombat` (17 kroków, w tym telegrafy i ustawka wrogów z bliska) oraz galeria zrzutów.

### Bryły wrogów, telegrafy, światło i post-processing

Uruchomienie: menu **LD57 > Wire P3 Visuals** (idempotentne; usuwa i buduje od nowa `Visual` wrogów).

- **Hierarchia `Visual`** (pod rootem wroga): `Body`, `Eyes/EyeL`, `Eyes/EyeR`; pająk i boss `Legs/Leg_0..Leg_7` (pivot przy ciele, dzieci `Upper` i `Lower`, co 45°); strzelec `Core` i `Barrel`; boss `Core`.
- **Podmiana na modele:** zachować nazwy `Visual`, `Body`, `Eyes/EyeL|EyeR`, `Legs/Leg_i` (i `Upper`/`Lower`), bo animacja szuka ich po nazwie. Collidery zostają na roocie wroga (nie na `Visual`). Skrypt wiringu zrobiony pod prymitywy nadpisze ręczną podmianę, więc po wstawieniu modeli go nie uruchamiać.
- **Materiały:** `Assets/Prefabs/Enemies/Materials/*.mat` (URP/Lit, oczy i rdzenie emisyjne przez `_EmissionColor`); serce `Pickups/Heart.mat`, pocisk `Enemies/Projectile.mat`.
- **Światła:** `Glow Light` (Heart, Projectile), `Core Light` (strzelec), `Eye Light` (oczy bossa); wszystkie z `LightFlicker` (±15 %) i `VolumetricAdditionalLight`.
- **Telegrafy:** obiekt `Telegraphs` (`TelegraphRenderer`) w scenie; AI woła `Telegraph.Circle/Cone/Line`. Kolory i szerokość linii w inspektorze.
- **Presety poziomów i post-processing:** `LevelAmbience.Presets` (winieta, bloom, ziarno, saturacja, temperatura, mgła wolumetryczna). `OnEnable` aktualizuje globalny Volume „Level Volume”. Prefaby bez zapisanych wartości biorą preset po numerze w nazwie obiektu („Level 3”).
- **Mgła wolumetryczna** działa dopiero po dodaniu `VolumetricFogRendererFeature` do `PC_Renderer` (ręcznie, nie edytowane tutaj); bez tego komponent jest nieaktywny, a zostaje mgła RenderSettings.
- Kamera: `renderPostProcessing = true` ustawia ten sam skrypt wiringu.

### Animacja proceduralna wrogów

- Menu `LD57 > Wire P3 Animation` dodaje (idempotentnie) `VisualRig` + animator + `HitReaction` do Slime Enemy, Spider, Shooter, Boss Spider Mother.
- `VisualRig` szuka `Visual`, `Visual/Body`, `Eyes`, `Legs/Leg_0..7`; animuje tylko je, nigdy roota. Brak nóg/oczu = komponent działa bez nich.
- Zdarzenia: `EnemyMeleeAttack.OnWindupStarted/OnAttackFinished` (slime, pająk), `ShooterController.OnWindupStarted/OnShot`, `SpiderController.Current` (odpytywane), `SpiderMotherBoss.OnStateChanged/OnTelegraph`, opcjonalnie `OnPhaseChanged(int)` (refleksja), `Health.OnHealthChanged/OnDied`.
- Strojenie: pola `[SerializeField]` na animatorze (amplituda i częstotliwość nóg, `perchedFold`, `dropFold`, `swayDegrees`, `hopHeight`, `coreScale`); trafienie w `HitReaction` (`hitSquash`, `punchAmount`).
- Znaki kątów nóg (`perchedFold`, `dropFold`, `ceilingFold`) zależą od orientacji pivotów `Leg_i` w modelu Grafiki; przy złym kierunku odwróć znak.
- Squash skaluje cały `Visual` wokół jego pivotu, więc pivot powinien leżeć przy ziemi.
- Przy śmierci `HitReaction` wyłącza animatory i zapada `Visual` w 0,4 s (EnemyDeath niszczy po 0,5 s).

### AI wrogów: stado, zasadzka, kiting, fazy bossa

Uruchomienie: menu **LD57 > Wire P3 AI** (idempotentne). Robi: `EnemyAlert` na `_Enemy Base` i `Slime Enemy`,
w `Slime Enemy` wyłącza `BehaviorGraphAgent`, dodaje `SlimeController`, `retreatAfterAttack` = 1,2;
w `Shooter` ustawia `burstCount` 1 / `burstInterval` 0,15. Reszta pól ma wartości domyślne ze skryptów.

#### Alarm (`EnemyAlert`)
`EnemyAlert.Raise(pozycja)` wołają kontrolery przy pierwszym wykryciu gracza. Każdy wróg w `alertRadius` (8 m)
dostaje `IsAlerted` + `OnAlerted` i przechodzi do pościgu/ataku nawet poza własnym zasięgiem wykrycia.

#### Stany i telegrafy
| Wróg | Stany | Telegraf |
| --- | --- | --- |
| Slime (`SlimeController`) | Idle (wędrówka 4 m co 2-4 s, v 2) > Chase (wykrycie 10 m lub alarm; sektory po 360/n wokół gracza, r 1,8 m, przeliczane co 0,5 s) > Attack (autoAttack) > Retreat (2,5 m od gracza, 1,2 s) > Chase; gracz dalej niż 14 m > Idle | koło przed ciosem (`EnemyMeleeAttack`, r = 0,6 zasięgu) |
| Pająk (`SpiderController`) | Perched > (gracz w 6 m / obrażenia / alarm) Dropping > Chasing > Leap (gracz w 2,5-3,5 m) > cios > Retreat (4 m, 1 s) > Circle (r 3 m, krok 60° co 0,5 s, 1,5 s) > Chasing | stożek 40° przed zeskokiem (0,4 s), stożek 30° przed skokiem (0,4 s) |
| Strzelec (`ShooterController`) | strzał tylko z linią wzroku; zasłonięty: szuka punktu z widokiem w 6 m (8 kierunków); w cooldownie strafe 7-9 m, zmiana strony co 1,5 s | linia do gracza przez windup |
| Boss (`SpiderMotherBoss`) | OnCeiling > Drop > Charge > Summon > ReturnToCeiling; `Phase` 1/2/3 (HP < 60 % / < 30 %), `OnPhaseChanged(int)`, `OnStateChanged("PhaseN")` + 1 s pauzy ryku | koło przy zeskoku, stożek 25° / 8 m przed każdą szarżą (0,5 s) |

Fazy bossa: 2 = `ceilingTime` x0,6, Drop dwa razy z rzędu (szybki powrót 0,4 s między), szarża x1,3.
3 = `ceilingTime` x0,4, 5 stworów, po pierwszym Charge+Summon jeszcze `phase3ExtraRounds` (2) par Charge+Summon bez
wracania na sufit.

#### Pola do strojenia (Inspector)
- `SlimeController`: `detectRange`, `loseRange`, `surroundRadius`, `groupRadius`, `lungeDistance`, `retreatDistance`, `idleSpeed`.
- `SpiderController`: `dropTriggerRange`, `retreatDistance/Time`, `circleTime/Radius/Step`, `leapMin/MaxDistance`, `leapDuration`.
- `ShooterController`: `keepDistance` + `keepBand`, `strafeSwitchTime`, `coverSearchRadius`, `burstCount` / `burstInterval`, `detectRange`.
- `SpiderMotherBoss`: progi i mnożniki faz, `chargeTelegraph`, `phase3MinionCount`, `phase3ExtraRounds`, `roarPause`.
- `EnemyMeleeAttack.retreatAfterAttack` (0 = brak; slime ma 1,2).

## 10. Tura „dopracowanie” 2: feel gracza, UI, reżyser fal (automat)

W `Wire ALL`: `P4WiringWaves` → `P4Wiring_Player` → `P4Wiring_UI` (po P3). Weryfikacja: `smokecombat` i `smokefull`
(drugi puls fali przychodzi po 0,8 s, testy liczą wrogów po 1,2 s). `smokecombat` ma krok „UI tury 2” (pasek HP
wroga, „!”, HUD, powidoki, panel bossa ukryty, zrzut `09-ui-world-close`); pełny test dokańcza animację wyboru karty
(`cardTweens[0].Complete(true)`), bo `Choose` zamyka ekran dopiero po 0,15 s. Kamera: `PunchZoom(0.85, 0.4)` przy trafieniu
bossa jest podpięty w `HitFeedback`; `SetZoom(0.9)` na walkę z bossem jeszcze nie.

### Feel gracza i kamera P4
Uruchom: menu `LD57 > Wire P4 Player` (idempotentne; wymaga wcześniej Wire P0).

- `Player.prefab`: `SwordTrail` (TrailRenderer powstaje na `SWORD`, iskry + mały wstrząs przy `DamagingBlade.OnHit`), `DashAfterimage` (powidoki z `SkinnedMeshRenderer`), `PlayerSkillAnimator` (animuje tylko `Body`).
- Materiały tworzone w `Assets/Materials/Player/` (SwordTrail.mat, DashAfterimage.mat; URP Particles/Unlit, Transparent).
- `Michal.unity`: obiekt `Low Health Overlay` (Image + `LowHealthOverlay`) jako pierwsze dziecko `Game UI`; `CameraController` dostaje `lookAhead = 1.5` i `ZoomMultiplier = 1`.
- Kamera: `SetZoom(0.9f)` na czas walki z bossem, `PunchZoom(0.85f, 0.4f)` przy trafieniu bossa (wywołania do dodania po stronie bossa/reżysera).
- Nowe zdarzenia: `DamagingBlade.OnHit(Vector3)`, `SkillUser.OnSkillExecuted(SkillKind, SkillTier)`; `CameraShake.ShakeScaled(int dmg)`.
- `LowHealthOverlay` sam znajduje `Health` gracza (FindFirstObjectByType) w `Start`.

### UI i świat P4
1. Menu **LD57 > Wire P4 UI** (otwiera `Michal.unity`, zapisuje scenę; idempotentne).
2. W `Game UI`: `Health Bar` (lewy górny róg, 320x22, `HealthBarView`) i `Boss Health Bar` (`BossHealthBar`, panel ukryty do pojawienia się bossa).
3. W scenie: `Enemy Health Bars` (paski nad wrogami po trafieniu, bez bossa) i `Alert Markers` („!” po `EnemyAlert.Raise`).
4. Stary `Status Bar` (`UI Canvas`) jest wyłączany; `UIManager` zostaje, ale bez widocznego paska.
5. Do `LevelUpScreen` wpisywany jest `header` (`Root/Header`) dla pulsu nagłówka.
6. Animacje kart/HUD/kraty/serca działają bez wiringu (czas rzeczywisty, DOTween `SetUpdate(true)`).
7. Wysokość pasków i „!” nad wrogiem: pola `height` na menedżerach (domyślnie 2 i 2,3 m).

### Reżyser fal P4
1. Uruchom `LD57 > Wire P4 Waves` (lub dodaj metodę do `Tools/unity-batch.sh`): ustawia `secondPulseDelay` 0,8 i `alertOnSpawn` na strefach Level 1..8 oraz dodaje `Wave Director` do Michal.unity.
2. `WaveZone` spawnuje w dwóch pulsach (połowa wpisu w górę, reszta po 0,8 s); drugi puls zmieści się w oknie 1,2 s testów dymnych.
3. Widoczny punkt spawnu jest zamieniany na niewidoczny punkt NavMesha w promieniu 8 m (nie dla pająka i bossa); maska raycasta bez warstwy 6.
4. `SpawnEntrance` dodaje się sam do każdego wroga (0,45 s: wyłonienie spod ziemi, pył, plask `EnemyDie` 0,4); agent stoi, `EnemyMeleeAttack` wyłączony.
5. Dźwięk plasku jest zastępczy (`SfxKind.EnemyDie`); docelowo osobny `SfxKind` i materiał pyłu z projektu.
6. `WaveDirector` (opcjonalny): slow-mo 0,6 przez 0,3 s po `OnWaveCleared`, wyłączalne polem `slowMoOnWaveCleared`.

## 11. Runda 3: menu, dekoracje, audio otoczenia, build (automat)

W `Wire ALL`: `P5Wiring_Menu` → `P5WiringAmbience` → `P5Wiring_Decor` (po P4). Build Windows: `bash Tools/unity-batch.sh build`
(`P5Build`, `Builds/Win64/LD57.exe`). Menu startowe jest pomijane w batchu (`Application.isBatchMode`) i po restarcie
(`StartMenu.MarkRestart()`); test z menu edytora wymaga `StartMenu.SkipMenu = true`.

### Menu startowe i pauza P5
1. `LD57 > Wire P5 Menu` (idempotentne): tworzy `Start Menu` i `Pause Menu` w `Game UI` tuż przed `Screen Flash`, ustawia `Game Manager.autoStart = false`.
2. Bez `Start Menu` w scenie i z `autoStart = false` gra stoi w pauzie: albo wpiąć menu, albo zaznaczyć `autoStart`.
3. Menu jest pomijane przy `Application.isBatchMode`, `StartMenu.SkipMenu = true` oraz po `RestartRun()` (menu tylko przy pierwszym uruchomieniu).
4. Tytuł i tagline: `Assets/Scripts/Game/GameTitle.cs` (jedno miejsce). Menu ukrywa `Skill HUD` i `Health Bar` do startu.
5. Esc: pauza w trakcie runu (nie na ekranie kart, nie po końcu runu); wznowienie przywraca poprzedni `timeScale`. Na czas menu/pauzy `PlayerController` jest wyłączany (czyta input przy timeScale 0).
6. Kompilacja: `bash Tools/compile-check.sh`; edytor: Play w `Michal.unity` pokazuje menu, Enter/Space/klik startuje run.

### Dekoracje poziomów P5
1. Menu **LD57 > Wire P5 Decor** dodaje `CaveDecor` (z `levelNumber` N) do `Level 1..8.prefab`; idempotentne, nie rusza innych dzieci.
2. W runtime `CaveDecor.Start` generuje obiekty pod dzieckiem `Decor` (seed = numer poziomu); poziom 1 bez dekoracji; bez colliderow, `EnemyHealth` i tagu `Enemy`.
3. Punkty losowane raycastami po `MeshCollider`-ach poziomu z punktow NavMesha wokol `LevelEntry`, `WaveZone` i `ExitGate`; brak trafienia = element pominiety.
4. Podmiana placeholderow na modele: pola `webPrefab`, `cocoonPrefab`, `nestPrefab`, `spiderPropPrefab`, `skeletonPrefab` na `CaveDecor` (puste = prymitywy z `DecorPrimitives`).
5. Wymagania prefabow: web = quad/model o boku 1 m; kokon: os dluga Y, ok. 1 m; gniazdo: pivot na gorze; pajak: nogi w plaszczyznie XZ; szkielet: pivot na podlodze, lezy wzdluz Z. Collidery z prefabow sa usuwane.
6. Podglad w edytorze: Play, albo menu kontekstowe komponentu **Regenerate** (w trybie Play). Ilosci i rozmieszczenie per poziom: `switch` w `CaveDecor.Generate`.

### Audio otoczenia P5
1. Menu `LD57 > Wire P5 Ambience` dodaje do `Michal.unity` obiekt `Ambience` (`ProceduralAmbienceBank` + `AmbienceController`); idempotentne.
2. `AmbienceController` słucha `GameEvents.OnLevelStarted`: poziomy 1-2 wiatr + kapanie, 3-4 + dudnienie i szelest, 5-6 mocniejsze dudnienie, 7 + bicie serca, 8 cisza (rzadkie ciche kapanie).
3. Po pojawieniu się `SpiderMotherBoss` (polling co 0,5 s) wchodzą `BossBreath` 0,3 i `Rumble` 0,2; `OnRunWon`/`OnPlayerDied` wyciszają wszystko w 1 s.
4. Przejścia między poziomami to crossfade pętli 1,5 s; jednorazowe dźwięki (3D, 6-12 m od gracza) są pomijane przy `timeScale == 0`.
5. Podmiana na nagrania: wpisz `AudioClip` w polach `AmbienceController` (`dripClips`, `rustleClips`, `rumbleClip`, `windClip`, `heartbeatClip`, `bossBreathClip`); puste pole = klip z banku proceduralnego.
6. Głośność ogólna: `masterVolume` (0,6) na `AmbienceController`; poziomy per warstwa są w `MixFor` (kod).

## 12. Runda 4: hordy i feel trafienia (automat)

`P6Wiring_Balance` (ostatni w `Wire ALL`): ustawia `Count` we wpisach istniejących stref według tabeli `Composition`
(slime, pająk, strzelec per poziom; rozdział po strefach jak w `P1Wiring_Levels`, reszta do ostatnich stref), HP i XP
wrogów (slime 3/4, pająk 4/9, strzelec 4/12), komponent `Knockback` na prefabach wrogów (`resistance`: boss 0,15,
pająk 0,8), `knockback` 0,9 na mieczu gracza. Nie rusza pozycji stref ani punktów spawnu. Po ręcznym rozstawieniu
poziomów można go uruchamiać dalej (tylko liczebności). Kod: `DamageFlash` (podmiana materiału na biały Unlit),
`Combat/Knockback`, `VFX/HitSplash`, promień rozsunięcia w `WaveZone` rośnie z pierwiastkiem liczebności.

## 13. Runda 5: strumień spawnu, skok slime'a, nowa mapa (automat)

- `WaveZone`: wysyp startowy do `maxAlive` z punktów strefy, potem strumień (`spawnInterval`, `perTick`) z okolicy
  gracza (`streamRadiusMin/Max`, punkt niewidoczny z gracza, jeśli się da). `P4WiringWaves` ustawia `spawnInterval`,
  `P6Wiring_Balance` ustawia `maxAlive` per poziom (7 + N) i liczebności. Testy: `WaveZone.InstantSpawnForTests = true`
  w `BeginDriving`, więc liczą i wybijają całą strefę naraz.
- Slime: `EnemyMeleeAttack.leap` (ustawia `P6Wiring_Balance.WireSlimeLeap`): windup = `rig.Crouch(0.4)` w `SlimeAnimator`,
  `Telegraph.Circle` w miejscu lądowania (cel = gracz + prędkość × 0,25 s, maks. zasięg + 1 m), lot przez `agent.Move`,
  łuk rysuje animator z `LeapProgress`, lądowanie = `OverlapSphere` na gracza + splat. Pająk/strzelec/boss bez zmian.
- Nowa mapa: `bash Tools/unity-batch.sh newcave` po podmianie `Assets/Models/Cave_new.fbx` (nazwy `Level_N`), potem
  `wireall` (P6 nakłada liczebności na świeżo rozstawione strefy).

## 14. Runda 9: feel, animacje, cząsteczki (automat)

W `Wire ALL` (po P7): `P8Wiring_PlayerFeel` → `P8Wiring_UiFeel`. Cząsteczki świata (śmierć wrogów per typ, serce,
pocisk, krata, puls telegrafu) są w kodzie bez wiringu (`VFX/DeathEffects`, `VFX/WorldVfx`). `ScreenFlash.Flash(Color)`
dla złotego błysku level upu. Test walki robi zrzuty `11-impact-close` (łuk wiru + fala) i `12-deaths-close`.

### Feel gracza P8
1. Uruchom `LD57 > Wire P8 Player Feel` (dodaje `PlayerFeel` do `Assets/Prefabs/Player.prefab`, idempotentnie).
2. Referencje: `PlayerController`, `Rigidbody`, `Health`, dziecko `Body`, `PlayerSkillAnimator` (wszystko z roota).
3. Cząsteczki (pył, smugi, krople) i materiały powstają w kodzie pod obiektem `Player Feel FX` (World space), bez assetów.
4. `SwordTrail` sam tworzy drugi TrailRenderer `Trail Core` pod mieczem (po ponownym wiringu P4 nic nie trzeba zmieniać).
5. `PlayerSkillAnimator` woła `PlayerFeel.Streaks` przy Charge i pomija squash lądowania PlayerFeel przy GroundSlam (`SlamPending`).
6. Progi do strojenia w inspektorze: `runSpeedThreshold` 2 m/s, `stepInterval` 0,22 s, `lowHealthFraction` 0,3, `lowHealthInterval` 0,8 s.

### Cząsteczki świata P8
1. Bez wiringu: `DeathEffects.Play` (slime, pająk, strzelec, boss) wołane z `EnemyDeath`; typ po komponencie, kolor po `HitSplash.ColorOf`.
2. Bez wiringu: serce (iskrzenie w locie, wybuch przy zebraniu), pocisk (smuga tworzona w `Awake`, iskry i pierścień przy trafieniu).
3. Bez wiringu: krata (pył i błysk `exitLight` x3 przy starcie, obłoczek na końcu zjazdu); telegrafy (pulsowanie w ostatnich 0,15 s, błysk 0,08 s).
4. Zapadnięcie bryły robi `HitReaction` (`VisualRig.Collapse`); `DeathEffects` go nie dubluje. `destroyDelay` bez zmian (0,5 s).
5. Wszystko w kodzie (materiały, tekstury, ParticleSystemy tworzone leniwie); `deathEffect` w prefabach zostaje i działa równolegle.
6. Wymaga wiringu: nic (brak `Assets/Editor/P8Wiring_WorldVfx.cs`). Obejrzeć w Play: kałuża po slime'ie, odłamki pająka, kratę po wyczyszczeniu poziomu.

### UI feel P8
1. `LD57 > Wire P8 UI Feel` (idempotentne, zapisuje `Michal.unity`): w `Game UI` powstają `Xp Bar` (480x10, `XpBarView`) i `Hit Direction` (`HitDirectionIndicator`, pełny ekran).
2. W scenie: `Level Up Burst` i `Wave Cleared Toast` (font z TMP Settings, kamera `Camera.main`).
3. Pasek XP: wybrano nowy `Xp Bar`; stary `XP Bar` z `SkillHud` jest wyłączony, `SkillHud.xpFill` wyzerowany (napis `Lv N` zostaje). Ponowne `Wire P0` może go włączyć, wtedy uruchom P8 jeszcze raz.
4. `ScreenFlash` ma tylko czerwony błysk bez wariantu z kolorem, więc level up go nie używa.
5. `WaveClearedToast`: tylko w świecie (napis „Czysto” + 8 iskier), bez HUD, zgodnie z designem.
6. Liczby obrażeń: >= 5 pomarańczowe x1,4 ze wstrząsem; zabójstwo czerwone z „!” (próg: pole `heavyHitThreshold`).
7. Wszystko na czasie rzeczywistym (ekran kart ma `timeScale = 0`); nie uruchamiano Unity, tylko `compile-check`.

## 15. Runda 10: klasa Mag, jakość poziomów, materiały terenu i post-processing (automat)

W `Wire ALL` (po P8): `P9Wiring_Materials` → `P9Wiring_PostFx` → `P9Wiring_Mage`. Rozstawienie poziomów z oceną sal,
pochodniami, grzybami i propami wymaga `bash Tools/unity-batch.sh levelsforce` (NADPISZ wszystkie 8) przed `wireall`.
Test walki ma krok „mag” (Apply("Mage") → 3 pociski w bossa → Apply("Warrior"), zrzut `13-mage-close`).

### Klasa Mag P9
1. Uruchom `LD57 > Wire P9 Mage` (Unity zamknięte: `-executeMethod P9Wiring_Mage.Run`); tworzy `Assets/Classes/{Warrior,Mage}.asset` i `Assets/Skills/Mage/*.asset` (istniejących nie nadpisuje).
2. `Player.prefab` dostaje `PlayerClassApplier` (warrior/mage/defaultClass) oraz wyłączony `RangedAttack`; domyślna klasa = Wojownik, więc testy dymne bez zmian.
3. `Michal.unity`: w `Start Menu` przyciski „Wojownik”/„Mag” nad „Graj”, opis klasy pod nimi; Graj/Sterowanie/Wyjdź przesunięte w dół. Wybór trafia do `ClassSelection.ChosenName` (static, przeżywa restart sceny).
4. HP klasy i prędkość ustawiane refleksją (`Health.maxHealth`, `PlayerController.moveSpeed`), bo nie mają setterów; model Maga (`FemaleCharacterPolyart`) podmieniany pod `Body`, stary tylko wyłączany.
5. Po podmianie modelu applier przepina refleksją `PlayerAnimations.animator` i `DashAfterimage.skinnedRenderer`; `SwordTrail` jest wyłączany u Maga.
6. Blink zajmuje spację (`UsesSpaceSlot`), reszta skilli maga Q/E/R; liczby w assetach `Assets/Skills/Mage`, balans do sprawdzenia w grze.
7. Brak ikon skilli (Icon null), brak dźwięków specyficznych dla maga (użyte istniejące SfxKind).
8. Test ręczny: Play, Mag, Graj; LPM strzela, awans daje karty z puli maga; restart (śmierć) zachowuje klasę.

### Poziomy P9
1. Kolejność: `bash Tools/unity-batch.sh levelsforce` (`P1Wiring_Levels.RunAll`, NADPISUJE rozstawienie 1–8) → `wireall` (`P2Wiring_Ambience`: pochodnie/grzyby, światło kraty). Potem `smokefull`.
2. Strefy: pozycja życzeniowa przesuwana po ścieżce o ±12 m do sali (12 punktów NavMesha na okręgu 6 m, krawędź ≥ 3 m); odstępy: ≥ 20 m między strefami, ≥ 14 m od wejścia, ≥ 8 m od kraty. Log: `[P1Levels] Level N strefa Z: ...`.
3. Spawny ≥ 1,5 m od krawędzi NavMesha i ≥ 6 m od wejścia; wyjątek: `Spawn B` pająka stoi 0,5 m od najbliższej ściany (8 kierunków, ≤ 5 m), bez ściany zostaje bez zmian.
4. Box triggera strefy: szerokość NavMesha w osi side (6–14 m) × 4 × głębokość w osi dir (6–10 m); nazwy `Level Entry`, `Wave Zone N`, `Spawn X`, `Exit Gate` bez zmian.
5. Krata: dzieci `Gate Pillar L/R` (cylindry 0,15 × 2,5, bez colliderów, materiał `Prefabs/Levels/Generated/Gate Pillar.mat`); `Exit Light` zasięg 6, intensywność 1,5.
6. Pochodnie `Torch N` (komponenty `Light` + `LightFlicker` + `Torch`, dziecko `Holder`): poziomy 1–2 co 10 m, 3–4 co 14 m, przy najbliższej ścianie (≤ 4 m), 1,8 m nad podłogą; płomień buduje `Torch.cs` w runtime.
7. Poziomy 5–6: `Mushrooms N` co 12 m (3 emisyjne kule + Point Light 0,5/1/0,8, zasięg 4); poziomy 7–8 bez źródeł światła przy ścianach; stare `Torch *`/`Mushrooms *` są kasowane przed dodaniem (idempotentnie).
8. Propy `CaveDecor` (runtime, seed = numer poziomu, bez colliderów): stalagmity 10 (poziomy 1–4) / 6 (5–8), 6 kupek rumowiska, poziom 8: 8 kłów gniazda wokół areny; poziom 1 ma teraz `CaveDecor` z samymi propami ogólnymi.

### Materiały terenu i post-processing P9
1. Kolejność: menu `LD57 > Wire P9 Materials`, potem `LD57 > Wire P9 PostFx` (oba idempotentne, bezpieczne do ponownego uruchomienia).
2. Materials generuje `Assets/Graphics/Generated/rock_albedo|rock_normal|rock_mask.png` (tylko gdy brak plików; skasuj PNG, żeby wygenerować od nowa) i 4 materiały `Assets/Materials/Cave/Cave_Warm|Grey|Cold|Violet.mat` (poziomy 1–2, 3–4, 5–6, 7–8).
3. Materiały trafiają do `Model` (MeshRenderer) w `Level 1..8.prefab`; wszystkie sloty materiałów dostają ten sam materiał.
4. Powrót do materiału level artysty: w każdym `Level N.prefab` przypisz do `Model` materiał `Assets/Materials/Ground.mat` (albo `git checkout -- "Assets/Prefabs/Levels/Level N.prefab"`, jeśli reszta prefabu nie była zmieniana). `Ground.mat` i `Ground.shadergraph` nie są ruszane.
5. PostFx dodaje `VolumetricFogRendererFeature` do `Assets/Settings/PC_Renderer.asset` (jeśli go brakuje; obecnie jest) oraz obiekt `Post Fx Pulse` w `Michal.unity`; kamera ma mieć włączone Post Processing.
6. Wartości post-processingu per poziom: tabela `LevelAmbience.Presets` (bloom, próg, kontrast, winieta, ziarno, chromatic, cienie/środek/światła); mgła wolumetryczna od poziomu 4 (`fogVolumetric`), jej Volume ustawia `LevelAmbience.Apply()`.
7. `PostFxPulse` (czas rzeczywisty): obrażenia = chromatic 0,35 i winieta +0,2 przez 0,25 s; level up = ekspozycja +0,4 przez 0,4 s; śmierć = saturacja do −80 w 0,6 s. Wyłączenie: usuń obiekt `Post Fx Pulse`.
8. Prefaby poziomów sprzed rozszerzenia presetu biorą nowe pola post-processingu z `Presets` po numerze w nazwie; `ApplyPresetValues` (P2 Ambience) przy nowym komponencie kopiuje wszystko.
