#!/usr/bin/env bash
# Uruchamia Unity w trybie batch dla tego projektu.
#   bash Tools/unity-batch.sh wire    -> P0Wiring.Run (podpięcie prefabów, sceny, UI, assetów), potem -quit
#   bash Tools/unity-batch.sh smoke   -> P0SmokeTest.Run (test dymny w Play Mode, sam kończy edytor)
#   bash Tools/unity-batch.sh compile -> tylko import i kompilacja, -quit
#   bash Tools/unity-batch.sh wireall -> WireAll.Run (P0 + wszystkie P1), -quit
#   bash Tools/unity-batch.sh levels  -> P1Wiring_Levels.Run (tylko nierozstawione), levelsforce -> RunForce (NADPISZ wszystkie), -quit
#   bash Tools/unity-batch.sh smokefull -> P0SmokeTest.RunFull (8 poziomów + boss)
#   bash Tools/unity-batch.sh newcave -> migracja poziomów na Cave_new.fbx: siatki, NavMesh, rozstawienie 1-8
#   bash Tools/unity-batch.sh smokecombat -> P0SmokeTest.RunCombat (skille, wrogowie, boss, zrzuty w Logs/shots)
#   bash Tools/unity-batch.sh build   -> P5Build.BuildWindows (Builds/Win64/LD57.exe)
# Logi w Logs/unity-<tryb>.log. Wymaga zamkniętego edytora Unity dla tego projektu.
set -u
cd "$(dirname "$0")/.."
PROJ="$(pwd)"
VERSION="$(grep m_EditorVersion: ProjectSettings/ProjectVersion.txt | awk '{print $2}')"
UNITY="/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
if [ ! -f "$UNITY" ]; then echo "Brak Unity $VERSION w Unity Hub"; exit 2; fi
MODE="${1:-compile}"
mkdir -p Logs
LOG="$PROJ/Logs/unity-$MODE.log"
PROJW="$(cygpath -w "$PROJ")"
LOGW="$(cygpath -w "$LOG")"
case "$MODE" in
  wire)    ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod P0Wiring.Run -logFile "$LOGW") ;;
  smoke)   ARGS=(-batchmode -projectPath "$PROJW" -executeMethod P0SmokeTest.Run -logFile "$LOGW") ;;
  smokefull) ARGS=(-batchmode -projectPath "$PROJW" -executeMethod P0SmokeTest.RunFull -logFile "$LOGW") ;;
  smokecombat) ARGS=(-batchmode -projectPath "$PROJW" -executeMethod P0SmokeTest.RunCombat -logFile "$LOGW") ;;
  wireall) ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod WireAll.Run -logFile "$LOGW") ;;
  newcave) ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod P3Wiring_NewCave.RunFullMigration -logFile "$LOGW") ;;
  navmesh) ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod P1Wiring_NavMesh.Run -logFile "$LOGW") ;;
  levelsforce) ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod P1Wiring_Levels.RunAll -logFile "$LOGW") ;;
  levels)  ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -executeMethod P1Wiring_Levels.Run -logFile "$LOGW") ;;
  build)   ARGS=(-batchmode -nographics -projectPath "$PROJW" -executeMethod P5Build.BuildWindows -logFile "$LOGW") ;;
  exec)    ARGS=(-batchmode -nographics -projectPath "$PROJW" -executeMethod "${2:?metoda}" -logFile "$LOGW") ;;
  compile) ARGS=(-batchmode -nographics -quit -projectPath "$PROJW" -logFile "$LOGW") ;;
  *) echo "nieznany tryb: $MODE"; exit 2 ;;
esac
echo "Unity $VERSION, tryb $MODE, log: $LOG"
"$UNITY" "${ARGS[@]}"
CODE=$?
echo "exit=$CODE"
grep -E "error CS|\[P9Diag\]|Exception|\[P0Wiring\]|\[P1|\[WireAll\]|\[P0Smoke\] (OK|FAIL|WYNIK)|\[P5Build\]|Scripts have compiler errors|Aborting batchmode" "$LOG" | head -80
exit $CODE
