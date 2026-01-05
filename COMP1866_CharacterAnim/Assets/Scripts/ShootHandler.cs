using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Manages drawing/holstering, shooting, and animator layer blending.
// Uses the new Input System; action map and action names are configurable.
public class ShootHandler : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string drawHolsterActionName = "Draw/Holster";
    [SerializeField] private string attackActionName = "Attack";
    public int aimMouseButton = 1; // right mouse button

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

    [Header("Shot FX")]
    [SerializeField] private ParticleSystem muzzleFlashR;
    [SerializeField] private ParticleSystem muzzleFlashL;

    [SerializeField] private AudioSource shotAudio;
    [SerializeField] private AudioClip shotClip;

    [Header("Alternate muzzle (R then L then R...)")]
    [SerializeField] private bool startWithRight = true;
    private bool shootRightNext;

    [Header("Timing")]
    [SerializeField] private float coverShootHoldTime = 0.15f;

    // Cached input map & actions
    private InputActionMap playerMap;
    private InputAction drawHolsterAction;
    private InputAction attackAction;

    private int upperLayer = -1;
    private int coverUpperLayer = -1;

    // Local state
    private bool isArmedLocal;
    private bool forceUpperLayerOn;
    private Coroutine holsterRoutine;

    void Awake()
    {
        // Auto-fill references and cache layer indices
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!animator) animator = GetComponent<Animator>();
        shootRightNext = startWithRight;

        if (!playerInput || playerInput.actions == null)
        {
            Debug.LogError("[ShootHandler] Missing PlayerInput or Actions asset.");
            enabled = false;
            return;
        }

        playerMap = playerInput.actions.FindActionMap(actionMapName, false);
        if (playerMap == null)
        {
            Debug.LogError($"[ShootHandler] ActionMap '{actionMapName}' not found on PlayerInput. Check the Actions asset and the field value.");
            enabled = false;
            return;
        }

        drawHolsterAction = playerMap.FindAction(drawHolsterActionName, false);
        attackAction = playerMap.FindAction(attackActionName, false);

        if (drawHolsterAction == null)
        {
            Debug.LogError($"[ShootHandler] Action '{drawHolsterActionName}' not found in map '{actionMapName}'.");
            enabled = false;
            return;
        }

        if (attackAction == null)
        {
            Debug.LogError($"[ShootHandler] Action '{attackActionName}' not found in map '{actionMapName}'.");
            enabled = false;
            return;
        }

        if (animator != null)
        {
            upperLayer = animator.GetLayerIndex(upperLayerName);
            coverUpperLayer = animator.GetLayerIndex(coverUpperLayerName);
        }
    }

    void OnEnable()
    {
        // Enable actions and subscribe
        playerMap?.Enable();
        drawHolsterAction?.Enable();
        attackAction?.Enable();

        if (drawHolsterAction != null)
            drawHolsterAction.performed += OnDrawHolster;

        if (attackAction != null)
            attackAction.performed += OnAttack;
    }

    void OnDisable()
    {
        // Unsubscribe and disable actions
        if (drawHolsterAction != null)
            drawHolsterAction.performed -= OnDrawHolster;

        if (attackAction != null)
            attackAction.performed -= OnAttack;

        drawHolsterAction?.Disable();
        attackAction?.Disable();
        playerMap?.Disable();

        if (holsterRoutine != null)
        {
            StopCoroutine(holsterRoutine);
            holsterRoutine = null;
        }
    }

    void LateUpdate()
    {
        if (animator == null) return;

        bool inCover = animator.GetBool(boolIsInCover);

        // Determine aiming using legacy mouse check (can migrate to Input System)
        bool aiming = isArmedLocal && Input.GetMouseButton(aimMouseButton);
        animator.SetBool(boolIsAiming, aiming);

        bool coverAiming = inCover && aiming;

        // Cover layer overrides when aiming from cover
        SetLayerWeight(coverUpperLayer, coverAiming ? 1f : 0f);

        // Upper layer follows armed state unless cover-aiming
        float upperTarget = coverAiming ? 0f : (isArmedLocal || forceUpperLayerOn ? 1f : 0f);
        SetLayerWeight(upperLayer, upperTarget);
    }

    private void OnDrawHolster(InputAction.CallbackContext _)
    {
        // Toggle armed state and fire animator triggers
        bool goingArmed = !isArmedLocal;

        if (holsterRoutine != null)
        {
            StopCoroutine(holsterRoutine);
            holsterRoutine = null;
        }

        if (goingArmed)
        {
            isArmedLocal = true;
            if (animator != null)
            {
                animator.SetBool(boolIsArmed, true);
                animator.SetTrigger(trigDraw);
            }
        }
        else
        {
            isArmedLocal = false;
            if (animator != null)
            {
                animator.SetBool(boolIsArmed, false);
                animator.SetBool(boolIsAiming, false);

                forceUpperLayerOn = true;
                animator.SetTrigger(trigHolster);
                holsterRoutine = StartCoroutine(DisableUpperAfterHolster());
            }
        }
    }

    private void OnAttack(InputAction.CallbackContext _)
    {
        if (!isArmedLocal || animator == null) return;

        bool inCover = animator.GetBool(boolIsInCover);

        if (inCover)
        {
            // Require aiming while in cover
            if (!animator.GetBool(boolIsAiming)) return;

            animator.SetTrigger(trigCoverShoot);
            PlayShotFx(forceRight: true);
            StartCoroutine(CoverShootWindow());
            return;
        }

        animator.SetTrigger(trigShoot);
        PlayShotFx();
    }

    private IEnumerator CoverShootWindow()
    {
        if (animator == null) yield break;

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
        if (animator == null || layer < 0) return;
        float w = animator.GetLayerWeight(layer);
        animator.SetLayerWeight(layer, Mathf.Lerp(w, target, Time.deltaTime * layerBlendSpeed));
    }

    private void PlayShotFx(bool forceRight = false)
    {
        // Play muzzle and SFX; alternate muzzles unless forced
        bool useRight = forceRight ? true : shootRightNext;

        var ps = useRight ? muzzleFlashR : muzzleFlashL;
        if (ps != null) ps.Play(true);

        if (shotAudio != null && shotClip != null)
            shotAudio.PlayOneShot(shotClip);

        if (!forceRight)
            shootRightNext = !shootRightNext;
    }
}
