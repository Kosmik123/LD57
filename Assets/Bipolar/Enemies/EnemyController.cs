using Enemies.Movement;
using NaughtyAttributes;
using UnityEngine;

namespace Enemies
{
    public sealed class EnemyController : MonoBehaviour
    {
        public event System.Action<bool> OnActiveChanged;

        [Header("Activation")]
        [SerializeField]
        private GameObject inactiveBehavior;
        [SerializeField]
        private GameObject activeBehavior;

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
            activationCondition?.Init(gameObject);
            attackCondition?.Init(gameObject);

            Deactivate();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool shouldBeActive = activationCondition.IsFulfilled();
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
                bool shouldAttack = attackCondition.IsFulfilled();
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
                behaviorToDisable.SetActive(false);
            if (behaviorToEnable)
                behaviorToEnable.SetActive(true);

            if (oldActive != isActive)
                OnActiveChanged?.Invoke(isActive);
        }

        private void OnValidate()
        {
            activationCondition?.Init(gameObject);
            attackCondition?.Init(gameObject);
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
        public GameObject Enemy { get; private set; }

        internal void Init(GameObject enemy)
        {
            Enemy = enemy;
            OnInit();
        }

        public abstract bool Check();
        protected virtual void OnInit() { }
        internal protected virtual void DrawGizmos() { }
    }

    public abstract class AttackBehavior : MonoBehaviour
    {
        public abstract void Attack();  
    }

    public abstract class EnemyBehavior : MonoBehaviour
    { }
}
