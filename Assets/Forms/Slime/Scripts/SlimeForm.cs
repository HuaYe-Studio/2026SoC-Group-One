using UnityEngine;

public class SlimeForm : BaseForm
{
    [Header("Slime Settings")]
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float deceleration = 0.5f;

    [Header("Devour")]
    [SerializeField] private DevourHandler devourHandler;

    [Header("Wall Climb")]
    [SerializeField] private PhysicsMaterial2D slimePhysicsMaterial;
    [SerializeField] private LayerMask wallLayer = -1;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float wallCheckInset = 0.05f;
    [SerializeField] private int wallRayCount = 3;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float slideDownSpeed = 2f;
    [SerializeField] private float climbStaminaPerSec = 25f;
    [SerializeField] private float clingStaminaPerSec = 8f;
    [SerializeField] private float wallStickForce = 5f;
    [SerializeField] private float wallClingCooldown = 0.15f;
    [SerializeField] private float wallExhaustedGraceTime = 1.5f;

    // Public accessors for holdable modifier system
    public float ClimbSpeed { get => climbSpeed; set => climbSpeed = value; }
    public float SlideDownSpeed { get => slideDownSpeed; set => slideDownSpeed = value; }
    public float ClimbStaminaPerSec { get => climbStaminaPerSec; set => climbStaminaPerSec = value; }
    public float ClingStaminaPerSec { get => clingStaminaPerSec; set => clingStaminaPerSec = value; }

    [Header("Audio")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] private float walkSoundInterval = 0.4f;

    private float _currentVelocityX;
    private float _nextWalkSoundTime;

    private int _wallClingDirection;
    private float _wallClingExitTime;
    private float _staminaExhaustedTime = -1f;
    private bool _exhaustedFall;

    // Holdable modifier original values for clean revert
    private float _originalClimbSpeed;
    private float _originalSlideDownSpeed;
    private float _originalClimbStaminaPerSec;
    private float _originalClingStaminaPerSec;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsWallClingHash = Animator.StringToHash("IsWallCling");
    private static readonly int SpitFireHash = Animator.StringToHash("SpitFire");

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 4f;
        gravityScale = 1f;
        fallGravityMultiplier = 1.8f;

        if (devourHandler == null)
            devourHandler = GetComponent<DevourHandler>();

