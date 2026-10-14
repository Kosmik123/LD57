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
            Refresh(views[i], skill, key, skill != null && skill == slots.WheelSkill);
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

    private static readonly Color NormalBg = new Color(0.1f, 0.1f, 0.12f, 0.8f);
    private static readonly Color WheelBg = new Color(0.2f, 0.16f, 0.07f, 0.92f);

    /// <summary>Slot z aktywnym skillem koła (PPM) dostaje złote tło i dopisek „PPM” przy klawiszu.</summary>
    private static void Refresh(SlotView v, SkillInstance skill, KeyCode key, bool onWheel)
    {
        if (v == null) return;
        bool has = skill != null;
        if (v.key != null)
        {
            string label = key == KeyCode.Space ? "SPACE" : key.ToString();
            if (onWheel) label += " <size=65%><color=#FFD966>PPM</color></size>";
            if (v.key.text != label) v.key.text = label;
        }
        if (v.root != null && v.root.TryGetComponent<Image>(out var bg))
            bg.color = onWheel ? WheelBg : NormalBg;
        if (v.icon != null)
        {
            var sprite = has ? SkillIcons.Get(skill.Definition) : null;
            v.icon.preserveAspect = true;
            v.icon.enabled = sprite != null;
            if (sprite != null) v.icon.sprite = sprite;
        }
        if (v.tier != null) v.tier.text = has ? Roman[Mathf.Clamp(skill.Tier, 0, 3)] : "";
        if (v.cooldownFill != null)
        {
            float cd = has ? skill.Current.Cooldown : 0f;
            v.cooldownFill.fillAmount = has && cd > 0f ? Mathf.Clamp01(skill.CooldownRemaining / cd) : 0f;
        }
    }
}
