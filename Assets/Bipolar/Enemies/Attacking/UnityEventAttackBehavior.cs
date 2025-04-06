using UnityEngine;
using UnityEngine.Events;

namespace Enemies.Attacking
{
    public class UnityEventAttackBehavior : AttackBehavior
    {
        [SerializeField]
        private UnityEvent onAttack;

        public override void Attack()
        {
            onAttack.Invoke();
        }
    }
}
