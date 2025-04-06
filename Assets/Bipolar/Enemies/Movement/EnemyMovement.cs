using NaughtyAttributes;
using UnityEngine;

namespace Enemies.Movement
{
    [AddComponentMenu(Paths.Root + "Enemy Movement")]
    public class EnemyMovement : MonoBehaviour
    {
        [SerializeField, Required]
        private EnemyRetargettingStrategy retargetingStrategy;
        [SerializeField, Required]
        private EnemyTargetProvider targetProvider;
        [SerializeField, Required]
        private EnemyMovementBehavior movement;

        private void Awake()
        {
            if (!enabled)
                OnDisable();
        }

        private void OnEnable()
        {
            retargetingStrategy.enabled = true;
            targetProvider.enabled = true;
            movement.enabled = true;

            retargetingStrategy.OnRetargettingRequested += RetargetEnemy;
            RetargetEnemy();
        }

        private void RetargetEnemy()
        {
            targetProvider.DetermineNextTarget();
            var target = targetProvider.Target;
            movement.Target = target;
        }

        private void OnDisable()
        {
            retargetingStrategy.OnRetargettingRequested -= RetargetEnemy;
            retargetingStrategy.enabled = false;
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

    public abstract class EnemyRetargettingStrategy : MonoBehaviour
    {
        public event System.Action OnRetargettingRequested;

        protected void RequestRetargetting()
        {
            OnRetargettingRequested?.Invoke();
        }
    }
}
