using UnityEditor;
using UnityEngine;

namespace Enemies
{
    public class VisibleInConeActivationCondition : EnemyActivationCondition
    {
        [SerializeField]
        private Transform player;

        [SerializeField, Min(0)]
        private float viewDistance = 10;
        [SerializeField, Range(0, 90)]
        private float viewAngle = 30;

        private bool isInView;
        public override bool CheckCondition() => isInView;

        private void Update()
        {
            isInView = IsInView();
        }

        public bool IsInView()
        {
            Vector3 forward = transform.forward;
            Vector3 direction = player.position - transform.position;
            if (Vector3.Angle(forward, direction) > viewAngle)
                return false;

            Vector3 forwardProjected = Vector3.Project(direction, forward);
            if (forwardProjected.sqrMagnitude > viewDistance * viewDistance)
                return false;

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float radius = viewDistance * Mathf.Tan(viewAngle * Mathf.Deg2Rad);
            GizmosDrawCone(transform.position, transform.forward, radius, viewDistance);
            if (Application.isPlaying && isInView)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, player.position); 
            }
        }

        private void GizmosDrawCone(Vector3 position, Vector3 direction, float radius, float height)
        {
            direction.Normalize();
            Vector3 baseCenter = position + direction * height;
            Handles.color = 0.7f * Color.white;
            Gizmos.color = 0.7f * Color.white;
            Handles.DrawWireDisc(baseCenter, direction, radius);
            Vector3 left = Vector3.Cross(direction, Vector3.up);
            Gizmos.DrawLine(position, baseCenter + left * radius);
            Gizmos.DrawLine(position, baseCenter - left * radius);
            Vector3 down = Vector3.Cross(direction, left);
            Gizmos.DrawLine(position, baseCenter + down * radius);
            Gizmos.DrawLine(position, baseCenter - down * radius);
        }
#endif
    }
}
