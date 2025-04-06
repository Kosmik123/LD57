using Bipolar;
using NaughtyAttributes;
using UnityEngine;

namespace Enemies.Movement
{
    public class OnTargetReachedRetargettingStrategy : EnemyRetargettingStrategy
    {
        [SerializeField, Required]
        private EnemyMovementBehavior enemyMovement;

        [SerializeField]
        private RandomFloat delay;
        private Timer timer;

        private void OnEnable()
        {
            timer = new Timer(this);
            enemyMovement.OnTargetReached += EnemyMovement_OnTargetReached;
        }

        private void EnemyMovement_OnTargetReached()
        {
            timer.Duration = delay;
            timer.Restart();
            timer.OnElapsed += Timer_OnElapsed;
        }

        private void Timer_OnElapsed()
        {
            timer.OnElapsed -= Timer_OnElapsed;
            RequestRetargetting();
        }

        private void OnDisable()
        {
            enemyMovement.OnTargetReached -= EnemyMovement_OnTargetReached;
        }
    }
}
