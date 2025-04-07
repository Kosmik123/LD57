using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[System.Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "RandomPointInRange",
    story: "Set [position] to random point around [center] in range between [min] and [max]",
    category: "Action",
    id: "ffea14fb44c75fefb3da2bc8f041893c")]
public partial class RandomPointInRangeAction : Action
{
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    [SerializeReference] public BlackboardVariable<Transform> Center;
    [SerializeReference] public BlackboardVariable<float> Min;
    [SerializeReference] public BlackboardVariable<float> Max;
    [SerializeReference] public BlackboardVariable<bool> Flat;

    protected override Status OnStart()
    {
        if (Center.Value == null)
            return Status.Failure;

        var point = Random.onUnitSphere;
        if (Flat)
            point.y = 0;
        point.Normalize();
        point *= GetRandomDistance();
        point += Center.Value.position;
        Position.Value = point;
        return Status.Success;
    }

    private float GetRandomDistance() => Random.Range(Min.Value, Max.Value);
}
