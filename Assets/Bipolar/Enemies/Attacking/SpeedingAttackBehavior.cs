using Bipolar;
using Enemies.Movement;
using UnityEngine;

namespace Enemies.Attacking
{
    [AddComponentMenu(Paths.Attacking + "Speeding Attack Behavior")]
    public class SpeedingAttackBehavior : AttackBehavior
    {
        [SerializeField]
        private EnemyMovement movementToStop;
        [SerializeField]
        private Rigidbody _rigidbody;
        [SerializeField]
        private Transform player;
        [SerializeField]
        private float speed;
        [SerializeField]
        private float runningDuration;

        private float runningStopTime;

        private bool isRunning;
        private Vector3 direction;

        public override void Attack()
        {
            movementToStop.enabled = false;
            _rigidbody.isKinematic = false;
            direction = (player.position - _rigidbody.position).normalized;
            direction.y = 0;
            isRunning = true;
        }

        private void Update()
        {
            if (isRunning)
            {
                float y = _rigidbody.linearVelocity.y;
                Vector3 velocity = direction * speed;
                velocity.y = y;
                _rigidbody.linearVelocity = velocity;
            
                if(Time.time > runningStopTime)
                {
                    StopRunning();
                }
            }
        }

        private void StopRunning()
        {
            _rigidbody.isKinematic = true;
            isRunning = false;
            movementToStop.enabled = true;
        }
    }

}
