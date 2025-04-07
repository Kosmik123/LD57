using NaughtyAttributes;
using UnityEngine;

namespace Enemies.Movement
{
    [AddComponentMenu(Paths.Root + "Enemy Movement")]
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeReference, SubclassSelector]
        private Condition retargettingCondition;
        [SerializeField, Required]
        private EnemyTargetProvider targetProvider;
        [SerializeField, Required]
        private EnemyMovementBehavior movement;

        private void Awake()
        {
            retargettingCondition?.Init(gameObject);
            if (!enabled)
                OnDisable();
        }

        private void OnEnable()
        {
            targetProvider.enabled = true;
            movement.enabled = true;

            RetargetEnemy();
        }

        protected virtual void Update()
        {
            if (retargettingCondition.IsFulfilled())
            {
                RetargetEnemy();
            }
        }

        private void RetargetEnemy()
        {
            targetProvider.DetermineNextTarget();
            var target = targetProvider.Target;
            movement.Target = target;
        }

        private void OnDisable()
        {
            targetProvider.enabled = false;
            movement.enabled = false;
        }
    }

    public abstract class EnemyMovementBehavior : MonoBehaviour
    {
        public event System.Action OnTargetReached;
        public Vector3 Target { get; set; }

        [SerializeField]
        protected bool flatTargetting = true;

        public bool IsOnTarget(Vector3 position)
        {
            if (IsBig(Target.x - position.x))
                return false;

            if (IsBig(Target.z - position.z))
                return false;

            return flatTargetting || !IsBig(Target.y - position.y);
        }

        private static bool IsBig(float number)
        {
            return Mathf.Abs(number) > 0.05f;
        }

        protected void TargetReached()
        {
            OnTargetReached?.Invoke();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && enabled)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, Target);
                Gizmos.DrawSphere(Target, 0.2f);
            }
        }
    }

    public abstract class EnemyRetargettingStrategy : Condition
    {
        protected bool shouldRetarget;

        public sealed override bool Check()
        {
            if (shouldRetarget == false)
                return false;
            shouldRetarget = false;
            return true;
        }

        protected void RequestRetargetting()
        {
            shouldRetarget = true;
        }
    }

    public static class ConditionExtensions
    {
        public static bool IsFulfilled(this Condition condition)
        {
            if (condition == null)
                return false;

            return condition.Check();
        }
    }
}
