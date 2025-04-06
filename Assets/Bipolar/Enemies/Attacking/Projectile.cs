using UnityEngine;

namespace Enemies.Attacking
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        private Rigidbody rb;
        public Rigidbody Rigidbody
        {
            get
            {
                if (rb == null)
                    rb = GetComponent<Rigidbody>();
                return rb;
            }
        }

        [SerializeField]
        private float horizontalSpeed;

        public void Shoot(Vector3 target)
        {
            Vector3 position = transform.position;
            Vector3 positionDelta = target - position;
            float heightDelta = positionDelta.y;
            float horizontalDistance = Mathf.Sqrt(positionDelta.x * positionDelta.x + positionDelta.z * positionDelta.z);
            
            Vector3 horizontalDirection = positionDelta;
            horizontalDirection.y = 0;
            horizontalDirection.Normalize();

            float flightDuration = horizontalDistance / horizontalSpeed;
            float startingVerticalSpeed = heightDelta / flightDuration - 0.5f * Physics.gravity.y * flightDuration;

            Vector3 startingVelocity = Vector3.up * startingVerticalSpeed + horizontalDirection * horizontalSpeed;
            Rigidbody.linearVelocity = startingVelocity;
        }
    }
}
