using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShootHandler : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string drawHolsterActionName = "Draw/Holster";
    [SerializeField] private string attackActionName = "Attack";
    public int aimMouseButton = 1;

    [Header("Animator Layers")]
    [SerializeField] private string upperLayerName = "UpperArmed";
    [SerializeField] private string coverUpperLayerName = "CoverUpper";
    [SerializeField] private float layerBlendSpeed = 12f;

    [Header("Animator Params")]
    [SerializeField] private string trigDraw = "Draw";
    [SerializeField] private string trigHolster = "Holster";
    [SerializeField] private string trigShoot = "Shoot";
    [SerializeField] private string trigCoverShoot = "coverShoot";
    [SerializeField] private string boolIsArmed = "isArmed";
    [SerializeField] private string boolIsAiming = "isAiming";
    [SerializeField] private string boolIsInCover = "isInCover";
    [SerializeField] private string boolIsCoverShooting = "isCoverShooting";

    [Header("Timing")]
    [SerializeField] private float coverShootHoldTime = 0.15f;

    private InputAction drawHolsterAction;
    private InputAction attackAction;

    private int upperLayer;
    private int coverUpperLayer;

    private bool isArmedLocal;
    private bool forceUpperLayerOn;
    private Coroutine holsterRoutine;

    void Awake()
    {
        playerInput = playerInput ? playerInput : GetComponent<PlayerInput>();
        animator = animator ? animator : GetComponent<Animator>();

        upperLayer = animator.GetLayerIndex(upperLayerName);
        coverUpperLayer = animator.GetLayerIndex(coverUpperLayerName);

        drawHolsterAction = playerInput.actions.FindAction($"{actionMapName}/{drawHolsterActionName}");
        attackAction = playerInput.actions.FindAction($"{actionMapName}/{attackActionName}");
    }

    void OnEnable()
    {
        drawHolsterAction.performed += OnDrawHolster;
        attackAction.performed += OnAttack;
        drawHolsterAction.Enable();
        attackAction.Enable();
    }

    void OnDisable()
    {
        drawHolsterAction.performed -= OnDrawHolster;
        attackAction.performed -= OnAttack;
    }

    void LateUpdate()
    {
        bool inCover = animator.GetBool(boolIsInCover);
        bool aiming = isArmedLocal && Input.GetMouseButton(aimMouseButton);

        animator.SetBool(boolIsAiming, aiming);

        bool coverAiming = inCover && aiming;

        // CoverUpper OVERRIDES everything while cover-aiming
        SetLayerWeight(coverUpperLayer, coverAiming ? 1f : 0f);

        // UpperArmed disabled during cover shooting
        float upperTarget =
            coverAiming ? 0f :
            (isArmedLocal || forceUpperLayerOn ? 1f : 0f);

        SetLayerWeight(upperLayer, upperTarget);
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
            animator.SetTrigger(trigDraw);
        }
        else
        {
            isArmedLocal = false;
            animator.SetBool(boolIsArmed, false);
            animator.SetBool(boolIsAiming, false);

            forceUpperLayerOn = true;
            animator.SetTrigger(trigHolster);
            holsterRoutine = StartCoroutine(DisableUpperAfterHolster());
        }
    }

    private void OnAttack(InputAction.CallbackContext _)
    {
        if (!isArmedLocal) return;

        bool inCover = animator.GetBool(boolIsInCover);

        if (inCover)
        {
            if (!animator.GetBool(boolIsAiming)) return;

            animator.SetTrigger(trigCoverShoot);
            StartCoroutine(CoverShootWindow());
            return;
        }

        animator.SetTrigger(trigShoot);
    }

    private IEnumerator CoverShootWindow()
    {
        animator.SetBool(boolIsCoverShooting, true);
        yield return new WaitForSeconds(coverShootHoldTime);
        animator.SetBool(boolIsCoverShooting, false);
    }

    private IEnumerator DisableUpperAfterHolster()
    {
        yield return new WaitForSeconds(0.4f);
        forceUpperLayerOn = false;
    }

    private void SetLayerWeight(int layer, float target)
    {
        if (layer < 0) return;
        float w = animator.GetLayerWeight(layer);
        animator.SetLayerWeight(layer, Mathf.Lerp(w, target, Time.deltaTime * layerBlendSpeed));
    }
}
