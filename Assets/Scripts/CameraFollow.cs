using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;               // Drag Player here
    public float smoothSpeed = 8f;         // Higher = snappier
    public Vector3 offset = new Vector3(0, 0, -10);

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}