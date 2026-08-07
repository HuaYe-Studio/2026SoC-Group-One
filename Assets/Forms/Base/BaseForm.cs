using System.Collections.Generic;
using AbilitySystem;
using UnityEngine;
public enum ActionState
{
    Idle,
    Moving,
    Jumping,
    Falling,
    WallCling,
    SpecialAction
}

public abstract class BaseForm : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private FormType formType;
    public FormType FormType => formType;

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 5f;

    [Header("Gravity")]
    [SerializeField] protected float gravityScale = 1f;
    [SerializeField] protected float fallGravityMultiplier = 1.5f;

    [Header("Ground Check")]
    [SerializeField] protected float groundCheckWidth = 0.8f;
    [SerializeField] protected float groundCheckHeight = 0.05f;
    [SerializeField] protected float groundCheckVerticalOffset = 0.02f;
    [SerializeField] protected LayerMask groundLayer;

    protected Rigidbody2D rb;
    protected Collider2D myCollider;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected PlayerController controller;
    protected PlayerStamina stamina;

    [Header("Ability Input")]
    [SerializeField] private List<AbilityInputBinding> abilityBindings = new();
    private readonly Dictionary<AbilityInputBinding, System.Action> _bindingHandlers = new();

    protected ActionState currentState = ActionState.Idle;
    protected float ignoreGroundUntil;

    protected bool IsGrounded { get; set; }
    public ActionState CurrentState => currentState;
    public Animator Animator => animator;

    protected static float HorizontalInput =>
        PlayerInputReader.HasInstance ? PlayerInputReader.Instance.MoveValue.x : 0f;

    protected static float VerticalInput =>
        PlayerInputReader.HasInstance ? PlayerInputReader.Instance.MoveValue.y : 0f;

    public virtual void Initialize(PlayerController ctrl)
    {
        controller = ctrl;
        rb = GetComponentInParent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        stamina = ctrl.GetComponent<PlayerStamina>();
    }

    protected virtual bool CanMove() =>
        currentState == ActionState.Idle ||
        currentState == ActionState.Moving ||
        currentState == ActionState.Jumping ||
        currentState == ActionState.Falling ||
        currentState == ActionState.WallCling;

    protected void UpdateIdleOrMovingState(float horizontal)
    {
        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            currentState = Mathf.Abs(horizontal) > 0.1f ? ActionState.Moving : ActionState.Idle;
    }

    public void SetActionState(ActionState state)
    {
        currentState = state;
    }

    public void SetAnimatorBool(string paramName, bool value)
    {
        if (animator != null)
            animator.SetBool(paramName, value);
    }

    public void SetAnimatorBool(int hash, bool value)
    {
        if (animator != null)
            animator.SetBool(hash, value);
    }

    public void ProcessInput(float horizontal)
    {
        if (currentState == ActionState.SpecialAction) return;

        DoMovement(horizontal);
        UpdateFacing(horizontal);

        HandleInput();
    }

    protected virtual void DoMovement(float horizontal)
    {
        if (!CanMove()) return;
        if (rb == null) return;

        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        UpdateIdleOrMovingState(horizontal);
    }

    protected virtual void UpdateFacing(float horizontal)
    {
        if (spriteRenderer == null) return;
        if (horizontal > 0.1f)
            spriteRenderer.flipX = false;
        else if (horizontal < -0.1f)
            spriteRenderer.flipX = true;
    }

    protected virtual void HandleInput() { }

    protected virtual void FixedUpdate()
    {
        PerformGroundCheck();
        UpdateAirState();
        HandleLanding();
        StaminaRecovery();
        ApplyGravity();
    }

    protected virtual Vector2 GetGroundCheckOrigin()
    {
        if (myCollider == null)
            return (Vector2)transform.position + Vector2.down * 0.5f;

        Bounds bounds = myCollider.bounds;
        return new Vector2(bounds.center.x, bounds.min.y) + Vector2.up * groundCheckVerticalOffset;
    }

    protected virtual Vector2 GetGroundCheckSize()
    {
        float width = myCollider != null ? myCollider.bounds.size.x * groundCheckWidth : 0.4f;
        return new Vector2(width, groundCheckHeight);
    }

    protected virtual void PerformGroundCheck()
    {
        if (Time.fixedTime < ignoreGroundUntil) return;

        Vector2 origin = GetGroundCheckOrigin();
        Vector2 size = GetGroundCheckSize();
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.05f, groundLayer);
        IsGrounded = hit.collider != null;
    }

    protected (bool left, bool right) DetectWalls(float checkDistance, float checkInset, int rayCount, LayerMask layer)
    {
        if (myCollider == null) return (false, false);
        Bounds bounds = myCollider.bounds;
        float startY = bounds.min.y + 0.1f;
        float endY = bounds.max.y - 0.1f;
        float step = rayCount > 1 ? (endY - startY) / (rayCount - 1) : 0f;
        float dist = checkDistance + checkInset;

        bool hitL = false, hitR = false;
        for (int i = 0; i < rayCount; i++)
        {
            float y = startY + step * i;
            Vector2 rightOrigin = new Vector2(bounds.max.x - checkInset, y);
            Vector2 leftOrigin  = new Vector2(bounds.min.x + checkInset, y);

            if (Physics2D.Raycast(rightOrigin, Vector2.right, dist, layer)) hitR = true;
            if (Physics2D.Raycast(leftOrigin,  Vector2.left,  dist, layer)) hitL = true;

            Debug.DrawRay(rightOrigin, Vector2.right * dist, hitR ? Color.green : Color.red);
            Debug.DrawRay(leftOrigin,  Vector2.left  * dist, hitL ? Color.green : Color.blue);
        }
        return (hitL, hitR);
    }

    protected virtual void UpdateAirState()
    {
        if (rb == null) return;

        if (currentState == ActionState.Jumping && rb.velocity.y < 0)
            currentState = ActionState.Falling;

        if (!IsGrounded && (currentState == ActionState.Idle || currentState == ActionState.Moving))
            currentState = ActionState.Falling;
    }

    protected virtual void HandleLanding()
    {
        if (rb == null) return;

        if (IsGrounded && (currentState == ActionState.Falling || currentState == ActionState.Jumping || currentState == ActionState.WallCling))
            currentState = Mathf.Abs(rb.velocity.x) > 0.1f ? ActionState.Moving : ActionState.Idle;
    }

    protected virtual void ApplyGravity()
    {
        if (rb == null) return;

        bool falling = rb.velocity.y < -0.1f;
        rb.gravityScale = falling ? gravityScale * fallGravityMultiplier : gravityScale;
    }

    protected virtual void StaminaRecovery()
    {
        if (IsGrounded && stamina != null && currentState != ActionState.SpecialAction)
            stamina.Restore(stamina.RecoverPerSecond * Time.fixedDeltaTime);
    }

    public virtual void OnFormActivated()
    {
        RegisterAbilityBindings();
    }

    public virtual void OnFormDeactivated()
    {
        UnregisterAbilityBindings();
    }

    private void RegisterAbilityBindings()
    {
        if (!PlayerInputReader.HasInstance) return;

        if (_bindingHandlers.Count > 0)
            UnregisterAbilityBindings();

        foreach (var binding in abilityBindings)
        {
            System.Action handler = () => binding.onInputFired.Invoke();
            _bindingHandlers[binding] = handler;
            SubscribeToSlot(binding.inputSlot, binding.phase, handler);
        }
    }

    private void UnregisterAbilityBindings()
    {
        if (!PlayerInputReader.HasInstance)
        {
            _bindingHandlers.Clear();
            return;
        }

        foreach (var kvp in _bindingHandlers)
            UnsubscribeFromSlot(kvp.Key.inputSlot, kvp.Key.phase, kvp.Value);
        _bindingHandlers.Clear();
    }

    private void SubscribeToSlot(InputActionSlot slot, InputPhase phase, System.Action handler)
        => ApplySlotSubscription(slot, phase, handler, add: true);

    private void UnsubscribeFromSlot(InputActionSlot slot, InputPhase phase, System.Action handler)
        => ApplySlotSubscription(slot, phase, handler, add: false);

    private void ApplySlotSubscription(InputActionSlot slot, InputPhase phase, System.Action handler, bool add)
    {
        var reader = PlayerInputReader.Instance;
        switch (slot)
        {
            case InputActionSlot.Ability1:
                switch (phase)
                {
                    case InputPhase.Started: if (add) reader.OnAbility1Started += handler; else reader.OnAbility1Started -= handler; break;
                    case InputPhase.Performed: if (add) reader.OnAbility1Performed += handler; else reader.OnAbility1Performed -= handler; break;
                    case InputPhase.Canceled: if (add) reader.OnAbility1Canceled += handler; else reader.OnAbility1Canceled -= handler; break;
                    case InputPhase.Triggered: if (add) reader.OnAbility1 += handler; else reader.OnAbility1 -= handler; break;
                }
                break;
            case InputActionSlot.Ability2:
                switch (phase)
                {
                    case InputPhase.Started: if (add) reader.OnAbility2Started += handler; else reader.OnAbility2Started -= handler; break;
                    case InputPhase.Performed: if (add) reader.OnAbility2Performed += handler; else reader.OnAbility2Performed -= handler; break;
                    case InputPhase.Canceled: if (add) reader.OnAbility2Canceled += handler; else reader.OnAbility2Canceled -= handler; break;
                    case InputPhase.Triggered: if (add) reader.OnAbility2 += handler; else reader.OnAbility2 -= handler; break;
                }
                break;
            case InputActionSlot.Input_Space:
                switch (phase)
                {
                    case InputPhase.Started: if (add) reader.OnInput_SpaceStarted += handler; else reader.OnInput_SpaceStarted -= handler; break;
                    case InputPhase.Canceled: if (add) reader.OnInput_SpaceCanceled += handler; else reader.OnInput_SpaceCanceled -= handler; break;
                    case InputPhase.Triggered: if (add) reader.OnInput_Space += handler; else reader.OnInput_Space -= handler; break;
                    default: if (add) Debug.LogWarning($"AbilitySystem: Invalid phase [{phase}] for Input_Space."); break;
                }
                break;
            case InputActionSlot.Interact:
                if (phase == InputPhase.Triggered) { if (add) reader.OnInteract += handler; else reader.OnInteract -= handler; }
                else if (add) Debug.LogWarning($"AbilitySystem: Invalid phase [{phase}] for Interact, only Triggered is supported.");
                break;
            case InputActionSlot.AnimalWheel:
                switch (phase)
                {
                    case InputPhase.Started: if (add) reader.OnAnimalWheelStarted += handler; else reader.OnAnimalWheelStarted -= handler; break;
                    case InputPhase.Canceled: if (add) reader.OnAnimalWheelCanceled += handler; else reader.OnAnimalWheelCanceled -= handler; break;
                    case InputPhase.Triggered: if (add) reader.OnAnimalWheel += handler; else reader.OnAnimalWheel -= handler; break;
                    default: if (add) Debug.LogWarning($"AbilitySystem: Invalid phase [{phase}] for AnimalWheel."); break;
                }
                break;
        }
    }

    protected void PlaySfx(AudioClip clip, float volume = 1f)
    {
        AudioManager.Instance?.PlaySfx(clip, volume);
    }

#if UNITY_EDITOR
    protected void DrawWallCheckGizmos(Collider2D col, float checkDistance, float checkInset, int rayCount)
    {
        Bounds bounds = col.bounds;
        float startY = bounds.min.y + 0.1f;
        float endY   = bounds.max.y - 0.1f;
        float step   = rayCount > 1 ? (endY - startY) / (rayCount - 1) : 0f;
        float dist   = checkDistance + checkInset;

        for (int i = 0; i < rayCount; i++)
        {
            float y = startY + step * i;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(new Vector3(bounds.max.x - checkInset, y, 0f), Vector3.right * dist);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(new Vector3(bounds.min.x + checkInset, y, 0f), Vector3.left  * dist);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(GetGroundCheckOrigin(), GetGroundCheckSize());
    }
#endif
}
