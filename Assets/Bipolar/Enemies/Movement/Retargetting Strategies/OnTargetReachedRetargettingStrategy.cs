using Bipolar;
using NaughtyAttributes;
using System;
using System.Collections;
using UnityEngine;

namespace Enemies.Movement
{
    [System.Serializable]
    public class OnTargetReachedRetargettingStrategy : EnemyRetargettingStrategy
    {
        [SerializeField, Required]
        private EnemyMovementBehavior enemyMovement;

        [SerializeField]
        private RandomFloat delayAfterReachingTarget;

        protected override void OnInit()
        {
            enemyMovement.OnTargetReached -= EnemyMovement_OnTargetReached;
            enemyMovement.OnTargetReached += EnemyMovement_OnTargetReached;
        }

        private void EnemyMovement_OnTargetReached()
        {
            enemyMovement.StartCoroutine(WaitForRetargetting());
        }

        private IEnumerator WaitForRetargetting()
        {
            yield return new WaitForSeconds(delayAfterReachingTarget);
            RequestRetargetting();
        }
    }
}
