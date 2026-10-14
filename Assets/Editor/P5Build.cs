using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build Windows x64 z batcha: bash Tools/unity-batch.sh build → Builds/Win64/LD57.exe.
/// Scena z Build Settings (Michal.unity). Wynik i błędy w Logs/unity-build.log ([P5Build] ...).
/// </summary>
public static class P5Build
{
    const string OutputDir = "Builds/Win64";

    [MenuItem("LD57/Build Windows x64")]
    public static void BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes;
        var paths = new System.Collections.Generic.List<string>();
        foreach (var s in scenes) if (s.enabled) paths.Add(s.path);
        if (paths.Count == 0) { Debug.LogError("[P5Build] brak scen w Build Settings"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }

        Directory.CreateDirectory(OutputDir);
        var options = new BuildPlayerOptions
        {
            scenes = paths.ToArray(),
            locationPathName = Path.Combine(OutputDir, "LD57.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[P5Build] wynik: {summary.result}, rozmiar {summary.totalSize / (1024 * 1024)} MB, czas {summary.totalTime.TotalSeconds:F0} s, błędów {summary.totalErrors}, ostrzeżeń {summary.totalWarnings}, wyjście {summary.outputPath}");
        foreach (var step in report.steps)
            foreach (var msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    Debug.Log($"[P5Build] BŁĄD w {step.name}: {msg.content}");
        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
