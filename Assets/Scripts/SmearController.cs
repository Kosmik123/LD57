using UnityEngine;

public class SmearController : MonoBehaviour
{
    public Material smearMaterial;
    private Vector3 lastPosition;
    private Vector3 velocity;
    private float lastTrailTime;
    private bool trailActive;

    void Start()
    {
        lastPosition = transform.position;
        lastTrailTime = Time.time;
        smearMaterial.SetFloat("_TrailStartTime", Time.time);
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;
        velocity = (currentPosition - lastPosition) / Time.deltaTime;
        lastPosition = currentPosition;

        float speed = velocity.magnitude;
        if (speed > 0.1f)
        {
            if (!trailActive)
            {
                lastTrailTime = Time.time;
                smearMaterial.SetFloat("_TrailStartTime", lastTrailTime);
                trailActive = true;
            }
        }
        else
        {
            trailActive = false;
        }

        smearMaterial.SetVector("_SmearVelocity", velocity);
    }
}