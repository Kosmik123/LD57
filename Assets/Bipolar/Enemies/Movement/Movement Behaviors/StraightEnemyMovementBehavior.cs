using UnityEngine;

namespace Enemies.Movement
{
    public class StraightEnemyMovementBehavior : EnemyMovementBehavior
    {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField, Min(0)]
        private float moveSpeed = 4;
        public float MoveSpeed => moveSpeed;

        [SerializeField]
        private float rotationSpeed = 120;
        
        [SerializeField]
        private bool stopMovementOnDisable;

        private bool targetReached;

        private void Reset()
        {
            _rigidbody = GetComponentInParent<Rigidbody>();
        }

        private void OnEnable()
        {
            _rigidbody.isKinematic = false;
        }

        private void Update()
        {
            if (IsOnTarget(_rigidbody.position))
            {
                if (targetReached == false)
                {
                    targetReached = true;
                    SetFlatVelocity(Vector3.zero);
                    TargetReached();
                }
            }
            else
            {
                UpdateMovement(Time.deltaTime);
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            targetReached = false;
            Vector3 direction = (Target - _rigidbody.position).normalized;
            SetFlatVelocity(direction * moveSpeed);
            Vector3 newForward = Vector3.RotateTowards(_rigidbody.transform.forward, direction, rotationSpeed * deltaTime * Mathf.Deg2Rad, 0);
            newForward.y = 0;
            _rigidbody.transform.forward = newForward;
        }

        private void SetFlatVelocity(Vector3 velocity)
        {
            velocity.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = velocity;
        }

        private void OnDisable()
        {
            if (stopMovementOnDisable)
                _rigidbody.isKinematic = true;
        }
    }
}
