using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // Simple player controller: handles camera-relative movement, running, rotation,
    // and gravity. Also respects an animator-driven cover state to lock movement.

    [Header("Movement Settings")]
    public float walkSpeed = 3.5f; // Walking speed of the player
    public float runSpeed = 5.5f; // Running speed of the player
    public float rotationSpeed = 12f; // Speed of player rotation
    public float gravity = -20f; // Gravity strength

    [Header("References")]
    public Camera cam; // Optional camera for camera-relative movement

    private CharacterController cc; // Cached CharacterController component
    private Animator anim; // Cached Animator component

    private Vector3 velocity; // Velocity vector, used for gravity
    private int aimLayer; // Animator layer index for aiming (if used)

    // NEW: cover gating
    private const string IS_IN_COVER = "isInCover"; // Animator boolean for cover state

    // Animator parameter names
    private const string SPEED_PARAM = "Speed"; // Animator parameter for speed

    // Smoothing for animator speed parameter (was hardcoded)
    [SerializeField] private float speedDampTime = 0.1f; // Damping time for speed transition

    void Awake()
    {
        // Cache required components
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();

        if (cc == null)
        {
            Debug.LogError("[PlayerController] CharacterController is required.");
            enabled = false;
            return;
        }

        if (anim == null)
        {
            Debug.LogError("[PlayerController] Animator is required.");
            enabled = false;
            return;
        }

        // Cache aim layer index if the animator contains it (safe call)
        aimLayer = anim.GetLayerIndex("AimUpper");
    }

    void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    // ------------------------------ MOVEMENT ------------------------------ //
    void HandleMovement()
    {
        // NEW: lock movement while in cover
        if (anim != null && anim.GetBool(IS_IN_COVER))
        {
            // While in cover we don't move; keep animator Speed at zero smoothly
            anim.SetFloat(SPEED_PARAM, 0f, speedDampTime, Time.deltaTime);

            // Face the CAMERA (so player sees the character's face)
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;

                if (toCam.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toCam.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }

            return;
        }

        // Read input (legacy Input API). Consider migrating to the Input System for consistency.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float moveSpeed = isRunning ? runSpeed : walkSpeed;

        // Move direction relative to camera if present, otherwise relative to character
        Vector3 camForward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 camRight = cam != null ? cam.transform.right : transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = (camForward * input.z + camRight * input.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // Rotate towards movement if there is input
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Apply horizontal movement (keep vertical velocity for gravity)
        Vector3 horizontalMove = moveDir * moveSpeed;

        // Animator "Speed" (0 = idle, 0.5 = walk, 1 = run)
        float speedParam = 0f;
        if (input.sqrMagnitude > 0.0001f)
            speedParam = isRunning ? 1f : 0.5f;

        if (anim != null)
            anim.SetFloat(SPEED_PARAM, speedParam, speedDampTime, Time.deltaTime);

        // MOVE the character controller
        cc.Move(horizontalMove * Time.deltaTime);
    }


    // ------------------------------ GRAVITY ------------------------------ //
    void ApplyGravity()
    {
        if (cc.isGrounded && velocity.y < 0)
            velocity.y = -2f; // small negative to keep grounded

        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
