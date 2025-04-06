using UnityEditor;
using UnityEngine;

namespace Enemies.Attacking
{
    public class ThrowAttackBehavior : AttackBehavior
    {
        [SerializeField]
        private Projectile projectilePrototype;
        [SerializeField]
        private Transform projectileOrigin;
        [SerializeField]
        private EnemyTargetProvider targetProvider;

        [HideInInspector]
        public Vector3 target;

        private void Reset()
        {
            projectileOrigin = transform;
        }

        public override void Attack()
        {
            var projectile = Instantiate(projectilePrototype, projectileOrigin.position, Quaternion.identity);
            targetProvider.DetermineNextTarget();
            Vector3 target = targetProvider.Target;
            projectile.Shoot(target);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ThrowAttackBehavior))]
    public class ThrowAttackBehaviorEditor : Editor
    {
        private void OnSceneGUI()
        {
            var @throw = target as ThrowAttackBehavior;
            Vector3 throwTarget = Handles.PositionHandle(
                @throw.target + @throw.transform.position, 
                Quaternion.identity);
            @throw.target = throwTarget - @throw.transform.position;

            Handles.color = Color.red;
            Handles.DrawWireDisc(throwTarget, Vector3.up, 0.3f);
            Handles.DrawLine(throwTarget + Vector3.left * 0.15f, throwTarget + Vector3.left * 0.4f);
            Handles.DrawLine(throwTarget + Vector3.right * 0.15f, throwTarget + Vector3.right * 0.4f);
            Handles.DrawLine(throwTarget + Vector3.forward * 0.15f, throwTarget + Vector3.forward * 0.4f);
            Handles.DrawLine(throwTarget + Vector3.back * 0.15f, throwTarget + Vector3.back * 0.4f);
        }
    }
#endif
}
