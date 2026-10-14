using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "TriggerMeleeAttack", story: "[Self] attacks [Target] with windup", category: "Action/Combat", id: "7c3e91a4b5d84f06a2e1c9d03b6f58e7")]
public partial class TriggerMeleeAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private EnemyMeleeAttack attack;

    protected override Status OnStart()
    {
        if (Self.Value == null || Target.Value == null)
            return Status.Failure;

        attack = Self.Value.GetComponent<EnemyMeleeAttack>();
        if (attack == null || !attack.CanAttack)
            return Status.Failure;

        float distance = Vector3.Distance(Self.Value.transform.position, Target.Value.transform.position);
        if (distance > attack.Range)
            return Status.Failure;

        attack.StartAttack();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (attack == null)
            return Status.Failure;
        return attack.IsAttacking ? Status.Running : Status.Success;
    }

    protected override void OnEnd()
    {
        attack = null;
    }
}
