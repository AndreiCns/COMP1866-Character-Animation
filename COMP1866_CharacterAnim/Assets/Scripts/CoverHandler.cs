using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CoverHandler : MonoBehaviour
{
    // Handles entering/exiting cover when near a cover trigger. Uses the new
    // Input System to listen for an Interact action and drives animator
    // parameters to gate cover state. Also optionally snaps the player to the
    // cover surface to avoid clipping.

    [Header("References")]
    [SerializeField] private PlayerInput playerInput;       // PlayerInput component (new Input System)
    [SerializeField] private Animator animator;             // Player Animator
    [SerializeField] private CharacterController controller; // CharacterController for snapping

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";    // Action map to look up
    [SerializeField] private string interactActionName = "Interact"; // Interact action name

    [Header("Cover Detection")]
    [SerializeField] private string coverTag = "Cover";    // Collider tag used for cover triggers
    [SerializeField] private float snapDistance = 0.25f;     // not currently used but reserved for future
    [SerializeField] private float rotateToCoverSpeed = 14f; // speed to rotate player to face cover

    [Header("Animator Params")]
    [SerializeField] private string boolIsCrouching = "isCrouching"; // animator param: is crouching
    [SerializeField] private string boolIsInCover = "isInCover";    // animator param: in cover
    [SerializeField] private string trigEnterCover = "enterCover"; // animator trigger: enter
    [SerializeField] private string trigExitCover = "exitCover";   // animator trigger: exit
    [SerializeField] private string boolIsCoverShooting = "isCoverShooting"; // optional param

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true; // enable debug logs for development

    // Cached input map and action
    private InputActionMap playerMap;
    private InputAction interactAction;

    // Current nearby cover transform and collider
    private Transform currentCover;
    private Collider currentCoverCol;

    // Polled fallback for input
    private bool interactPressedThisFrame;

    void Reset()
    {
        // Set sensible defaults when component is first added
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Awake()
    {
        // Auto-fill references if not set in inspector
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();
        if (!controller) controller = GetComponent<CharacterController>();

        // Basic validation
        if (!playerInput || !animator || !controller)
        {
            Debug.LogError("[CoverHandler] Missing PlayerInput / Animator / CharacterController.");
            enabled = false;
            return;
        }

        if (playerInput.actions == null)
        {
            Debug.LogError("[CoverHandler] PlayerInput has no Actions asset assigned.");
            enabled = false;
            return;
        }

        // Find action map and action safely (no exceptions)
        playerMap = playerInput.actions.FindActionMap(actionMapName, false);
        if (playerMap == null)
        {
            Debug.LogError($"[CoverHandler] ActionMap '{actionMapName}' not found on PlayerInput. Check the Actions asset and the field value.");
            enabled = false;
            return;
        }

        interactAction = playerMap.FindAction(interactActionName, false);
        if (interactAction == null)
        {
            Debug.LogError($"[CoverHandler] Action '{interactActionName}' not found in map '{actionMapName}'. Check the Actions asset and the field value.");
            enabled = false;
            return;
        }

        if (debugLogs)
            Debug.Log($"[CoverHandler] Bound to {playerMap.name}/{interactAction.name}");
    }

    void OnEnable()
    {
        // Enable map/action and subscribe to callback safely
        playerMap?.Enable();
        interactAction?.Enable();

        if (interactAction != null)
            interactAction.performed += OnInteract;

        if (debugLogs)
            Debug.Log("[CoverHandler] Enabled + listening for Interact.");
    }

    void OnDisable()
    {
        // Unsubscribe and disable map/action
        if (interactAction != null)
            interactAction.performed -= OnInteract;

        interactAction?.Disable();
        playerMap?.Disable();

        if (debugLogs)
            Debug.Log("[CoverHandler] Disabled.");
    }

    void Update()
    {
        // Poll as a fallback so input works even if events don't fire in some setups
        if (interactAction != null)
            interactPressedThisFrame = interactAction.WasPressedThisFrame();
        else
            interactPressedThisFrame = false;

        if (interactPressedThisFrame)
        {
            if (debugLogs) Debug.Log("[CoverHandler] Interact polled in Update.");
            TryToggleCover();
        }

        // If not in cover or no current cover, skip rotation
        if (!animator.GetBool(boolIsInCover) || currentCover == null)
            return;

        // Gently rotate player to face cover forward while in cover
        Vector3 forward = currentCover.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(forward);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotateToCoverSpeed);
        }
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (debugLogs) Debug.Log("[CoverHandler] OnInteract callback invoked.");

        bool isCrouching = animator.GetBool(boolIsCrouching);
        bool isInCover = animator.GetBool(boolIsInCover);

        if (debugLogs)
        {
            Debug.Log($"[CoverHandler] Interact pressed | crouching={isCrouching} inCover={isInCover} nearCover={(currentCover ? currentCover.name : "NO")}");
        }

        if (!isInCover)
        {
            if (!isCrouching) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not crouching."); return; }
            if (!currentCover) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not near a Cover trigger."); return; }

            EnterCover();
        }
        else
        {
            ExitCover();
        }
    }

    private void TryToggleCover()
    {
        bool isCrouching = animator.GetBool(boolIsCrouching);
        bool isInCover = animator.GetBool(boolIsInCover);

        if (debugLogs)
            Debug.Log($"[CoverHandler] Interact (polled) | crouching={isCrouching} inCover={isInCover} nearCover={(currentCover ? currentCover.name : "NO")}");

        if (!isInCover)
        {
            if (!isCrouching) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not crouching."); return; }
            if (!currentCover) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not near cover."); return; }
            EnterCover();
        }
        else
        {
            ExitCover();
        }
    }

    private void EnterCover()
    {
        // Set animator state/trigger for entering cover
        animator.ResetTrigger(trigExitCover);
        animator.SetBool(boolIsInCover, true);
        animator.SetTrigger(trigEnterCover);

        // Clear optional cover-shooting flag if present
        if (HasParam(boolIsCoverShooting))
            animator.SetBool(boolIsCoverShooting, false);

        if (!currentCoverCol) return;

        // Snap player to the cover surface with a small stand-off so they don't clip into the collider
        Vector3 playerPos = transform.position;
        Vector3 closest = currentCoverCol.ClosestPoint(playerPos);

        Vector3 coverForward = currentCover.forward;
        coverForward.y = 0f;
        coverForward.Normalize();

        Vector3 coverToPlayer = (playerPos - currentCover.position);
        coverToPlayer.y = 0f;

        // Determine which side of the cover the player is on
        float side = Vector3.Dot(coverToPlayer, coverForward);
        Vector3 pushDir = (side >= 0f) ? coverForward : -coverForward;

        float standOff = (controller ? controller.radius : 0.3f) + 0.05f;

        Vector3 snapPos = closest + pushDir * standOff;
        snapPos.y = transform.position.y;

        controller.Move(snapPos - transform.position);
    }

    private void ExitCover()
    {
        // Reset animator triggers and params for leaving cover
        animator.ResetTrigger(trigEnterCover);

        if (HasParam(boolIsCoverShooting))
            animator.SetBool(boolIsCoverShooting, false);

        animator.SetBool(boolIsInCover, false);
        animator.SetTrigger(trigExitCover);

        if (debugLogs) Debug.Log("[CoverHandler] ExitCover fired.");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only register colliders with the configured tag as cover
        if (!other.CompareTag(coverTag)) return;
        currentCover = other.transform;
        currentCoverCol = other;
        if (debugLogs) Debug.Log($"[CoverHandler] Near cover: {currentCover.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        // Clear current cover when leaving the trigger collider
        if (currentCoverCol && other == currentCoverCol)
        {
            if (debugLogs) Debug.Log($"[CoverHandler] Left cover: {currentCover.name}");
            currentCover = null;
            currentCoverCol = null;
        }
    }

    private bool HasParam(string param)
    {
        // Check if animator contains a parameter named `param`.
        if (string.IsNullOrEmpty(param)) return false;
        foreach (var p in animator.parameters)
            if (p.name == param) return true;
        return false;
    }
}
