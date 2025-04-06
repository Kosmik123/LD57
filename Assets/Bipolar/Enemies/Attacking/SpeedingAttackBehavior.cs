using Bipolar;
using Enemies.Movement;
using UnityEngine;

namespace Enemies.Attacking
{
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

        private Timer timer;

        private bool isRunning;
        private Vector3 direction;

        public override void Attack()
        {
            movementToStop.enabled = false;
            _rigidbody.isKinematic = false;
            direction = (player.position - _rigidbody.position).normalized;
            direction.y = 0;
            isRunning = true;
            timer = new Timer(this, duration: runningDuration, onElapsed: StopRunning);
        }

        private void Update()
        {
            if (isRunning)
            {
                float y = _rigidbody.linearVelocity.y;
                Vector3 velocity = direction * speed;
                velocity.y = y;
                _rigidbody.linearVelocity = velocity;
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
