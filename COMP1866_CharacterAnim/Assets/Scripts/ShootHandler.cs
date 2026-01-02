using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShootHandler : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;

    [Header("Input Action Names")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string drawHolsterActionName = "Draw/Holster";
    [SerializeField] private string attackActionName = "Attack";

    [Header("Aim Input (fallback)")]
    [Tooltip("If you don't have an Aim action, RMB will control isAiming (only when armed).")]
    public int aimMouseButton = 1; // RMB

    [Header("Animator Layer")]
    [SerializeField] private string upperLayerName = "UpperArmed";
    [SerializeField] private float layerBlendSpeed = 12f;

    [Header("Animator Params")]
    [SerializeField] private string trigDraw = "Draw";
    [SerializeField] private string trigHolster = "Holster";
    [SerializeField] private string trigShoot = "Shoot";
    [SerializeField] private string boolIsArmed = "isArmed";
    [SerializeField] private string boolIsAiming = "isAiming";

    [Header("UpperArmed State Names (must match STATE names in the Animator graph)")]
    [SerializeField] private string holsterStateName = "Holster";

    private InputAction drawHolsterAction;
    private InputAction attackAction;

    private int upperLayerIndex = -1;

    private bool isArmedLocal;
    private bool forceUpperLayerOn;      // keeps layer visible during holster
    private Coroutine holsterRoutine;

    void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();

        if (!playerInput || !animator)
        {
            Debug.LogError("[ShootHandler] Missing PlayerInput or Animator on this GameObject.");
            enabled = false;
            return;
        }

        upperLayerIndex = animator.GetLayerIndex(upperLayerName);
        if (upperLayerIndex < 0)
        {
            Debug.LogError($"[ShootHandler] Animator layer '{upperLayerName}' not found.");
            enabled = false;
            return;
        }

        var actions = playerInput.actions;
        drawHolsterAction = actions.FindAction($"{actionMapName}/{drawHolsterActionName}", false);
        attackAction = actions.FindAction($"{actionMapName}/{attackActionName}", false);

        if (drawHolsterAction == null)
            Debug.LogError($"[ShootHandler] Missing action {actionMapName}/{drawHolsterActionName}");
        if (attackAction == null)
            Debug.LogError($"[ShootHandler] Missing action {actionMapName}/{attackActionName}");
    }

    void OnEnable()
    {
        drawHolsterAction?.Enable();
        attackAction?.Enable();

        if (drawHolsterAction != null) drawHolsterAction.performed += OnDrawHolster;
        if (attackAction != null) attackAction.performed += OnAttack;
    }

    void OnDisable()
    {
        if (drawHolsterAction != null) drawHolsterAction.performed -= OnDrawHolster;
        if (attackAction != null) attackAction.performed -= OnAttack;
    }

    // Use LateUpdate so we overwrite whatever PlayerController wrote earlier this frame.
    void LateUpdate()
    {
        // Aim is ONLY allowed when armed.
        bool aimingNow = isArmedLocal && Input.GetMouseButton(aimMouseButton);
        animator.SetBool(boolIsAiming, aimingNow);

        // Upper layer should be ON while armed, OR while holster is playing.
        float targetWeight = (isArmedLocal || forceUpperLayerOn) ? 1f : 0f;
        float current = animator.GetLayerWeight(upperLayerIndex);
        animator.SetLayerWeight(upperLayerIndex, Mathf.Lerp(current, targetWeight, Time.deltaTime * layerBlendSpeed));
    }

    private void OnDrawHolster(InputAction.CallbackContext _)
    {
        bool goingArmed = !isArmedLocal;

        if (holsterRoutine != null)
        {
            StopCoroutine(holsterRoutine);
            holsterRoutine = null;
        }

        if (goingArmed)
        {
            isArmedLocal = true;
            animator.SetBool(boolIsArmed, true);

            // Keep layer on (it will stay on anyway because isArmedLocal = true)
            forceUpperLayerOn = true;

            animator.ResetTrigger(trigHolster);
            animator.SetTrigger(trigDraw);

            // Release force immediately; armed keeps it on.
            forceUpperLayerOn = false;
        }
        else
        {
            // Start holster, but DO NOT fade the upper layer out yet
            isArmedLocal = false;
            animator.SetBool(boolIsArmed, false);

            // Aim must drop instantly when holstering
            animator.SetBool(boolIsAiming, false);

            forceUpperLayerOn = true;

            animator.ResetTrigger(trigDraw);
            animator.SetTrigger(trigHolster);

            holsterRoutine = StartCoroutine(WaitForHolsterThenDisableLayer());
        }
    }

    private void OnAttack(InputAction.CallbackContext _)
    {
        // Shoot from BOTH ArmedIdle and Aim:
        // - If unarmed: ignore
        // - If armed: always trigger shoot (animator decides which state path to use)
        if (!isArmedLocal) return;

        animator.SetTrigger(trigShoot);
    }

    private IEnumerator WaitForHolsterThenDisableLayer()
    {
        // Wait until Holster state is actually active on the upper layer
        while (!IsInUpperState(holsterStateName))
            yield return null;

        // Wait until the holster clip is basically done
        while (IsInUpperState(holsterStateName) &&
               animator.GetCurrentAnimatorStateInfo(upperLayerIndex).normalizedTime < 0.95f)
            yield return null;

        forceUpperLayerOn = false;
        holsterRoutine = null;
    }

    private bool IsInUpperState(string stateName)
    {
        return animator.GetCurrentAnimatorStateInfo(upperLayerIndex).IsName(stateName);
    }
}
