using UnityEditor;
using UnityEngine;

namespace Enemies.Targetting
{
    [AddComponentMenu(Paths.Targetting + "Patrolling Enemy Target Provider")]
    public class PatrollingEnemyTargetProvider : EnemyTargetProvider
    {
        [Header("Settings")]
        [SerializeField]
        private Vector3[] patrolPoints;

        [SerializeField]
        private bool looped;

        [SerializeField]
        private int targetIndex;

        public override Vector3 GetNextTarget()
        {
            targetIndex++;
            if (looped)
            {
                targetIndex %= patrolPoints.Length;
                return patrolPoints[targetIndex ];
            }
            else
            {
                int pointsCoint = patrolPoints.Length;
                int index = pointsCoint - Mathf.Abs(targetIndex - pointsCoint + 1) - 1;
                if (index == 0)
                    targetIndex = 0;
                return patrolPoints[index];
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                var point = patrolPoints[i];
                Gizmos.DrawSphere(point, i == targetIndex ? 0.3f : 0.2f);

                int nextIndex = (i + 1) % patrolPoints.Length;
                if (looped || i < patrolPoints.Length - 1)
                   Gizmos.DrawLine(point, patrolPoints[nextIndex]);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PatrollingEnemyTargetProvider))]
    public class PatrollingEnemyTargetProviderEditor : Editor
    {
        private void OnSceneGUI()
        {
            var patrolPointsProperty = serializedObject.FindProperty("patrolPoints");
            for (int i = 0; i < patrolPointsProperty.arraySize; i++)
            {
                var positionProperty = patrolPointsProperty.GetArrayElementAtIndex(i);
                positionProperty.vector3Value = Handles.PositionHandle(positionProperty.vector3Value, Quaternion.identity);
            }
            patrolPointsProperty.serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
