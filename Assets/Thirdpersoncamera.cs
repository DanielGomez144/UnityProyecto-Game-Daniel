using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;                    
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
    public float distance = 4f;
    public float sensitivity = 3f;
    public float minPitch = -20f;
    public float maxPitch = 60f;
    public LayerMask obstacleMask = ~0;         

    float yaw;
    float pitch = 15f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yaw = target != null ? target.eulerAngles.y : 0f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 desired = pivot - rot * Vector3.forward * distance;

       
        if (Physics.Linecast(pivot, desired, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore)
            && hit.transform != target)
        {
            desired = hit.point + hit.normal * 0.2f;
        }

        transform.SetPositionAndRotation(desired, rot);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}