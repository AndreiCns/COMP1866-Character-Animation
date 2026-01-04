using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CrouchHandler : MonoBehaviour
{
    // Handles toggling crouch via the new Input System and optionally adjusts
    // the CharacterController capsule to match crouch/stand heights.

    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string crouchActionName = "Crouch";

    [Header("Capsule Settings")]
    [SerializeField] private bool adjustCapsule = true;
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private Vector3 standingCenter = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0f, 0.6f, 0f);

    [Header("Stability")]
    [Tooltip("Prevents spam toggling mid-transition which can cause stuck states.")]
    [SerializeField] private float toggleCooldown = 0.25f;

    // Animator parameter names (MATCH YOUR CONTROLLER)
    private const string IS_CROUCHING = "isCrouching";
    private const string ENTER_CROUCH = "enterCrouch";
    private const string EXIT_CROUCH = "exitCrouch";
    private const string IS_IN_COVER = "isInCover";

    // Cached input map & action (safe lookups are used in Awake)
    private InputActionMap playerMap;
    private InputAction crouchAction;

    private float nextAllowedToggleTime;

    void Reset()
    {
        // Provide sensible defaults in the inspector when adding the component
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    void Awake()
    {
        // Auto-fill references if not set in inspector
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();
        if (!characterController) characterController = GetComponent<CharacterController>();

        // Require essential components to operate
        if (!playerInput || !animator || !characterController)
        {
            Debug.LogError("[CrouchHandler] Missing PlayerInput, Animator, or CharacterController.");
            enabled = false;
            return;
        }

        if (playerInput.actions == null)
        {
            Debug.LogError("[CrouchHandler] PlayerInput has no Actions asset assigned.");
            enabled = false;
            return;
        }

        // Find the action map and action without throwing exceptions. This is safer
        // than using the "map/action" path with throwOnError=true which can throw.
        playerMap = playerInput.actions.FindActionMap(actionMapName, false);
        if (playerMap == null)
        {
            Debug.LogError($"[CrouchHandler] ActionMap '{actionMapName}' not found on PlayerInput. Check the Actions asset and the field value.");
            enabled = false;
            return;
        }

        crouchAction = playerMap.FindAction(crouchActionName, false);
        if (crouchAction == null)
        {
            Debug.LogError($"[CrouchHandler] Action '{crouchActionName}' not found in map '{actionMapName}'. Check the Actions asset and the field value.");
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        // Enable the action map and action and subscribe safely
        playerMap?.Enable();
        crouchAction?.Enable();

        if (crouchAction != null)
            crouchAction.performed += OnCrouchPressed;
    }

    void OnDisable()
    {
        // Unsubscribe safely and disable actions/maps
        if (crouchAction != null)
            crouchAction.performed -= OnCrouchPressed;

        crouchAction?.Disable();
        playerMap?.Disable();
    }

    private void OnCrouchPressed(InputAction.CallbackContext _)
    {
        // Simple cooldown to prevent rapid toggles which can confuse animation state
        if (Time.time < nextAllowedToggleTime)
            return;

        // Future-proof: block toggle in cover (change later if you want)
        if (animator.GetBool(IS_IN_COVER))
            return;

        nextAllowedToggleTime = Time.time + toggleCooldown;

        bool isCurrentlyCrouching = animator.GetBool(IS_CROUCHING);
        if (!isCurrentlyCrouching) EnterCrouch();
        else ExitCrouch();
    }

    private void EnterCrouch()
    {
        // Clear triggers to avoid leftover firing
        animator.ResetTrigger(ENTER_CROUCH);
        animator.ResetTrigger(EXIT_CROUCH);

        animator.SetBool(IS_CROUCHING, true);
        animator.SetTrigger(ENTER_CROUCH);

        if (!adjustCapsule) return;

        // Immediately snap capsule to crouch values. If you prefer a smooth
        // transition, consider lerping height/center over time instead.
        characterController.height = crouchHeight;
        characterController.center = crouchCenter;
    }

    private void ExitCrouch()
    {
        // Clear triggers to avoid leftover firing
        animator.ResetTrigger(ENTER_CROUCH);
        animator.ResetTrigger(EXIT_CROUCH);

        animator.SetBool(IS_CROUCHING, false);
        animator.SetTrigger(EXIT_CROUCH);

        if (!adjustCapsule) return;

        characterController.height = standingHeight;
        characterController.center = standingCenter;
    }
}
