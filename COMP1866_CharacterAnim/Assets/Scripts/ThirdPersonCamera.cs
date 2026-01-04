using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    // Third person over-the-shoulder camera with optional aim-zoom and crouch offsets.
    // Designed for a typical 3rd-person character with an Animator that can gate states
    // such as aiming/arming/crouching.

    [Header("Target")]
    public Transform target;              // Player root 
    public Transform aimPivot;            // Empty GameObject at chest height 

    [Header("Animator Gating (NEW)")]
    public Animator animator;             // Player Animator 
    public string boolIsArmed = "isArmed";
    public string boolIsAiming = "isAiming"; 
    public string boolIsCrouching = "isCrouching"; 

    [Header("Offsets")]
    public float height = 1.6f;           // camera pivot height above player root
    public float distance = 3.2f;         // default follow distance
    public float shoulderOffset = 0.45f;  // horizontal offset (to shoulder)

    [Header("Crouch Camera Offset (NEW)")]
    public float crouchDistanceOffset = -0.45f; // offset applied to distance when crouching (closer)
    public float crouchHeightOffset = -0.25f;   // offset applied to height when crouching (lower)
    public float crouchLerpSpeed = 8f;          // how fast camera transitions for crouch offsets

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
    public float positionSmooth = 0.06f;  // time for SmoothDamp
    public float rotationSmooth = 12f;    // lerp speed for rotation
    public float fovSmooth = 10f;         // lerp speed for FOV

    [Header("Collision")]
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;
    public LayerMask collisionMask = ~0;

    [Header("Crouch Camera Timing")]
    public float crouchCameraDelay = 0.25f; // delay before camera moves (seconds)

    // Internal state
    float yaw;
    float pitch;
    float currentShoulder;
    float crouchTimer;
    bool wasCrouching;

    Vector3 posVel;
    Quaternion currentRot;

    Camera cam;

    // cache base values so crouch offset is always relative
    float baseHeight;
    float baseDistance;

    void Awake()
    {
        // Cache and initialize
        cam = GetComponent<Camera>();
        if (cam != null)
            cam.fieldOfView = normalFov;

        currentRot = transform.rotation;
        currentShoulder = shoulderOffset;

        // Auto-find animator on target as a convenience if not manually assigned
        if (!animator && target)
            animator = target.GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // Lock cursor for an FPS-like mouselook. 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target)
            yaw = target.eulerAngles.y;

        // cache base offsets so crouch modifies them relative to original values
        baseHeight = height;
        baseDistance = distance;
    }

    void LateUpdate()
    {
        if (!target) return;

        // Shoulder swap (press Q to toggle left/right)
        if (Input.GetKeyDown(KeyCode.Q))
            shoulderOffset *= -1f;

        // Mouse look 
        // Multiply by 100 and Time.deltaTime to make sensitivity feel consistent across framerates
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        yaw += mx * mouseSensitivity * 100f * Time.deltaTime;
        pitch -= my * mouseSensitivity * 100f * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        
        // Animator gating: check animator states safely (avoid repeated null checks)
        
        bool hasAnimator = animator != null;
        bool isArmed = hasAnimator && animator.GetBool(boolIsArmed);
        bool isAiming = hasAnimator && animator.GetBool(boolIsAiming);
        bool isCrouching = hasAnimator && animator.GetBool(boolIsCrouching);

        
        bool aiming = enableAimZoom && isArmed && isAiming;

        // Detect crouch state change and time how long we've been crouching
        if (isCrouching && !wasCrouching)
        {
            // Just started crouching
            crouchTimer = 0f;
        }

        if (!isCrouching)
        {
            // Reset timer when standing
            crouchTimer = 0f;
        }

        wasCrouching = isCrouching;

        if (isCrouching)
            crouchTimer += Time.deltaTime;

        // Apply crouch offset only after specified delay
        bool applyCrouchCamera = isCrouching && crouchTimer >= crouchCameraDelay;

        float targetBaseDistance = baseDistance + (applyCrouchCamera ? crouchDistanceOffset : 0f);
        float targetBaseHeight = baseHeight + (applyCrouchCamera ? crouchHeightOffset : 0f);

        // Smoothly lerp the base distance/height towards target values
        distance = Mathf.Lerp(distance, targetBaseDistance, crouchLerpSpeed * Time.deltaTime);
        height = Mathf.Lerp(height, targetBaseHeight, crouchLerpSpeed * Time.deltaTime);

        // If aiming override distance (but keep crouch affecting height)
        float targetDist = aiming ? aimDistance : distance;

        float targetShoulder = aiming ? aimShoulderOffset * Mathf.Sign(shoulderOffset) : shoulderOffset;
        currentShoulder = Mathf.Lerp(currentShoulder, targetShoulder, rotationSmooth * Time.deltaTime);

        // Compute ideal rotation from look input
        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);

        // Stable pivot: prefer an aim pivot (e.g. chest) or fallback to target root
        Vector3 pivotPos = (aimPivot ? aimPivot.position : target.position) + Vector3.up * height;

        // Desired camera position in world space
        Vector3 desiredPos = pivotPos + targetRot * Vector3.right * currentShoulder - targetRot * Vector3.forward * targetDist;

        // Collision handling: cast a sphere from pivot to desired position and move camera in front of hit
        Vector3 dir = desiredPos - pivotPos;
        float dist = dir.magnitude;

        Vector3 finalPos = desiredPos;

        // Only perform normalization and SphereCast if there is a meaningful distance
        if (dist > 0.0001f)
        {
            Vector3 dirNormalized = dir / dist; // avoid calling Normalize which can allocate/generate NaN for zero

            if (Physics.SphereCast(pivotPos, collisionRadius, dirNormalized, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Move camera slightly in front of the collision point to avoid clipping
                float safeDist = Mathf.Max(0.1f, hit.distance - collisionPadding);
                finalPos = pivotPos + dirNormalized * safeDist;
            }
        }

        // Smoothly move camera position
        transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref posVel, positionSmooth);

        // Smoothly rotate towards target rotation
        currentRot = Quaternion.Slerp(currentRot, targetRot, rotationSmooth * Time.deltaTime);
        transform.rotation = currentRot;

        // Smoothly transition FOV when aiming
        float targetFov = aiming ? aimFov : normalFov;
        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovSmooth * Time.deltaTime);
    }
}
