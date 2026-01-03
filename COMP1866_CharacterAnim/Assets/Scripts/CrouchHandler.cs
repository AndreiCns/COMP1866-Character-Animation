using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CrouchHandler : MonoBehaviour
{
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

    private InputAction crouchAction;
    private float nextAllowedToggleTime;

    void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();
        if (!characterController) characterController = GetComponent<CharacterController>();

        if (!playerInput || !animator || !characterController)
        {
            Debug.LogError("[CrouchHandler] Missing PlayerInput, Animator, or CharacterController.");
            enabled = false;
            return;
        }

        crouchAction = playerInput.actions.FindAction($"{actionMapName}/{crouchActionName}", true);
    }

    void OnEnable()
    {
        crouchAction.performed += OnCrouchPressed;
        crouchAction.Enable();
    }

    void OnDisable()
    {
        crouchAction.performed -= OnCrouchPressed;
    }

    private void OnCrouchPressed(InputAction.CallbackContext _)
    {
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
