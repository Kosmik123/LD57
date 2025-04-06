using UnityEngine;

namespace Enemies
{
    public abstract class EnemyTargetProvider : MonoBehaviour
    {
        public Vector3 Target { get; private set; }

        public void DetermineNextTarget()
        {
            Target = GetNextTarget();
        }

        public abstract Vector3 GetNextTarget();
    }
}
