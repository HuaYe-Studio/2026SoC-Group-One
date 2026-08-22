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
    SpecialAction,
    Dead
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

    // Public accessors for holdable modifier system
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
    public float GravityScale { get => gravityScale; set => gravityScale = value; }
    public float FallGravityMultiplier { get => fallGravityMultiplier; set => fallGravityMultiplier = value; }

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
    protected PlayerHP hp;

    [Header("Ability Input")]
    [SerializeField] private List<AbilityInputBinding> abilityBindings = new();
    private readonly Dictionary<AbilityInputBinding, System.Action> _bindingHandlers = new();

    // Holdable modifier original values for clean revert
    private float _originalMoveSpeed;
    private float _originalGravityScale;
    private float _originalFallGravityMultiplier;
    private float _originalMass;
    private Color _originalTint;
    private GameObject _overlaySpriteObject;
    private AbilityInputBinding _grantedAbilityInstance;

    private static readonly int DeathHash = Animator.StringToHash("Death");

    protected ActionState currentState = ActionState.Idle;
    protected float ignoreGroundUntil;

    protected bool IsGrounded { get; set; }
    public ActionState CurrentState => currentState;
    public Animator Animator => animator;
    public Vector2 FacingDirection => spriteRenderer != null && spriteRenderer.flipX
        ? Vector2.left : Vector2.right;

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
        hp = ctrl.GetComponent<PlayerHP>();
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

    public void SetAnimatorBool(int hash, bool value)
    {
        if (animator != null)
            animator.SetBool(hash, value);
    }

    public void ProcessInput(float horizontal)
    {
        if (currentState == ActionState.SpecialAction || currentState == ActionState.Dead) return;

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
        if (currentState == ActionState.Dead) return;

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

    // Param-driven like DetectWalls so concrete values live on each form's serialized fields.
    // Probes the head in two zones (left/right), each flush with bounds.max.y, so a zone fires
    // iff it is within checkDistance of an overhead surface on `layer`. Exactly one zone firing
    // means the head clips a platform edge (caller ejects away from it); both firing is a ceiling.
    private const float CeilingProbeHeight = 0.05f;

    protected (bool left, bool right) DetectCeiling(
        float checkDistance, float checkWidthFraction, LayerMask layer)
    {
        if (myCollider == null) return (false, false);

        Bounds bounds = myCollider.bounds;
        float headWidth = bounds.size.x * checkWidthFraction;
        float zoneOffset = headWidth * 0.25f;
        Vector2 size = new Vector2(headWidth / 2f, CeilingProbeHeight);
        float topY = bounds.max.y - CeilingProbeHeight * 0.5f;
        float centerX = bounds.center.x;

        Vector2 leftOrigin = new Vector2(centerX - zoneOffset, topY);
        Vector2 rightOrigin = new Vector2(centerX + zoneOffset, topY);

        RaycastHit2D leftHit = Physics2D.BoxCast(leftOrigin, size, 0f, Vector2.up, checkDistance, layer);
        RaycastHit2D rightHit = Physics2D.BoxCast(rightOrigin, size, 0f, Vector2.up, checkDistance, layer);

        return (leftHit.collider != null, rightHit.collider != null);
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

    public virtual void Die()
    {
        currentState = ActionState.Dead;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.gravityScale = 0f;
        }
        if (animator != null)
            animator.SetBool(DeathHash, true);
    }

    public virtual void Revive()
    {
        currentState = ActionState.Idle;
        // All forms share the root Rigidbody2D; only the active form may reset it.
        if (controller != null && controller.ActiveForm == this)
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.gravityScale = gravityScale;
            }
        }
        if (animator != null)
            animator.SetBool(DeathHash, false);
    }

    private void RegisterAbilityBindings()
    {
        if (!PlayerInputReader.HasInstance) return;

        if (_bindingHandlers.Count > 0)
            UnregisterAbilityBindings();

        foreach (var binding in abilityBindings)
        {
            // Empty binding (no UnityEvent callbacks) grants nothing — skip it.
            if (binding.onAbilityActivated == null || binding.onAbilityActivated.GetPersistentEventCount() == 0) continue;
            System.Action handler = () => binding.onAbilityActivated.Invoke();
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
            case InputActionSlot.SpitFire:
                switch (phase)
                {
                    case InputPhase.Started:
                    case InputPhase.Triggered: if (add) reader.OnSpitFire += handler; else reader.OnSpitFire -= handler; break;
                    case InputPhase.Canceled: if (add) reader.OnSpitFireCanceled += handler; else reader.OnSpitFireCanceled -= handler; break;
                    default: if (add) Debug.LogWarning($"AbilitySystem: Invalid phase [{phase}] for SpitFire."); break;
                }
                break;
        }
    }

    public virtual void ApplyHoldableModifier(HoldableModifier m)
    {
        if (!m.HasAnyEffect) return;

        _originalMoveSpeed = moveSpeed;
        _originalGravityScale = gravityScale;
        _originalFallGravityMultiplier = fallGravityMultiplier;
        if (rb != null) _originalMass = rb.mass;

        if (!Mathf.Approximately(m.moveSpeedMultiplier, 1f))
            moveSpeed = _originalMoveSpeed * m.moveSpeedMultiplier;
        if (!Mathf.Approximately(m.gravityScaleMultiplier, 1f))
            gravityScale = _originalGravityScale * m.gravityScaleMultiplier;
        if (!Mathf.Approximately(m.fallGravityMultiplier, 1f))
            fallGravityMultiplier = _originalFallGravityMultiplier * m.fallGravityMultiplier;
        if (!Mathf.Approximately(m.massMultiplier, 1f) && rb != null)
            rb.mass = _originalMass * m.massMultiplier;

        if (spriteRenderer != null && m.tintColor.a > 0f)
        {
            _originalTint = spriteRenderer.color;
            spriteRenderer.color = m.tintColor;
        }

        if (spriteRenderer != null && m.overlaySprite != null)
        {
            _overlaySpriteObject = new GameObject("HoldableOverlay");
            _overlaySpriteObject.transform.SetParent(spriteRenderer.transform, false);
            _overlaySpriteObject.transform.localPosition = Vector3.zero;
            _overlaySpriteObject.transform.localScale = Vector3.one;
            var overlaySr = _overlaySpriteObject.AddComponent<SpriteRenderer>();
            overlaySr.sprite = m.overlaySprite;
            overlaySr.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        if (m.grantedAbility != null)
        {
            _grantedAbilityInstance = m.grantedAbility;
            AddAbilityBinding(_grantedAbilityInstance);
        }
    }

    public virtual void RemoveHoldableModifier(HoldableModifier m)
    {
        moveSpeed = _originalMoveSpeed;
        gravityScale = _originalGravityScale;
        fallGravityMultiplier = _originalFallGravityMultiplier;
        if (rb != null) rb.mass = _originalMass;

        // Only restore the tint if one was actually applied during equip.
        // Otherwise _originalTint is never set (stays Color(0,0,0,0)) and would
        // make the form's sprite fully transparent on unequip.
        if (spriteRenderer != null && m.tintColor.a > 0f)
            spriteRenderer.color = _originalTint;

        if (_overlaySpriteObject != null)
        {
            if (Application.isPlaying)
                Destroy(_overlaySpriteObject);
            else
                DestroyImmediate(_overlaySpriteObject);
            _overlaySpriteObject = null;
        }

        if (_grantedAbilityInstance != null)
        {
            RemoveAbilityBinding(_grantedAbilityInstance);
            _grantedAbilityInstance = null;
        }
    }

    public void AddAbilityBinding(AbilityInputBinding binding)
    {
        if (binding == null || abilityBindings.Contains(binding)) return;
        // Empty binding (no UnityEvent callbacks) grants nothing — skip it so
        // SubscribeToSlot never warns about unsupported slots for dead bindings.
        if (binding.onAbilityActivated == null || binding.onAbilityActivated.GetPersistentEventCount() == 0) return;
        abilityBindings.Add(binding);
        // Only subscribe immediately while this form's GameObject is active (the current form).
        // Inactive forms keep the binding in the list and get subscribed by
        // RegisterAbilityBindings on OnFormActivated, avoiding triggering an inactive
        // form's ability (e.g. SpitFire armed while restoring elements during a load).
        if (PlayerInputReader.HasInstance && gameObject.activeInHierarchy)
        {
            System.Action handler = () => binding.onAbilityActivated.Invoke();
            _bindingHandlers[binding] = handler;
            SubscribeToSlot(binding.inputSlot, binding.phase, handler);
        }
    }

    private void RemoveAbilityBinding(AbilityInputBinding binding)
    {
        if (binding == null) return;
        abilityBindings.Remove(binding);
        if (_bindingHandlers.TryGetValue(binding, out var handler))
        {
            if (PlayerInputReader.HasInstance)
                UnsubscribeFromSlot(binding.inputSlot, binding.phase, handler);
            _bindingHandlers.Remove(binding);
        }
    }

    protected void PlaySfx(AudioClip clip, float volume = 1f)
    {
        AudioManager.Instance?.PlaySfx(clip, volume);
    }

    /// <summary>按 key 播放音效（key 对应 AudioLibrary 的 sfxEntries）。</summary>
    protected void PlaySfxByKey(string key, float volume = 1f)
    {
        AudioManager.Instance?.PlaySfxByKey(key, volume);
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

    // Mirrors DetectCeiling's two probe zones (swept volume) so the editor visual can't drift
    // from the runtime probe whenever the shared geometry constants change.
    protected void DrawCeilingCheckGizmos(Collider2D col, float checkDistance, float checkWidthFraction)
    {
        Bounds bounds = col.bounds;
        float headWidth = bounds.size.x * checkWidthFraction;
        float zoneOffset = headWidth * 0.25f;
        float centerY = bounds.max.y - CeilingProbeHeight * 0.5f + checkDistance * 0.5f;
        Vector2 zoneSize = new Vector2(headWidth / 2f, CeilingProbeHeight + checkDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(new Vector2(bounds.center.x - zoneOffset, centerY), zoneSize);
        Gizmos.DrawWireCube(new Vector2(bounds.center.x + zoneOffset, centerY), zoneSize);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(GetGroundCheckOrigin(), GetGroundCheckSize());
    }
#endif
}
