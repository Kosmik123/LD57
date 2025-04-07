using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/Player Detected")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "Player Detected", message: "[Agent] has spotted [Player]", category: "Events", id: "07aa7e9b9fb8eb57442f4154607e0a72")]
public partial class PlayerDetected : EventChannelBase
{
    public delegate void PlayerDetectedEventHandler(GameObject Agent, GameObject Player);
    public event PlayerDetectedEventHandler Event; 

    public void SendEventMessage(GameObject Agent, GameObject Player)
    {
        Event?.Invoke(Agent, Player);
    }

    public override void SendEventMessage(BlackboardVariable[] messageData)
    {
        BlackboardVariable<GameObject> AgentBlackboardVariable = messageData[0] as BlackboardVariable<GameObject>;
        var Agent = AgentBlackboardVariable != null ? AgentBlackboardVariable.Value : default(GameObject);

        BlackboardVariable<GameObject> PlayerBlackboardVariable = messageData[1] as BlackboardVariable<GameObject>;
        var Player = PlayerBlackboardVariable != null ? PlayerBlackboardVariable.Value : default(GameObject);

        Event?.Invoke(Agent, Player);
    }

    public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
    {
        PlayerDetectedEventHandler del = (Agent, Player) =>
        {
            BlackboardVariable<GameObject> var0 = vars[0] as BlackboardVariable<GameObject>;
            if(var0 != null)
                var0.Value = Agent;

            BlackboardVariable<GameObject> var1 = vars[1] as BlackboardVariable<GameObject>;
            if(var1 != null)
                var1.Value = Player;

            callback();
        };
        return del;
    }

    public override void RegisterListener(Delegate del)
    {
        Event += del as PlayerDetectedEventHandler;
    }

    public override void UnregisterListener(Delegate del)
    {
        Event -= del as PlayerDetectedEventHandler;
    }
}

