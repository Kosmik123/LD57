using UnityEngine;
using UnityEngine.Events;

namespace Enemies.Attacking
{
    [AddComponentMenu(Paths.Attacking + "Unity Event Attack Behavior")]
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
