using System.Reflection;
using UnityEngine;

/// <summary>
/// Stosuje klasę postaci na graczu: tryb ataku, pula skilli, HP, prędkość, model i tint. Komponent na Player.prefab.
/// Awake (przed resztą) stosuje klasę z ClassSelection; menu startowe woła Apply() ponownie po zmianie wyboru
/// (zmiana jest odwracalna: stary model jest tylko wyłączany).
/// Pola prywatne Health.maxHealth i PlayerController.moveSpeed ustawiane refleksją (bez edycji tych plików).
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerClassApplier : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private PlayerClass warrior;
    [SerializeField] private PlayerClass mage;
    [SerializeField] private PlayerClass defaultClass;

    public PlayerClass Current { get; private set; }

    private Transform body;
    private Transform originalModel;
    private GameObject spawnedModel;
    private PlayerClass spawnedFor;
    private float baseMoveSpeed = 1f;
    private int baseMaxHealth = -1;
    private bool swordTrailWasEnabled;
    private bool captured;
    private bool awakeDone;

    private void Awake()
    {
        Apply(Resolve(ClassSelection.ChosenName));
        awakeDone = true;
    }

    public PlayerClass Resolve(string className)
    {
        if (!string.IsNullOrEmpty(className))
        {
            if (warrior != null && (warrior.DisplayName == className || warrior.name == className)) return warrior;
            if (mage != null && (mage.DisplayName == className || mage.name == className)) return mage;
        }
        return defaultClass != null ? defaultClass : warrior;
    }

    /// <summary>Stosuje klasę po nazwie (jak ClassSelection.ChosenName).</summary>
    public void Apply(string className) => Apply(Resolve(className));

    public void Apply(PlayerClass cls)
    {
        if (cls == null) return;
        Capture();
        Current = cls;

        var controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.MeleeHitboxes = !cls.Ranged;
            controller.JumpEnabled = false;
            SetField(controller, "moveSpeed", baseMoveSpeed * cls.MoveSpeedMultiplier);
        }

        var ranged = GetComponent<RangedAttack>();
        if (ranged != null)
        {
            ranged.Configure(cls.AttackDamage, cls.AttackCooldown);
            ranged.enabled = cls.Ranged;
        }

        var slots = GetComponent<SkillSlots>();
        if (slots != null && cls.SkillPool != null && cls.SkillPool.Length > 0)
            slots.SetPool(cls.SkillPool);

        ApplyHealth(cls);
        ApplyModel(cls);

        var trail = GetComponent<SwordTrail>();
        if (trail != null) trail.enabled = !cls.Ranged && swordTrailWasEnabled;
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        body = transform.Find("Body");
        var controller = GetComponent<PlayerController>();
        var f = typeof(PlayerController).GetField("moveSpeed", Priv);
        if (controller != null && f != null) baseMoveSpeed = (float)f.GetValue(controller);
        var health = GetComponent<Health>();
        baseMaxHealth = health != null ? health.MaxHealth : -1;
        var trail = GetComponent<SwordTrail>();
        swordTrailWasEnabled = trail != null && trail.enabled;
        if (body != null)
        {
            var animator = body.GetComponentInChildren<Animator>(true);
            originalModel = animator != null ? ChildOfBody(animator.transform) : null;
        }
    }

    private Transform ChildOfBody(Transform t)
    {
        while (t != null && t.parent != body) t = t.parent;
        return t;
    }

    private void ApplyHealth(PlayerClass cls)
    {
        var health = GetComponent<Health>();
        if (health == null) return;
        int max = cls.MaxHealth > 0 ? cls.MaxHealth : baseMaxHealth;
        if (max <= 0 || max == health.MaxHealth) return;
        if (!SetField(health, "maxHealth", max)) return;
        SetField(health, "currentHealth", max);
        // Po Start() Health ma już HP; odśwież pasek (zmiana 0 nie jest obrażeniem).
        if (awakeDone) health.ChangeHealth(0);
    }

    private void ApplyModel(PlayerClass cls)
    {
        if (body == null) return;

        GameObject target = originalModel != null ? originalModel.gameObject : null;
        if (cls.ModelPrefab != null)
        {
            if (spawnedModel == null || spawnedFor != cls)
            {
                if (spawnedModel != null) Destroy(spawnedModel);
                spawnedModel = Instantiate(cls.ModelPrefab, body);
                spawnedModel.name = cls.ModelPrefab.name;
                spawnedFor = cls;
                if (originalModel != null)
                {
                    spawnedModel.transform.localPosition = originalModel.localPosition;
                    spawnedModel.transform.localRotation = originalModel.localRotation;
                    spawnedModel.transform.localScale = originalModel.localScale;
                }
                var oldAnim = originalModel != null ? originalModel.GetComponentInChildren<Animator>(true) : null;
                var newAnim = spawnedModel.GetComponentInChildren<Animator>(true);
                if (oldAnim != null && newAnim != null)
                {
                    newAnim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
                    newAnim.applyRootMotion = false;
                }
            }
            target = spawnedModel;
        }
        else if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
            spawnedFor = null;
        }

        if (target == null) return;
        if (originalModel != null) originalModel.gameObject.SetActive(target == originalModel.gameObject);
        if (spawnedModel != null) spawnedModel.SetActive(target == spawnedModel);

        // Miecz: tylko w modelu docelowym.
        foreach (var t in target.GetComponentsInChildren<Transform>(true))
            if (t.name == "SWORD") t.gameObject.SetActive(!cls.HideSword);

        ApplyTint(target, cls.Tint);

        // Komponenty, które w Awake zapamiętały stary model.
        var newAnimator = target.GetComponentInChildren<Animator>(true);
        var pa = GetComponent<PlayerAnimations>();
        if (pa != null && newAnimator != null) SetField(pa, "animator", newAnimator);
        var after = GetComponent<DashAfterimage>();
        var smr = target.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (after != null && smr != null) SetField(after, "skinnedRenderer", smr);
    }

    private static void ApplyTint(GameObject model, Color tint)
    {
        bool neutral = tint.r > 0.99f && tint.g > 0.99f && tint.b > 0.99f;
        var block = new MaterialPropertyBlock();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (neutral) { r.SetPropertyBlock(null); continue; }
            block.Clear();
            block.SetColor(BaseColorId, tint);
            block.SetColor(ColorId, tint);
            r.SetPropertyBlock(block);
        }
    }

    private static bool SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, Priv);
        if (f == null) { Debug.LogWarning($"[PlayerClassApplier] brak pola {target.GetType().Name}.{name}"); return false; }
        f.SetValue(target, value);
        return true;
    }
}
