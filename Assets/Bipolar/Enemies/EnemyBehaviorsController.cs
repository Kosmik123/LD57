using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    public sealed class EnemyBehaviorsController : MonoBehaviour
    {
        public event Action<bool> OnActiveChanged;

        [Header("Activation")]
        [SerializeField]
        private Behaviour inactiveBehavior;
        [SerializeField]
        private Behaviour activeBehavior;

        [SerializeField]
        private EnemyActivationCondition[] activationConditions;
        [SerializeField]
        private float deactivationDelay = 4;
        [SerializeField, ReadOnly]
        private float deactivationTimer;

        [SerializeField, ReadOnly]
        private bool isActive;
        public bool IsActive => isActive;

        [Header("Attacking")]
        [SerializeField]
        private EnemyActivationCondition[] attackConditions;
        
        [SerializeField]
        private float attackDelay;
        [SerializeField, ReadOnly]
        private float attackTimer;
        [SerializeField]
        private AttackBehavior attackBehavior;  

        public bool CanAttack => attackTimer <= 0;

        private void Awake()
        {
            Deactivate();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool shouldBeActive = AreAllConditionsFulfilled(activationConditions);
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
                bool shouldAttack = AreAllConditionsFulfilled(attackConditions);
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

        public bool AreAllConditionsFulfilled(IReadOnlyList<EnemyActivationCondition> conditions)
        {
            foreach (var condition in conditions)
                if (condition.CheckCondition() == false)
                    return false;

            return conditions.Count > 0;
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
    }

    public abstract class EnemyActivationCondition : MonoBehaviour
    {
        public abstract bool CheckCondition();
    }

    public abstract class AttackBehavior : MonoBehaviour
    {
        public abstract void Attack();  
    }

    public abstract class EnemyBehavior : MonoBehaviour
    { }
}
