using Bipolar;
using UnityEditor;
using UnityEngine;

namespace Enemies.Targetting
{
    [AddComponentMenu(Paths.Targetting + "Random Enemy Target Provider")]
    public class RandomPointTargetProvider : EnemyTargetProvider
    {
        [Header("Settings")]
        [SerializeField]
        private RandomFloat randomTargetRadius = 1;

        public override Vector3 GetNextTarget()
        {
            var target = Random.onUnitSphere;
            target.y = 0;
            target.Normalize();
            target *= randomTargetRadius;
            target += transform.position;
            return target;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (enabled)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(transform.position, Vector3.up, randomTargetRadius.min);
                Handles.DrawWireDisc(transform.position, Vector3.up, randomTargetRadius.max);
            }
        }
#endif
    }
}
