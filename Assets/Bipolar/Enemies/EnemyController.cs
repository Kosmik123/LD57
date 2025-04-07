using NaughtyAttributes;
using UnityEngine;

namespace Enemies
{
    public sealed class EnemyController : MonoBehaviour
    {
        public event System.Action<bool> OnActiveChanged;

        [Header("Activation")]
        [SerializeField]
        private Behaviour inactiveBehavior;
        [SerializeField]
        private Behaviour activeBehavior;

        [SerializeReference, SubclassSelector]
        private Condition activationCondition;
        [SerializeField]
        private float deactivationDelay = 4;
        [SerializeField, ReadOnly]
        private float deactivationTimer;

        [SerializeField, ReadOnly]
        private bool isActive;
        public bool IsActive => isActive;

        [Header("Attacking")]
        [SerializeReference, SubclassSelector]
        private Condition attackCondition;
        
        [SerializeField]
        private float attackDelay;
        [SerializeField, ReadOnly]
        private float attackTimer;
        [SerializeField]
        private AttackBehavior attackBehavior;  

        public bool CanAttack => attackTimer <= 0;

        private void Awake()
        {
            activationCondition?.Init(this);
            attackCondition?.Init(this);

            Deactivate();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool shouldBeActive = activationCondition?.Check() ?? false;
            if (shouldBeActive)
            {
                deactivationTimer = deactivationDelay;
                if (isActive == false)
                    Activate();
            }
            else
            {
                deactivationTimer -= dt;
                deactivationTimer = Mathf.Max(deactivationTimer, 0);
                if (isActive && deactivationTimer <= 0)
                    Deactivate();
            }

            attackTimer -= dt;
            attackTimer = Mathf.Max(attackTimer, 0);
            if (isActive && CanAttack)
            {
                bool shouldAttack = attackCondition?.Check() ?? false;
                if (shouldAttack)
                {
                    Attack();
                    attackTimer = attackDelay;
                }
            }
        }

        private void Attack()
        {
            if (attackBehavior)
                attackBehavior.Attack();
        }


        private void Deactivate() => Activate(false);

        private void Activate(bool active = true)
        {
            bool oldActive = isActive;
            isActive = active;

            var behaviorToEnable = isActive ? activeBehavior : inactiveBehavior;
            var behaviorToDisable = isActive ? inactiveBehavior : activeBehavior;

            if (behaviorToDisable)
                behaviorToDisable.enabled = false;
            if (behaviorToEnable)
                behaviorToEnable.enabled = true;

            if (oldActive != isActive)
                OnActiveChanged?.Invoke(isActive);
        }

        private void OnValidate()
        {
            activationCondition?.Init(this);
            attackCondition?.Init(this);
        }

        private void OnDrawGizmosSelected()
        {
            activationCondition?.DrawGizmos();
            attackCondition?.DrawGizmos();
        }
    }

    [System.Serializable]
    public abstract class Condition
    {
        public EnemyController Enemy { get; private set; }

        internal void Init(EnemyController enemy) => Enemy = enemy;

        public abstract bool Check();

        internal protected virtual void DrawGizmos() { }
    }

    public abstract class AttackBehavior : MonoBehaviour
    {
        public abstract void Attack();  
    }

    public abstract class EnemyBehavior : MonoBehaviour
    { }
}
