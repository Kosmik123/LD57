using NaughtyAttributes;
using UnityEngine;

namespace Enemies.Targetting
{
    [AddComponentMenu(Paths.Targetting + "Player Position Target Provider")]
    public class PlayerPositionTargetProvider : EnemyTargetProvider
    {
        [SerializeField, Tag]
        private string playerTag;

        private Transform playerTransform;
        public Transform PlayerTransform
        {
            get
            {
                if (playerTransform == null)
                    playerTransform = GameObject.FindGameObjectWithTag(playerTag).transform;
                return playerTransform;
            }
        }

        public override Vector3 GetNextTarget()
        {
            return PlayerTransform.position;
        }
    }
}
