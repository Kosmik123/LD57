using System.Collections;
using UnityEngine;

namespace Enemies.Attacking
{
    public class AnimationAttackBehavior : AttackBehavior
    {
        [SerializeField]
        private Animator _animator;
        [SerializeField]
        private string animationTrigger;
        
        [SerializeField]
        private AttackBehavior attackBehavior;
        [SerializeField]
        private float delay;

        private bool isAttacking;

        public override void Attack()
        {
            if (isAttacking)
                return;

            isAttacking = true;

            _animator.SetTrigger(animationTrigger);
            StartCoroutine(AttackCo());
        }

        private IEnumerator AttackCo()
        {
            yield return new WaitForSeconds(delay); 
            attackBehavior.Attack();
            isAttacking = false;
        }

        private void OnDisable()
        {
            isAttacking = false;
        }
    }
}
