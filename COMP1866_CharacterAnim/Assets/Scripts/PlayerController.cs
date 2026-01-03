using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 5.5f;
    public float rotationSpeed = 12f;
    public float gravity = -20f;

    [Header("References")]
    public Camera cam;

    private CharacterController cc;
    private Animator anim;

    private Vector3 velocity;
    private int aimLayer;

    // NEW: cover gating
    private const string IS_IN_COVER = "isInCover";

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
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
        if (anim && anim.GetBool(IS_IN_COVER))
        {
            anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);

            // NEW: face camera
            // Face the CAMERA (so player sees the character's face)
            if (cam)
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


        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float moveSpeed = isRunning ? runSpeed : walkSpeed;

        // move direction relative to camera
        Vector3 camForward = cam ? cam.transform.forward : transform.forward;
        Vector3 camRight = cam ? cam.transform.right : transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = (camForward * input.z + camRight * input.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // rotate towards movement
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // apply horizontal movement (keep vertical for gravity)
        Vector3 horizontalMove = moveDir * moveSpeed;

        // Animator "Speed" (0 = idle, 0.5 = walk, 1 = run)
        float speedParam = 0f;
        if (input.sqrMagnitude > 0.0001f)
            speedParam = isRunning ? 1f : 0.5f;

        anim.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);

        // MOVE the character controller
        cc.Move(horizontalMove * Time.deltaTime);
    }


    // ------------------------------ GRAVITY ------------------------------ //
    void ApplyGravity()
    {
        if (cc.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
