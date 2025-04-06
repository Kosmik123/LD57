using UnityEngine;

namespace Enemies
{
    [AddComponentMenu(Paths.ActivationConditions + "Any Condition")]
    public class AnyCondition : EnemyActivationCondition
    {
        [SerializeField]
        private EnemyActivationCondition[] conditions;

        public override bool CheckCondition()
        {
            foreach (var condition in conditions)
                if (condition.CheckCondition())
                    return true;

            return false;
        }
    }
}
