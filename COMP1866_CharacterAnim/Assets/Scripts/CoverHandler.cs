using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CoverHandler : MonoBehaviour
{
    // Manages entering/exiting cover using the new Input System and animator gating.
    // Optionally snaps player to cover surface to avoid clipping.

    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Cover Detection")]
    [SerializeField] private string coverTag = "Cover";
    [SerializeField] private float snapDistance = 0.25f; // reserved
    [SerializeField] private float rotateToCoverSpeed = 14f;

    [Header("Animator Params")]
    [SerializeField] private string boolIsCrouching = "isCrouching";
    [SerializeField] private string boolIsInCover = "isInCover";
    [SerializeField] private string trigEnterCover = "enterCover";
    [SerializeField] private string trigExitCover = "exitCover";
    [SerializeField] private string boolIsCoverShooting = "isCoverShooting";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Cached input map and action
    private InputActionMap playerMap;
    private InputAction interactAction;

    // Nearby cover refs
    private Transform currentCover;
    private Collider currentCoverCol;

    private bool interactPressedThisFrame;

    void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Awake()
    {
        // Auto-fill refs and validate
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();
        if (!controller) controller = GetComponent<CharacterController>();

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
        playerMap?.Enable();
        interactAction?.Enable();

        if (interactAction != null)
            interactAction.performed += OnInteract;

        if (debugLogs)
            Debug.Log("[CoverHandler] Enabled + listening for Interact.");
    }

    void OnDisable()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;

        interactAction?.Disable();
        playerMap?.Disable();

        if (debugLogs)
            Debug.Log("[CoverHandler] Disabled.");
    }

    void Update()
    {
        // Poll input as a fallback
        if (interactAction != null)
            interactPressedThisFrame = interactAction.WasPressedThisFrame();
        else
            interactPressedThisFrame = false;

        if (interactPressedThisFrame)
        {
            if (debugLogs) Debug.Log("[CoverHandler] Interact polled in Update.");
            TryToggleCover();
        }

        if (!animator.GetBool(boolIsInCover) || currentCover == null)
            return;

        // Rotate to face cover while in cover
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
        // Set animator state and clear optional flags
        animator.ResetTrigger(trigExitCover);
        animator.SetBool(boolIsInCover, true);
        animator.SetTrigger(trigEnterCover);

        if (HasParam(boolIsCoverShooting))
            animator.SetBool(boolIsCoverShooting, false);

        if (!currentCoverCol) return;

        // Snap player to cover with a small stand-off
        Vector3 playerPos = transform.position;
        Vector3 closest = currentCoverCol.ClosestPoint(playerPos);

        Vector3 coverForward = currentCover.forward;
        coverForward.y = 0f;
        coverForward.Normalize();

        Vector3 coverToPlayer = (playerPos - currentCover.position);
        coverToPlayer.y = 0f;

        float side = Vector3.Dot(coverToPlayer, coverForward);
        Vector3 pushDir = (side >= 0f) ? coverForward : -coverForward;

        float standOff = (controller ? controller.radius : 0.3f) + 0.05f;

        Vector3 snapPos = closest + pushDir * standOff;
        snapPos.y = transform.position.y;

        controller.Move(snapPos - transform.position);
    }

    private void ExitCover()
    {
        // Reset animator params when leaving cover
        animator.ResetTrigger(trigEnterCover);

        if (HasParam(boolIsCoverShooting))
            animator.SetBool(boolIsCoverShooting, false);

        animator.SetBool(boolIsInCover, false);
        animator.SetTrigger(trigExitCover);

        if (debugLogs) Debug.Log("[CoverHandler] ExitCover fired.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(coverTag)) return;
        currentCover = other.transform;
        currentCoverCol = other;
        if (debugLogs) Debug.Log($"[CoverHandler] Near cover: {currentCover.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentCoverCol && other == currentCoverCol)
        {
            if (debugLogs) Debug.Log($"[CoverHandler] Left cover: {currentCover.name}");
            currentCover = null;
            currentCoverCol = null;
        }
    }

    private bool HasParam(string param)
    {
        // Return true if animator contains the named parameter
        if (string.IsNullOrEmpty(param)) return false;
        foreach (var p in animator.parameters)
            if (p.name == param) return true;
        return false;
    }
}
