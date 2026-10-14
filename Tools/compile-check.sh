#!/usr/bin/env bash
# Kompiluje skrypty gry (Assembly-CSharp) Roslynem z instalacji Unity, bez otwierania edytora.
# Użycie: bash Tools/compile-check.sh        (z dowolnego katalogu)
# Wynik: lista błędów CS albo "OK". Ostrzeżenia są wyciszone.
# Wymaga: Library/ScriptAssemblies (projekt otwarty choć raz w Unity) i Unity Hub z dowolnym 6000.x.
set -u
cd "$(dirname "$0")/.."
PROJ="$(pwd)"
UNITY_DIR="$(ls -d "/c/Program Files/Unity/Hub/Editor"/6000.* 2>/dev/null | head -1)"
if [ -z "$UNITY_DIR" ]; then echo "Brak instalacji Unity 6000.x w Unity Hub"; exit 2; fi
CSC="$UNITY_DIR/Editor/Data/DotNetSdkRoslyn/csc.dll"
MANAGED="$UNITY_DIR/Editor/Data/Managed/UnityEngine"
NETSTD="$UNITY_DIR/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
w() { cygpath -w "$1"; }   # ścieżki Windows dla dotnet

OUT="$(mktemp -d)"
RSP="$OUT/args.rsp"
{
  echo "-target:library"
  echo "-nologo"
  echo "-nowarn:CS0109,CS0114,CS0168,CS0219,CS0414,CS0649,CS1998,CS8632"
  echo "-langversion:9.0"
  echo "-define:UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_LEGACY_INPUT_MANAGER"
  echo "-out:\"$(w "$OUT/Assembly-CSharp.dll")\""
  echo "-r:\"$(w "$NETSTD")\""
  # shimy netfx (mscorlib itd.) dla DLL-ek zbudowanych pod .NET Framework, np. DOTween
  for d in "$UNITY_DIR"/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/*.dll; do echo "-r:\"$(w "$d")\""; done
  for d in "$MANAGED"/*.dll; do echo "-r:\"$(w "$d")\""; done
  for d in "$PROJ"/Library/ScriptAssemblies/*.dll; do
    case "$(basename "$d")" in
      Assembly-CSharp*.dll|*Editor*.dll|*editor*.dll|*CodeGen*.dll|*.Test.dll) ;;
      *) echo "-r:\"$(w "$d")\"" ;;
    esac
  done
  find "$PROJ/Assets/Plugins" -name "*.dll" 2>/dev/null | grep -vi editor | while read -r d; do echo "-r:\"$(w "$d")\""; done
  # Źródła Assembly-CSharp: wszystko w Assets poza folderami z własnym asmdef lub Editor
  find "$PROJ/Assets" -name "*.cs" \
    -not -path "*/Editor/*" \
    -not -path "*/NaughtyAttributes/*" \
    -not -path "*/SerializeReferenceExtensions/*" \
    -not -path "*/Bipolar/Interface Serialization/*" \
    -not -path "*/Plugins/*" \
    -not -path "*/TextMesh Pro/*" \
    -not -path "*/RPG Tiny Hero Duo/*" \
    -not -path "*/Volumetric/*" \
    | while read -r f; do echo "\"$(w "$f")\""; done
} > "$RSP"

LOG="$OUT/csc.log"
dotnet "$(w "$CSC")" "@$(w "$RSP")" > "$LOG" 2>&1
STATUS=$?
grep -E "error CS" "$LOG" | sort -u > "$OUT/errors.txt"
if [ "$STATUS" -ne 0 ] || [ -s "$OUT/errors.txt" ]; then
  echo "BŁĘDY KOMPILACJI ($(wc -l < "$OUT/errors.txt")):"
  if [ -s "$OUT/errors.txt" ]; then cat "$OUT/errors.txt"; else tail -20 "$LOG"; fi
  rm -rf "$OUT"
  exit 1
fi
echo "OK: Assembly-CSharp kompiluje się bez błędów ($(grep -c '\.cs"$' "$RSP") plików)"

# Etap 2: Assembly-CSharp-Editor (Assets/Editor i foldery Editor poza pakietami z asmdef)
EDITOR_SRC="$(find "$PROJ/Assets" -path "*/Editor/*" -name "*.cs"     -not -path "*/NaughtyAttributes/*" -not -path "*/SerializeReferenceExtensions/*"     -not -path "*/Bipolar/Interface Serialization/*" -not -path "*/Plugins/*"     -not -path "*/TextMesh Pro/*" -not -path "*/RPG Tiny Hero Duo/*" -not -path "*/Volumetric/*")"
if [ -n "$EDITOR_SRC" ]; then
  RSP2="$OUT/args-editor.rsp"
  {
    grep -vE '^-out:|\.cs"$' "$RSP"
    echo "-out:\"$(w "$OUT/Assembly-CSharp-Editor.dll")\""
    echo "-r:\"$(w "$OUT/Assembly-CSharp.dll")\""
    for d in "$UNITY_DIR"/Editor/Data/Managed/UnityEditor*.dll "$UNITY_DIR"/Editor/Data/Managed/UnityEngine/UnityEditor*.dll; do [ -f "$d" ] && echo "-r:\"$(w "$d")\""; done
    for d in "$PROJ"/Library/ScriptAssemblies/*Editor*.dll "$PROJ"/Library/ScriptAssemblies/*editor*.dll; do [ -f "$d" ] && echo "-r:\"$(w "$d")\""; done
    echo "$EDITOR_SRC" | while read -r f; do echo "\"$(w "$f")\""; done
  } > "$RSP2"
  LOG2="$OUT/csc-editor.log"
  dotnet "$(w "$CSC")" "@$(w "$RSP2")" > "$LOG2" 2>&1
  STATUS2=$?
  grep -E "error CS" "$LOG2" | sort -u > "$OUT/errors2.txt"
  if [ "$STATUS2" -ne 0 ] || [ -s "$OUT/errors2.txt" ]; then
    echo "BŁĘDY KOMPILACJI EDITOR ($(wc -l < "$OUT/errors2.txt")):"
    if [ -s "$OUT/errors2.txt" ]; then cat "$OUT/errors2.txt"; else tail -20 "$LOG2"; fi
    rm -rf "$OUT"
    exit 1
  fi
  echo "OK: Assembly-CSharp-Editor kompiluje się bez błędów ($(echo "$EDITOR_SRC" | grep -c .) plików)"
fi
rm -rf "$OUT"
exit 0
