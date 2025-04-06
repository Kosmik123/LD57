using UnityEngine;
using UnityEngine.Events;
#if NAUGHTY_ATTRIBUTES
using NaughtyAttributes;
#endif

namespace Bipolar
{
    public class TimerBehavior : MonoBehaviour, ITimer
    {
        public System.Action OnElapsed { get; set; }

        [SerializeField, Min(0)]
        private float speed = 1;
        public float Speed
        {
            get => speed;
            set
            {
                speed = value;
            }
        }

        [SerializeField, Min(0.0001f)]
        private float duration;
        public float Duration
        {
            get => duration;
            set
            {
                duration = value;
            }
        }

        [SerializeField]
        private bool autoReset;
        public bool AutoReset
        {
            get => autoReset;
            set
            {
                autoReset = value;
            }
        }

#if NAUGHTY_ATTRIBUTES
        [ReadOnly]
#endif
        [SerializeField]
        protected float time;
        public float CurrentTime
        {
            get => time;
            set
            {
                time = value;
            }
        }

        [SerializeField]
        private UnityEvent onElapsed;

        private void Update()
        {
            TimerHelper.UpdateTimer(ref time, speed, duration, OnElapsedAction);
        }

        private void OnElapsedAction()
        {
            if (autoReset == false)
                enabled = false;

            OnElapsed?.Invoke();
            onElapsed.Invoke();
        }
    }
}
