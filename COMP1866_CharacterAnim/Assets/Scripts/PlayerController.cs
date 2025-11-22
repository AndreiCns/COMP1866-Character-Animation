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

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        aimLayer = anim.GetLayerIndex("AimUpper");
    }

    void Update()
    {
        HandleMovement();
        HandleAimAndShoot();
        ApplyGravity();
    }

    // ------------------------------ MOVEMENT ------------------------------ //
    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v).normalized;

        // Animator "Speed"
        float moveAmount = Mathf.Clamp01(Mathf.Abs(h) + Mathf.Abs(v));
        anim.SetFloat("Speed", moveAmount);

        if (moveAmount > 0.01f)
        {
            // Move relative to camera
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * v + camRight * h;
            moveDir.Normalize();

            // Apply movement
            float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            cc.Move(moveDir * targetSpeed * Time.deltaTime);

            // Rotate to movement direction
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    // ------------------------------ AIMING & SHOOTING ------------------------------ //
    void HandleAimAndShoot()
    {
        bool aiming = Input.GetMouseButton(1); // RMB
        bool shooting = Input.GetMouseButton(0); // LMB

        anim.SetBool("Aim", aiming);
        anim.SetBool("isShooting", shooting);

        // Enable the upper-body layer only while aiming
        if (aimLayer >= 0)
        {
            float targetWeight = aiming ? 1f : 0f;
            float currentWeight = anim.GetLayerWeight(aimLayer);
            anim.SetLayerWeight(aimLayer, Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * 10f));
        }
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
