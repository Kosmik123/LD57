using UnityEditor;
using UnityEngine;

namespace Enemies.Conditions
{
    [System.Serializable]
    public class VisibleInConeActivationCondition : Condition
    {
        [SerializeField]
        private Transform target;

        [SerializeField, Min(0)]
        private float viewDistance = 10;
        [SerializeField, Range(0, 90)]
        private float viewAngle = 30;

        private int lastCheckFrame = -1;
        private bool isInView;

        public override bool Check()
        {
            if (lastCheckFrame != Time.frameCount)
                isInView = IsInView();

            return isInView;
        }

        public bool IsInView()
        {
            var target = this.target;
            if (target == null)
                target = Player.Instance.transform;

            Vector3 forward = Enemy.transform.forward;
            Vector3 direction = target.position - Enemy.transform.position;
            if (Vector3.Angle(forward, direction) > viewAngle)
                return false;

            Vector3 forwardProjected = Vector3.Project(direction, forward);
            if (forwardProjected.sqrMagnitude > viewDistance * viewDistance)
                return false;

            return true;
        }

        protected internal override void DrawGizmos()
        {
            float radius = viewDistance * Mathf.Tan(viewAngle * Mathf.Deg2Rad);
            GizmosDrawCone(Enemy.transform.position, Enemy.transform.forward, radius, viewDistance);
            if (Application.isPlaying && isInView)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(Enemy.transform.position, target.position);
            }
        }

        private void GizmosDrawCone(Vector3 position, Vector3 direction, float radius, float height)
        {
#if UNITY_EDITOR
            direction.Normalize();
            Vector3 baseCenter = position + direction * height;
            Gizmos.color = Handles.color = 0.7f * Color.white;
            Handles.DrawWireDisc(baseCenter, direction, radius);
            Vector3 left = Vector3.Cross(direction, Vector3.up);
            Gizmos.DrawLine(position, baseCenter + left * radius);
            Gizmos.DrawLine(position, baseCenter - left * radius);
            Vector3 down = Vector3.Cross(direction, left);
            Gizmos.DrawLine(position, baseCenter + down * radius);
            Gizmos.DrawLine(position, baseCenter - down * radius);
#endif
        }
    }
}