        if (slimePhysicsMaterial != null && myCollider != null)
            myCollider.sharedMaterial = slimePhysicsMaterial;
    }

    protected override void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        if (currentState == ActionState.WallCling)
        {
            DoWallMovement(horizontal);
            return;
        }

        float targetSpeed = horizontal * moveSpeed;
        _currentVelocityX = Mathf.MoveTowards(_currentVelocityX, targetSpeed,
            (Mathf.Abs(horizontal) > 0.1f ? acceleration : deceleration) * Time.fixedDeltaTime * 60f);

        rb.velocity = new Vector2(_currentVelocityX, rb.velocity.y);

        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            UpdateIdleOrMovingState(horizontal);

        if (currentState == ActionState.Moving && IsGrounded && Time.time >= _nextWalkSoundTime)
        {
            PlaySfx(walkClip);
            _nextWalkSoundTime = Time.time + walkSoundInterval;
        }
    }

    public override void OnFormActivated()
    {
        base.OnFormActivated();
        _currentVelocityX = rb != null ? rb.velocity.x : 0f;
    }

    public override void OnFormDeactivated()
    {
        base.OnFormDeactivated();
        if (currentState == ActionState.WallCling)
            ExitWallCling();
        if (devourHandler != null)
        {
            devourHandler.SpitOutHeldObject();
            if (!devourHandler.IsDevourInitiatedSwitchPending)
                devourHandler.CancelAll();
        }
    }

    public override void Die()
    {
        if (currentState == ActionState.WallCling)
            ExitWallCling();
        base.Die();
    }

    public void SpitFire()
    {
        if (animator != null)
            animator.SetTrigger(SpitFireHash);
        // TODO: implement fire projectile spawn / collision / damage later
        Debug.Log("SpitFire ability triggered");
    }

    protected override void FixedUpdate()
    {
        if (currentState == ActionState.Dead) return;

        var (wallLeft, wallRight) = DetectWalls(wallCheckDistance, wallCheckInset, wallRayCount, wallLayer);
        float horizontal = HorizontalInput;

        // Entry check moved BEFORE base.FixedUpdate to prevent gravity application in same frame
        if (currentState != ActionState.WallCling && currentState != ActionState.SpecialAction)
        {
            bool pushingRight = horizontal > 0.1f;
            bool pushingLeft = horizontal < -0.1f;
            bool canCling = !_exhaustedFall
                && (stamina == null || !stamina.IsEmpty)
                && Time.time >= _wallClingExitTime + wallClingCooldown
                && (devourHandler == null || !devourHandler.IsPouncing);

            if (canCling && wallRight && pushingRight)
                EnterWallCling(1);
            else if (canCling && wallLeft && pushingLeft)
                EnterWallCling(-1);
        }

        base.FixedUpdate();

        // Stamina drain for wall cling/climb (moved here from DoWallMovement for correct timing)
        if (currentState == ActionState.WallCling && stamina != null)
        {
            float vertical = VerticalInput;

            if (vertical > 0.1f)
                stamina.Spend(climbStaminaPerSec * Time.fixedDeltaTime);
            else if (vertical < -0.1f)
                stamina.Spend(clingStaminaPerSec * 0.3f * Time.fixedDeltaTime);
            else
                stamina.Spend(clingStaminaPerSec * Time.fixedDeltaTime);
        }

        // Clear exhausted-fall flag once player releases horizontal input
        if (_exhaustedFall && Mathf.Abs(horizontal) < 0.1f)
            _exhaustedFall = false;

        // Exhaustion timeout
        if (_staminaExhaustedTime >= 0f && Time.time >= _staminaExhaustedTime + wallExhaustedGraceTime)
        {
            ExitWallCling();
            _exhaustedFall = true;
        }

        // Exit check
        if (currentState == ActionState.WallCling)
        {
            bool wallOnSide = _wallClingDirection > 0 ? wallRight : wallLeft;
            if (!wallOnSide)
                ExitWallCling();
        }

        SyncAnimator();
    }

    private void DoWallMovement(float horizontal)
    {
        float vertical = VerticalInput;
        bool pushingAway = (_wallClingDirection > 0 && horizontal < -0.1f)
                        || (_wallClingDirection < 0 && horizontal > 0.1f);

        if (pushingAway)
        {
            ExitWallCling();
            return;
        }

        if (stamina != null && stamina.IsEmpty)
        {
            if (_staminaExhaustedTime < 0f)
                _staminaExhaustedTime = Time.time;
            rb.velocity = new Vector2(0f, 0f);
            return;
        }

        float wallDir = _wallClingDirection > 0 ? 1f : -1f;
        float stick = wallDir * wallStickForce;

        if (vertical > 0.1f)
            rb.velocity = new Vector2(stick, climbSpeed);
        else if (vertical < -0.1f)
            rb.velocity = new Vector2(stick, -slideDownSpeed);
        else
            rb.velocity = new Vector2(stick, 0f);
    }

    private void EnterWallCling(int direction)
    {
        _wallClingDirection = direction;
        _staminaExhaustedTime = -1f;
        _exhaustedFall = false;
        currentState = ActionState.WallCling;
        rb.velocity = new Vector2((direction > 0 ? 1f : -1f) * wallStickForce, 0f);
        rb.gravityScale = 0f;
    }

    private void ExitWallCling()
    {
        _staminaExhaustedTime = -1f;
        _wallClingExitTime = Time.time;
        currentState = ActionState.Falling;
        rb.gravityScale = gravityScale;
    }

    protected override void HandleLanding()
    {
        if (currentState == ActionState.WallCling)
            return;
        base.HandleLanding();
    }

    protected override void ApplyGravity()
    {
        if (currentState == ActionState.WallCling) return;
        base.ApplyGravity();
    }

    protected override void StaminaRecovery()
    {
        if (currentState == ActionState.WallCling) return;
        base.StaminaRecovery();
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || rb == null) return;

        if (currentState == ActionState.WallCling)
            spriteRenderer.flipX = _wallClingDirection < 0;
        else
            spriteRenderer.flipX = rb.velocity.x < 0f;
    }

    private void SyncAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.SetBool(IsWallClingHash, currentState == ActionState.WallCling);
        animator.SetFloat(SpeedHash, Mathf.Abs(rb.velocity.x));
    }

    public override void ApplyHoldableModifier(HoldableModifier m)
    {
        _originalClimbSpeed = climbSpeed;
        _originalSlideDownSpeed = slideDownSpeed;
        _originalClimbStaminaPerSec = climbStaminaPerSec;
        _originalClingStaminaPerSec = clingStaminaPerSec;

        base.ApplyHoldableModifier(m);

        if (!Mathf.Approximately(m.climbSpeedMultiplier, 1f))
            climbSpeed = _originalClimbSpeed * m.climbSpeedMultiplier;
        if (!Mathf.Approximately(m.slideDownSpeedMultiplier, 1f))
            slideDownSpeed = _originalSlideDownSpeed * m.slideDownSpeedMultiplier;
        if (!Mathf.Approximately(m.climbStaminaCostMultiplier, 1f))
            climbStaminaPerSec = _originalClimbStaminaPerSec * m.climbStaminaCostMultiplier;
        if (!Mathf.Approximately(m.clingStaminaCostMultiplier, 1f))
            clingStaminaPerSec = _originalClingStaminaPerSec * m.clingStaminaCostMultiplier;
    }

    public override void RemoveHoldableModifier(HoldableModifier m)
    {
        base.RemoveHoldableModifier(m);

        climbSpeed = _originalClimbSpeed;
        slideDownSpeed = _originalSlideDownSpeed;
        climbStaminaPerSec = _originalClimbStaminaPerSec;
        clingStaminaPerSec = _originalClingStaminaPerSec;
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (GetComponent<Collider2D>() is { } col)
            DrawWallCheckGizmos(col, wallCheckDistance, wallCheckInset, wallRayCount);
    }
#endif
}