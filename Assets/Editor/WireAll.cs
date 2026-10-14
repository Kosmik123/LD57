using UnityEditor;
using UnityEngine;

/// <summary>
/// Jedno wejście dla całego podpięcia: P0 (gracz, wróg bazowy, serce, scena, UI, poziom 1),
/// skille P1, wrogowie P1, boss, poziomy 2–8. Kolejność ma znaczenie (boss potrzebuje prefabu pająka,
/// poziomy potrzebują wszystkich prefabów wrogów).
///   bash Tools/unity-batch.sh wireall
/// </summary>
public static class WireAll
{
    [MenuItem("LD57/Wire ALL (P0 + P1)")]
    public static void Run()
    {
        Debug.Log("[WireAll] start");
        P0Wiring.Run();
        P1Wiring_Skills.Run();
        P1WiringEnemies.Run();
        P1WiringBoss.Run();
        P1Wiring_Levels.Run(false);
        P2Wiring_Ambience.Run();
        P2WiringAudio.Run();
        P2Wiring_Vfx.Run();
        P3WiringVisuals.Run();
        P3WiringAnimation.Run();
        P3WiringAI.Run();
        P4WiringWaves.Run();
        P4Wiring_Player.Run();
        P4Wiring_UI.Run();
        P5Wiring_Menu.Run();
        P5WiringAmbience.Run();
        P5Wiring_Decor.Run();
        P6Wiring_Balance.Run();
        P6Wiring_SwordAudio.Run();
        P7Wiring_FallGuard.Run();
        P8Wiring_PlayerFeel.Run();
        P8Wiring_UiFeel.Run();
        P9Wiring_Materials.Run();
        P9Wiring_PostFx.Run();
        P9Wiring_Mage.Run();
        P10Wiring_Population.Run();
        P12Wiring_SkillUi.Run();
        P12Wiring_Mechanics.Run();
        P12WiringBoss.Run(); // ostatni: P3/P1 przebudowują nogi bossa
        AssetDatabase.SaveAssets();
        Debug.Log("[WireAll] done");
    }
}
