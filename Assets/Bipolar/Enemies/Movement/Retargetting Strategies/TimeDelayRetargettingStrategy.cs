using Bipolar;
using System.Collections;
using UnityEngine;

namespace Enemies.Movement
{
    [System.Serializable]
    public class TimeDelayRetargettingStrategy : EnemyRetargettingStrategy
    {
        [SerializeField]
        private RandomFloat delay;

        private MonoBehaviour coroutineRunner;
        protected override void OnInit()
        {
            coroutineRunner = Enemy.GetComponentInChildren<MonoBehaviour>();
            coroutineRunner.StartCoroutine(WaitForRetargetting());
        }

        private IEnumerator WaitForRetargetting()
        {
            while (coroutineRunner)
            {
                yield return new WaitForSeconds(delay);
                RequestRetargetting();
            }
        }
    }
}
