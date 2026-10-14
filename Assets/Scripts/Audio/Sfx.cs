using UnityEngine;

public enum SfxKind
{
    Dash, Attack, Hit, PlayerHurt, Windup, Shot, GateOpen, Heart, LevelUp, CardPick,
    WaveCleared, Death, Win, BossTelegraph, EnemyDie
}

/// <summary>Statyczny punkt wejścia dla efektów dźwiękowych. Bez ProceduralSfxBank w scenie nic nie robi.</summary>
public static class Sfx
{
    public static void Play(SfxKind kind, Vector3 position, float volume = 1f)
    {
        if (ProceduralSfxBank.Instance != null)
            ProceduralSfxBank.Instance.Play(kind, position, volume, true);
    }

    public static void Play(SfxKind kind)
    {
        if (ProceduralSfxBank.Instance != null)
            ProceduralSfxBank.Instance.Play(kind, Vector3.zero, 1f, false);
    }
}
