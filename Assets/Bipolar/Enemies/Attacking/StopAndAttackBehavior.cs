using Enemies.Movement;
using System.Collections;
using UnityEngine;

namespace Enemies.Attacking
{
    [AddComponentMenu(Paths.Attacking + "Stop and Attack Behavior")]
    public class StopAndAttackBehavior : AttackBehavior
    {
        [SerializeField]
        private Animator _animator;
        [SerializeField]
        private string attackTrigger;
        [SerializeField]
        private EnemyMovement movementToStop;

        public override void Attack()
        {
            _animator.SetTrigger(attackTrigger);
            movementToStop.enabled = false;
            StartCoroutine(ReenableMovementCo());
        }

        private IEnumerator ReenableMovementCo()
        {
            yield return new WaitForSeconds(1);
            movementToStop.enabled = true;
        }
    }
}
