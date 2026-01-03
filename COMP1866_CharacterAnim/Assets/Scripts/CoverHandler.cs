using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CoverHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Cover Detection")]
    [SerializeField] private string coverTag = "Cover";
    [SerializeField] private float snapDistance = 0.25f;
    [SerializeField] private float rotateToCoverSpeed = 14f;

    [Header("Animator Params")]
    [SerializeField] private string boolIsCrouching = "isCrouching";
    [SerializeField] private string boolIsInCover = "isInCover";
    [SerializeField] private string trigEnterCover = "enterCover";
    [SerializeField] private string trigExitCover = "exitCover";
    [SerializeField] private string boolIsCoverShooting = "isCoverShooting"; // optional

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private InputActionMap playerMap;
    private InputAction interactAction;

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

        // Force-enable the Player action map (prevents “nothing happens” when maps get switched)
        playerMap = playerInput.actions.FindActionMap(actionMapName, true);
        interactAction = playerMap.FindAction(interactActionName, true);

        if (debugLogs)
            Debug.Log($"[CoverHandler] Bound to {playerMap.name}/{interactAction.name}");
    }

    void OnEnable()
    {
        playerMap.Enable();
        interactAction.Enable();
       

        if (debugLogs)
            Debug.Log("[CoverHandler] Enabled + listening for Interact.");
    }

    void OnDisable()
    {
        
    }

    void Update()
    {
        if (interactAction != null)
            interactPressedThisFrame = interactAction.WasPressedThisFrame();
        else
            interactPressedThisFrame = false;

        if (interactPressedThisFrame)
            TryToggleCover();

        if (!animator.GetBool(boolIsInCover) || currentCover == null)
            return;

        // Optional: gently rotate player to face cover forward
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

    private void OnInteract(InputAction.CallbackContext _)
    {
        bool isCrouching = animator.GetBool(boolIsCrouching);
        bool isInCover = animator.GetBool(boolIsInCover);

        if (debugLogs)
        {
            Debug.Log($"[CoverHandler] Interact pressed | crouching={isCrouching} inCover={isInCover} nearCover={(currentCover ? currentCover.name : "NO")}");
        }

        // Enter cover
        if (!isInCover)
        {
            if (!isCrouching) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not crouching."); return; }
            if (!currentCover) { if (debugLogs) Debug.Log("[CoverHandler] Refused: not near a Cover trigger."); return; }

            EnterCover();
        }
        // Exit cover
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
        animator.ResetTrigger(trigExitCover);
        animator.SetBool(boolIsInCover, true);
        animator.SetTrigger(trigEnterCover);

        // Stop any cover shooting flags if you still use them
        if (HasParam(boolIsCoverShooting))
            animator.SetBool(boolIsCoverShooting, false);

        if (!currentCoverCol) return;

        // Find closest point on the cover volume to the player
        Vector3 playerPos = transform.position;
        Vector3 closest = currentCoverCol.ClosestPoint(playerPos);

        // Use cover forward/back to decide which side the player is on
        Vector3 coverForward = currentCover.forward;
        coverForward.y = 0f;
        coverForward.Normalize();

        // Vector from cover center to player (flat)
        Vector3 coverToPlayer = (playerPos - currentCover.position);
        coverToPlayer.y = 0f;

        // If player is in front of the cover (dot > 0), push outward along +forward.
        // If player is behind it (dot < 0), push outward along -forward.
        float side = Vector3.Dot(coverToPlayer, coverForward);
        Vector3 pushDir = (side >= 0f) ? coverForward : -coverForward;

        // stand-off distance (hug wall)
        float standOff = (controller ? controller.radius : 0.3f) + 0.05f;

        // Final snap position: closest point + forced outward normal
        Vector3 snapPos = closest + pushDir * standOff;
        snapPos.y = transform.position.y;

        controller.Move(snapPos - transform.position);
    }


    private void ExitCover()
    {
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
        if (string.IsNullOrEmpty(param)) return false;
        foreach (var p in animator.parameters)
            if (p.name == param) return true;
        return false;
    }
}
