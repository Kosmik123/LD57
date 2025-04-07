using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

namespace Enemies.Conditions
{
    [System.Serializable]
    public class DistanceActivationCondition : Condition
    {
        [SerializeField, Tooltip("Transform which will revive enemy when nearby")]
        private Transform activator;
        [SerializeField]
        private float distance = 1;
        [SerializeField, Min(0.0001f)]
        private float deadzone = 0.1f;

        [SerializeField, ReadOnly]
        private bool isNearby;

        public override bool Check()
        {
            float squareDistance = (Enemy.transform.position - activator.position).sqrMagnitude;
            if (isNearby == false && squareDistance < distance * distance)
            {
                isNearby = true;
            }
            else if (isNearby && squareDistance > (distance + deadzone) * (distance + deadzone))
            {
                isNearby = false;
            }

            return isNearby;
        }

#if UNITY_EDITOR
        override protected internal void DrawGizmos()
        {
            if (Application.isPlaying) 
            {
                GizmosDrawDistanceSphere(isNearby);
            }
            else
            {
                GizmosDrawDistanceSphere(true);
                GizmosDrawDistanceSphere(false);
            }
        }

        private void GizmosDrawDistanceSphere(bool nearby)
        {
            Handles.color = nearby ? Color.red : Color.cyan;
            float radius = nearby ? distance + deadzone : distance;
            Handles.DrawWireArc(Enemy.transform.position, Vector3.up, Vector3.forward, 360, radius);
        }
#endif
    }
}
