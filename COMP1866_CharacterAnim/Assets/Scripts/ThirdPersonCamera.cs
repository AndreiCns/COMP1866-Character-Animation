using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ThirdPersonOTS_Camera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;              // Player root
    public Transform aimPivot;            // Empty GameObject at chest height (recommended)

    [Header("Animator Gating (NEW)")]
    public Animator animator;             // Player Animator
    public string boolIsArmed = "isArmed";
    public string boolIsAiming = "isAiming"; // optional but recommended
    public string boolIsCrouching = "isCrouching"; // NEW

    [Header("Offsets")]
    public float height = 1.6f;
    public float distance = 3.2f;
    public float shoulderOffset = 0.45f;

    [Header("Crouch Camera Offset (NEW)")]
    public float crouchDistanceOffset = -0.45f; // closer
    public float crouchHeightOffset = -0.25f;   // lower
    public float crouchLerpSpeed = 8f;

    [Header("Look")]
    public float mouseSensitivity = 2.5f;
    public float minPitch = -35f;
    public float maxPitch = 70f;

    [Header("Aim (RMB)")]
    public bool enableAimZoom = true;
    public float aimDistance = 2.0f;
    public float aimShoulderOffset = 0.55f;
    public float normalFov = 70f;
    public float aimFov = 55f;

    [Header("Smoothing")]
    public float positionSmooth = 0.06f;
    public float rotationSmooth = 12f;
    public float fovSmooth = 10f;

    [Header("Collision")]
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;
    public LayerMask collisionMask = ~0;

    [Header("Crouch Camera Timing")]
    public float crouchCameraDelay = 0.25f; // delay before camera moves (seconds)


    float yaw;
    float pitch;
    float currentShoulder;
    float crouchTimer;
    bool wasCrouching;


    Vector3 posVel;
    Quaternion currentRot;

    Camera cam;

    // NEW: cache base values so crouch offset is always relative
    float baseHeight;
    float baseDistance;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = normalFov;
        currentRot = transform.rotation;
        currentShoulder = shoulderOffset;

        // Auto-grab animator if not assigned
        if (!animator && target)
            animator = target.GetComponentInChildren<Animator>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target)
            yaw = target.eulerAngles.y;

        // NEW: cache base offsets
        baseHeight = height;
        baseDistance = distance;
    }

    void LateUpdate()
    {
        if (!target) return;

        // Shoulder swap (Q)
        if (Input.GetKeyDown(KeyCode.Q))
            shoulderOffset *= -1f;

        // Mouse look
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // ─────────────────────────────────────────────
        // 🔒 ANIMATOR GATING
        // ─────────────────────────────────────────────

        bool isArmed = animator && animator.GetBool(boolIsArmed);

        // Recommended: zoom follows animator aiming state
        bool aiming =
            enableAimZoom &&
            isArmed &&
            animator &&
            animator.GetBool(boolIsAiming);

        // NEW: crouch state
        bool isCrouching = animator && animator.GetBool(boolIsCrouching);

        // Detect crouch state change
        if (isCrouching && !wasCrouching)
        {
            // Just started crouching
            crouchTimer = 0f;
        }

        if (!isCrouching)
        {
            // Reset when standing
            crouchTimer = 0f;
        }

        wasCrouching = isCrouching;

        // Count time while crouching
        if (isCrouching)
            crouchTimer += Time.deltaTime;

        // Apply crouch offset only after delay
        bool applyCrouchCamera = isCrouching && crouchTimer >= crouchCameraDelay;

        float targetBaseDistance =
            baseDistance + (applyCrouchCamera ? crouchDistanceOffset : 0f);

        float targetBaseHeight =
            baseHeight + (applyCrouchCamera ? crouchHeightOffset : 0f);

        distance = Mathf.Lerp(
            distance,
            targetBaseDistance,
            crouchLerpSpeed * Time.deltaTime);

        height = Mathf.Lerp(
            height,
            targetBaseHeight,
            crouchLerpSpeed * Time.deltaTime);

        // Aim distance overrides crouch distance (crouch still affects height)
        float targetDist = aiming ? aimDistance : distance;

        float targetShoulder = aiming
            ? aimShoulderOffset * Mathf.Sign(shoulderOffset)
            : shoulderOffset;

        currentShoulder = Mathf.Lerp(
            currentShoulder,
            targetShoulder,
            rotationSmooth * Time.deltaTime);

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);

        // Stable pivot (do NOT use head bone)
        Vector3 pivotPos =
            (aimPivot ? aimPivot.position : target.position) + Vector3.up * height;

        // Desired camera position
        Vector3 desiredPos =
            pivotPos
            + targetRot * Vector3.right * currentShoulder
            - targetRot * Vector3.forward * targetDist;

        // Collision
        Vector3 dir = desiredPos - pivotPos;
        float dist = dir.magnitude;
        dir.Normalize();

        Vector3 finalPos = desiredPos;

        if (Physics.SphereCast(
            pivotPos,
            collisionRadius,
            dir,
            out RaycastHit hit,
            dist,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Max(0.1f, hit.distance - collisionPadding);
            finalPos = pivotPos + dir * safeDist;
        }

        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            finalPos,
            ref posVel,
            positionSmooth);

        // Smooth rotation
        currentRot = Quaternion.Slerp(
            currentRot,
            targetRot,
            rotationSmooth * Time.deltaTime);

        transform.rotation = currentRot;

        // Smooth FOV
        float targetFov = aiming ? aimFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFov,
            fovSmooth * Time.deltaTime);
    }
}
