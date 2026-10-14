using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD skilli (3 sloty + spacja) i pasek XP.</summary>
public class SkillHud : MonoBehaviour
{
    [System.Serializable]
    public class SlotView
    {
        public GameObject root;
        public Image icon;
        [Tooltip("Radialny Image (Filled) pokazujący pozostały cooldown.")]
        public Image cooldownFill;
        public TMP_Text key;
        public TMP_Text tier;
    }

    [SerializeField, Tooltip("4 elementy: Q, E, R, Space.")]
    private SlotView[] views = new SlotView[4];
    [SerializeField] private SkillSlots slots;
    [SerializeField] private Experience experience;
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text heroLevel;

    private readonly bool[] wasOnCooldown = new bool[4];

    private static readonly string[] Roman = { "", "I", "II", "III" };

    private void Update()
    {
        for (int i = 0; i < views.Length; i++)
        {
            SkillInstance skill = null;
            KeyCode key;
            if (i < SkillSlots.MaxSlots)
            {
                key = SkillSlots.SlotKeys[i];
                if (i < slots.Slots.Count) skill = slots.Slots[i];
            }
            else
            {
                key = SkillSlots.SpaceSlotKey;
                skill = slots.SpaceSkill;
            }
            Refresh(views[i], skill, key);
            TrackReady(i, skill);
        }

        if (experience != null)
        {
            if (xpFill != null) xpFill.fillAmount = experience.Progress01;
            if (heroLevel != null) heroLevel.text = "Lv " + experience.HeroLevel;
        }
    }

    private void TrackReady(int i, SkillInstance skill)
    {
        var v = views[i];
        if (v == null || i >= wasOnCooldown.Length) return;
        bool onCooldown = skill != null && skill.CooldownRemaining > 0f;
        if (wasOnCooldown[i] && !onCooldown && v.icon != null)
        {
            var t = v.icon.transform;
            t.DOKill(true);
            t.localScale = Vector3.one;
            DOTween.Sequence().SetTarget(t).SetUpdate(true)
                .Append(t.DOScale(1.25f, 0.125f).SetEase(Ease.OutQuad))
                .Append(t.DOScale(1f, 0.125f).SetEase(Ease.InQuad));
        }
        wasOnCooldown[i] = onCooldown;
    }

    private static void Refresh(SlotView v, SkillInstance skill, KeyCode key)
    {
        if (v == null) return;
        if (v.key != null) v.key.text = key == KeyCode.Space ? "SPACE" : key.ToString();
        bool has = skill != null;
        if (v.icon != null)
        {
            v.icon.enabled = has && skill.Definition.Icon != null;
            if (has) v.icon.sprite = skill.Definition.Icon;
        }
        if (v.tier != null) v.tier.text = has ? Roman[Mathf.Clamp(skill.Tier, 0, 3)] : "";
        if (v.cooldownFill != null)
        {
            float cd = has ? skill.Current.Cooldown : 0f;
            v.cooldownFill.fillAmount = has && cd > 0f ? Mathf.Clamp01(skill.CooldownRemaining / cd) : 0f;
        }
    }
}
