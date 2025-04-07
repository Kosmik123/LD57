using UnityEngine;

namespace Enemies.Conditions
{
    public abstract class CompositeCondition : Condition
    {
        [SerializeReference, SubclassSelector]
        protected Condition[] conditions;
     
        protected internal override void DrawGizmos()
        {
            foreach (var condition in conditions)
                condition.DrawGizmos();
        }
    }

    [System.Serializable]
    public class Any : CompositeCondition
    {
        public override bool Check()
        {
            foreach (var condition in conditions)
                if (condition.Check())
                    return true;

            return false;
        }
    }

    [System.Serializable]
    public class All : CompositeCondition
    {
        public override bool Check()
        {
            foreach (var condition in conditions)
                if (condition.Check() == false)
                    return false;

            return true;
        }
    }
}
