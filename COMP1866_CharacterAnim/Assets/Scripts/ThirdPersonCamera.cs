using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    // Third-person camera with aim-zoom, shoulder offset, crouch offsets, and collision.

    [Header("Target")]
    public Transform target;              // player root
    public Transform aimPivot;            // chest pivot for aiming

    [Header("Animator Gating (NEW)")]
    public Animator animator;             // player animator
    public string boolIsArmed = "isArmed";
    public string boolIsAiming = "isAiming";
    public string boolIsCrouching = "isCrouching";

    [Header("Offsets")]
    public float height = 1.6f;           // pivot height above player
    public float distance = 3.2f;         // follow distance
    public float shoulderOffset = 0.45f;  // horizontal shoulder offset

    [Header("Crouch Camera Offset (NEW)")]
    public float crouchDistanceOffset = -0.45f; // closer when crouching
    public float crouchHeightOffset = -0.25f;   // lower when crouching
    public float crouchLerpSpeed = 8f;          // transition speed

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
    public float positionSmooth = 0.06f;  // SmoothDamp time
    public float rotationSmooth = 12f;    // rotation lerp speed
    public float fovSmooth = 10f;         // FOV lerp speed

    [Header("Collision")]
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;
    public LayerMask collisionMask = ~0;

    [Header("Crouch Camera Timing")]
    public float crouchCameraDelay = 0.25f; // delay before applying crouch offset

    // Internal state
    float yaw;
    float pitch;
    float currentShoulder;
    float crouchTimer;
    bool wasCrouching;

    Vector3 posVel;
    Quaternion currentRot;

    Camera cam;

    float baseHeight;
    float baseDistance;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
            cam.fieldOfView = normalFov;

        currentRot = transform.rotation;
        currentShoulder = shoulderOffset;

        if (!animator && target)
            animator = target.GetComponentInChildren<Animator>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target)
            yaw = target.eulerAngles.y;

        baseHeight = height;
        baseDistance = distance;
    }

    void LateUpdate()
    {
        if (!target) return;

        // Shoulder swap (press Q)
        if (Input.GetKeyDown(KeyCode.Q))
            shoulderOffset *= -1f;

        // Mouse look
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        yaw += mx * mouseSensitivity * 100f * Time.deltaTime;
        pitch -= my * mouseSensitivity * 100f * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Animator gating
        bool hasAnimator = animator != null;
        bool isArmed = hasAnimator && animator.GetBool(boolIsArmed);
        bool isAiming = hasAnimator && animator.GetBool(boolIsAiming);
        bool isCrouching = hasAnimator && animator.GetBool(boolIsCrouching);

        bool aiming = enableAimZoom && isArmed && isAiming;

        // Crouch timing
        if (isCrouching && !wasCrouching) crouchTimer = 0f;
        if (!isCrouching) crouchTimer = 0f;
        wasCrouching = isCrouching;
        if (isCrouching) crouchTimer += Time.deltaTime;

        bool applyCrouchCamera = isCrouching && crouchTimer >= crouchCameraDelay;

        float targetBaseDistance = baseDistance + (applyCrouchCamera ? crouchDistanceOffset : 0f);
        float targetBaseHeight = baseHeight + (applyCrouchCamera ? crouchHeightOffset : 0f);

        distance = Mathf.Lerp(distance, targetBaseDistance, crouchLerpSpeed * Time.deltaTime);
        height = Mathf.Lerp(height, targetBaseHeight, crouchLerpSpeed * Time.deltaTime);

        float targetDist = aiming ? aimDistance : distance;
        float targetShoulder = aiming ? aimShoulderOffset * Mathf.Sign(shoulderOffset) : shoulderOffset;
        currentShoulder = Mathf.Lerp(currentShoulder, targetShoulder, rotationSmooth * Time.deltaTime);

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivotPos = (aimPivot ? aimPivot.position : target.position) + Vector3.up * height;

        Vector3 desiredPos = pivotPos + targetRot * Vector3.right * currentShoulder - targetRot * Vector3.forward * targetDist;

        // Collision: sphere cast from pivot to desired position
        Vector3 dir = desiredPos - pivotPos;
        float dist = dir.magnitude;
        Vector3 finalPos = desiredPos;

        if (dist > 0.0001f)
        {
            Vector3 dirNormalized = dir / dist;
            if (Physics.SphereCast(pivotPos, collisionRadius, dirNormalized, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                float safeDist = Mathf.Max(0.1f, hit.distance - collisionPadding);
                finalPos = pivotPos + dirNormalized * safeDist;
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref posVel, positionSmooth);

        currentRot = Quaternion.Slerp(currentRot, targetRot, rotationSmooth * Time.deltaTime);
        transform.rotation = currentRot;

        float targetFov = aiming ? aimFov : normalFov;
        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovSmooth * Time.deltaTime);
    }
}
