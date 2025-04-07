using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RotateTowards", story: "Rotate [transform] towards [target] with angular speed of [speed]", category: "Action/Transform", id: "0f2922a671ba933412b0703d9e2b978b")]
public partial class RotateTowardsAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Transform;
    [SerializeReference] public BlackboardVariable<Transform> Target;
    [SerializeReference] public BlackboardVariable<float> Speed = new BlackboardVariable<float>(300);
    [SerializeReference] public BlackboardVariable<float> AngleTolerance = new BlackboardVariable<float>(20);

    protected override Status OnUpdate()
    {
        if (Transform.Value == null || Target.Value == null)
            return Status.Failure;

        var directionToTarget = Target.Value.position - Transform.Value.position;
        float angle = Vector3.Angle(Transform.Value.forward, directionToTarget);
        if (angle < AngleTolerance.Value)
            return Status.Success;

        var targetRotation = Quaternion.LookRotation(directionToTarget, Transform.Value.up);
        Transform.Value.rotation = Quaternion.RotateTowards(Transform.Value.rotation, targetRotation, Speed.Value * Time.deltaTime);
        return Status.Running;
    }
}

