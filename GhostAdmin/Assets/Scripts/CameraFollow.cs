using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.1f;   // 0 = suivi rigide
    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = smoothTime <= 0f
            ? goal
            : Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}