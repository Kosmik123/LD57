using NaughtyAttributes;
using UnityEngine;
using UnityEngine.AI;

namespace Enemies.Targetting
{
    [AddComponentMenu(Paths.Targetting + "NavMesh Enemy Target Validator")]
    public class NavMeshEnemyTargetValidator : EnemyTargetProvider
    {
        private const int maxTriesCount = 10;

        [SerializeField, Required]
        protected EnemyTargetProvider validatedTargetProvider;

        [Space]
        [SerializeField, NavMeshAgentType]
        private int agentType;
        [SerializeField]
        private float detectionRadius = 10;

        public override Vector3 GetNextTarget()
        {
            var navMeshFilter = new NavMeshQueryFilter()
            {
                agentTypeID = agentType,
                areaMask = NavMesh.AllAreas
            };

            int triesCount = 0;
            validatedTargetProvider.DetermineNextTarget();
            Vector3 target = validatedTargetProvider.Target;
            while (triesCount < maxTriesCount)
            {
                triesCount++;
                if (NavMesh.SamplePosition(target, out var navMeshHit, detectionRadius, navMeshFilter))
                    return navMeshHit.position;

                validatedTargetProvider.DetermineNextTarget();
                target = validatedTargetProvider.Target;
            }
            return target;
        }
    }
}
