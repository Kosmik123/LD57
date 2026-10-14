using System.Collections.Generic;
using UnityEngine;

/// <summary>Czyta Q/E/R/Space, odpala skille i liczy cooldowny. Komponent na graczu.</summary>
public class SkillUser : MonoBehaviour
{
    [SerializeField] private SkillSlots slots;
    [SerializeField] private PlayerController player;
    [SerializeField] private LayerMask enemyLayers = ~0;

    /// <summary>Skill został uruchomiony (po Execute).</summary>
    public event System.Action<SkillKind, SkillTier> OnSkillExecuted;

    private readonly Dictionary<SkillKind, ISkillBehaviour> behaviours = new Dictionary<SkillKind, ISkillBehaviour>();
    private GroundSlamSkill groundSlam;

    private void Awake()
    {
        groundSlam = new GroundSlamSkill();
        behaviours[SkillKind.Spin] = new SpinSkill();
        behaviours[SkillKind.Charge] = new ChargeSkill();
        behaviours[SkillKind.GroundSlam] = groundSlam;
        behaviours[SkillKind.AxeThrow] = new AxeThrowSkill();
        behaviours[SkillKind.WarCry] = new WarCrySkill();
        behaviours[SkillKind.Fireball] = new FireballSkill();
        behaviours[SkillKind.FrostNova] = new FrostNovaSkill();
        behaviours[SkillKind.ArcaneOrbs] = new ArcaneOrbsSkill();
        behaviours[SkillKind.Blink] = new BlinkSkill();
        behaviours[SkillKind.ChainLightning] = new ChainLightningSkill();
        if (player == null) player = GetComponent<PlayerController>();
        if (slots == null) slots = GetComponent<SkillSlots>();
    }

    private void OnEnable() => slots.OnSkillChanged += Slots_OnSkillChanged;

    private void OnDisable()
    {
        slots.OnSkillChanged -= Slots_OnSkillChanged;
        groundSlam.Unsubscribe();
    }

    private void Slots_OnSkillChanged(SkillInstance instance)
    {
        if (instance.Definition.Kind == SkillKind.GroundSlam)
            player.JumpEnabled = true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        for (int i = 0; i < slots.Slots.Count; i++)
            Process(slots.Slots[i], SkillSlots.SlotKeys[i]);
        if (slots.SpaceSkill != null)
            Process(slots.SpaceSkill, SkillSlots.SpaceSlotKey);

        // Koło szybkiego wyboru: PPM używa aktywnego skilla (ten sam timer co slot Q/E/R),
        // rolka i Tab przełączają. PPM czytany jako przycisk myszy 1 (oś Fire2 ma też Left Alt).
        if (Input.GetMouseButtonDown(1))
            TryExecute(slots.WheelSkill);
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f) slots.CycleWheel(-1);
        else if (scroll < 0f) slots.CycleWheel(1);
        if (Input.GetKeyDown(KeyCode.Tab)) slots.CycleWheel(1);
    }

    private void Process(SkillInstance skill, KeyCode key)
    {
        if (skill.CooldownRemaining > 0f)
            skill.CooldownRemaining = Mathf.Max(0f, skill.CooldownRemaining - Time.deltaTime);

        if (!Input.GetKeyDown(key)) return;
        TryExecute(skill);
    }

    private void TryExecute(SkillInstance skill)
    {
        if (skill == null || !skill.IsReady || player.IsDashing) return;
        if (!behaviours.TryGetValue(skill.Definition.Kind, out var behaviour)) return;

        var aim = player.transform.forward;
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;

        behaviour.Execute(new SkillContext
        {
            Player = player,
            Origin = player.transform,
            Tier = skill.Current,
            TierIndex = skill.Tier,
            EnemyLayers = enemyLayers,
            Runner = this,
            AimDirection = aim,
        });
        skill.CooldownRemaining = skill.Current.Cooldown;
        OnSkillExecuted?.Invoke(skill.Definition.Kind, skill.Current);
    }
}
