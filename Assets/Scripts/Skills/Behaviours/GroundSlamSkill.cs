using System.Collections.Generic;
using UnityEngine;

/// <summary>Uderzenie w ziemię: skok, a po OnLanded obrażenia w promieniu Range. Tier 3: ogłuszenie.</summary>
public class GroundSlamSkill : ISkillBehaviour
{
    private SkillContext pending;
    private bool awaitingLanding;
    private PlayerController subscribedTo;

    public void Execute(SkillContext ctx)
    {
        if (ctx.Player == null || !ctx.Player.IsGrounded) return;
        if (subscribedTo != ctx.Player)
        {
            Unsubscribe();
            subscribedTo = ctx.Player;
            subscribedTo.OnLanded += Player_OnLanded;
        }
        pending = ctx;
        awaitingLanding = true;
        ctx.Player.RequestJump();
    }

    /// <summary>Odpina zdarzenie (wołane przez SkillUser w OnDisable).</summary>
    public void Unsubscribe()
    {
        if (subscribedTo != null) subscribedTo.OnLanded -= Player_OnLanded;
        subscribedTo = null;
        awaitingLanding = false;
    }

    private void Player_OnLanded()
    {
        if (!awaitingLanding) return;
        awaitingLanding = false;
        var ctx = pending;
        var center = ctx.Origin.position;
        var already = new HashSet<Health>();
        var hits = Physics.OverlapSphere(center, ctx.Tier.Range, ctx.EnemyLayers);
        foreach (var col in hits)
            SkillHit.Apply(col, ctx, center, ctx.Tier.Damage, ctx.Tier.Knockback, ctx.Tier.Stun, already);
        bool evolved = ctx.TierIndex >= SkillDefinition.MaxTier;
        SkillImpactVfx.Shockwave(center, ctx.Tier.Range, evolved ? new Color(1f, 0.35f, 0.15f) : new Color(1f, 0.8f, 0.45f));
        VoxelFxRecipes.GroundSlam(center, ctx.Tier.Range, evolved);
    }
}
