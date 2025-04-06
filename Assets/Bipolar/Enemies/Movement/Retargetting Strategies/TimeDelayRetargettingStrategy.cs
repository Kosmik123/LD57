using Bipolar;
using UnityEngine;

namespace Enemies.Movement
{
    public class TimeDelayRetargettingStrategy : EnemyRetargettingStrategy
    {
        [SerializeField]
        private RandomFloat delay;

        private Timer timer;

        private void Awake()
        {
            timer = new Timer(this, onElapsed: Timer_OnElapsed);
        }

        private void OnEnable()
        {
            timer.Duration = delay;
            timer.Restart();
        }

        private void Timer_OnElapsed()
        {
            timer.Duration = delay;
            timer.Restart();
            RequestRetargetting();
        }

        private void OnDisable()
        {
            timer.Stop();
        }
    }
}
