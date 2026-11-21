
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocidades")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;
    public float rotationSmoothTime = 0.1f;
    public float gravity = -20f;

    [Header("Salto")]
    public float jumpHeight = 1.5f;

    [Header("Agacharse")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;

    [Header("Referencias")]
    public Transform cameraTransform;

    [Header("Ruido")]
    public float runNoise = 8f;
    public float walkNoise = 4f;
    public float crouchNoise = 1.5f;

    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }
    public float NoiseRadius { get; private set; }

    CharacterController controller;
    float verticalVelocity;
    float rotationVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.height = standHeight;
        controller.center = new Vector3(0, standHeight / 2f, 0);

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        
        Vector3 input = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        bool moving = input.magnitude >= 0.1f;

        
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCrouch();
        }

        
        IsRunning = moving &&
                    !IsCrouching &&
                    Input.GetKey(KeyCode.LeftShift);

        float speed = IsCrouching
            ? crouchSpeed
            : (IsRunning ? runSpeed : walkSpeed);

        Vector3 move = Vector3.zero;

        
        if (moving)
        {
            float cameraYaw = cameraTransform != null
                ? cameraTransform.eulerAngles.y
                : transform.eulerAngles.y;

            float targetAngle =
                Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg
                + cameraYaw;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref rotationVelocity,
                rotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            move = Quaternion.Euler(0f, targetAngle, 0f)
                   * Vector3.forward * speed;
        }

        
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        
        if (controller.isGrounded &&
            !IsCrouching &&
            Input.GetKeyDown(KeyCode.Space))
        {
            verticalVelocity = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );
        }

        verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        
        if (!moving)
        {
            NoiseRadius = 0f;
        }
        else if (IsCrouching)
        {
            NoiseRadius = crouchNoise;
        }
        else if (IsRunning)
        {
            NoiseRadius = runNoise;
        }
        else
        {
            NoiseRadius = walkNoise;
        }
    }

    void ToggleCrouch()
    {
       
        if (IsCrouching)
        {
            if (!CanStandUp())
            {
                Debug.Log("No hay espacio suficiente para levantarse.");
                return;
            }

            IsCrouching = false;
        }
        else
        {
            IsCrouching = true;
        }

       
        float h = IsCrouching ? crouchHeight : standHeight;

        controller.height = h;
        controller.center = new Vector3(0, h / 2f, 0);
    }

    bool CanStandUp()
    {
        float radius = controller.radius;

        Vector3 center = transform.TransformPoint(
            new Vector3(0, standHeight / 2f, 0)
        );

        float halfSegment = Mathf.Max(
            standHeight / 2f - radius,
            0f
        );

        Vector3 point1 = center + Vector3.up * halfSegment;
        Vector3 point2 = center - Vector3.up * halfSegment;

        Collider[] hits = Physics.OverlapCapsule(
            point1,
            point2,
            radius,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            
            if (hit == controller ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
