using UnityEngine;
using UnityEngine.AI;

namespace Enemies.Movement
{
    public class NavMeshEnemyMovementBehavior : EnemyMovementBehavior
    {
        [SerializeField]
        protected NavMeshAgent agent;
        [SerializeField]
        protected bool disableAgentOnDisable;

        private bool targetReached;

        private void OnEnable()
        {
            agent.enabled = true;
        }

        private void Update()
        {
            if (IsOnTarget(transform.position))
            {
                if (targetReached == false)
                {
                    targetReached = true;
                    TargetReached();
                }
            }
            else
            {
                targetReached = false;
                if (agent.destination != Target)
                    agent.SetDestination(Target);
            }
        }

        private void OnDisable()
        {
            if (disableAgentOnDisable)
                agent.enabled = false;
        }
    }
}
