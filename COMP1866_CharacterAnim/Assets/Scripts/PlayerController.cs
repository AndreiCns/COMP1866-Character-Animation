using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // Handles camera-relative movement, running, rotation, and gravity.
    // Respects animator-driven cover state to lock movement.

    [Header("Movement Settings")]
    public float walkSpeed = 3.5f; // walking speed
    public float runSpeed = 5.5f; // running speed
    public float rotationSpeed = 12f; // rotation speed
    public float gravity = -20f; // gravity force

    [Header("References")]
    public Camera cam; // optional camera for camera-relative movement

    private CharacterController cc; // cached CharacterController
    private Animator anim; // cached Animator

    private Vector3 velocity; // vertical velocity
    private int aimLayer; // animator aim layer index

    // Animator parameter names
    private const string IS_IN_COVER = "isInCover";
    private const string SPEED_PARAM = "Speed";

    [SerializeField] private float speedDampTime = 0.1f; // animator damping

    void Awake()
    {
        // Cache components and validate
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

        aimLayer = anim.GetLayerIndex("AimUpper");
    }

    void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    // Process movement input, rotation, and animator Speed param
    void HandleMovement()
    {
        var st0 = anim.GetCurrentAnimatorStateInfo(0);

        bool lockByTag =
            st0.IsTag("LockMove") ||
            (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("LockMove"));

        if (lockByTag)
        {
            anim.SetFloat(SPEED_PARAM, 0f, speedDampTime, Time.deltaTime);
            return;
        }

        // Lock movement while in cover and face the camera
        if (anim != null && anim.GetBool(IS_IN_COVER))
        {
            anim.SetFloat(SPEED_PARAM, 0f, speedDampTime, Time.deltaTime);

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

        // Read input (legacy Input API)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float moveSpeed = isRunning ? runSpeed : walkSpeed;

        // Camera-relative movement
        Vector3 camForward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 camRight = cam != null ? cam.transform.right : transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = (camForward * input.z + camRight * input.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // Rotate towards movement direction
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        Vector3 horizontalMove = moveDir * moveSpeed;

        // Map input to animator Speed (idle/walk/run)
        float speedParam = 0f;
        if (input.sqrMagnitude > 0.0001f)
            speedParam = isRunning ? 1f : 0.5f;

        if (anim != null)
            anim.SetFloat(SPEED_PARAM, speedParam, speedDampTime, Time.deltaTime);

        // Move character controller horizontally
        cc.Move(horizontalMove * Time.deltaTime);
    }

    // Apply gravity to vertical velocity
    void ApplyGravity()
    {
        if (cc.isGrounded && velocity.y < 0)
            velocity.y = -2f; // small negative to stay grounded

        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
