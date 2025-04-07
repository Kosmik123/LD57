using Bipolar.Prototyping;
using System.Linq;
using Unity.Behavior;
using UnityEngine;

public class Player : SceneSingleton<Player>
{
    [SerializeField]
    private RuntimeBlackboardAsset globalBlackboard;

    protected override void Awake()
    {
        base.Awake();
        globalBlackboard.Blackboard.Variables.First(v => v.Name == "Player").ObjectValue = gameObject;
    }
}
