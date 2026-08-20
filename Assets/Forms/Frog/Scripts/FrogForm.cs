using UnityEngine;
using UnityEngine.InputSystem;

public class FrogForm : BaseForm
{
    [Header("Jump - Normal")]
    [SerializeField] private float normalJumpForce = 7f;

    [Header("Jump - Charged")]
    [SerializeField] private float maxChargedJumpForce = 14f;
    [SerializeField] private float jumpStaminaCost = 20f;
    [SerializeField] private float chargeTimeToMax = 0.5f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Wall Jump")]
    [SerializeField] private LayerMask wallLayer = -1;
    [SerializeField] private float wallCheckDistance = 0.4f;
    [SerializeField] private float wallCheckInset = 0.05f;
    [SerializeField] private int wallRayCount = 3;
    [SerializeField] private float wallJumpHorizontalForce = 10f;
    [SerializeField] private float wallJumpVerticalForce = 12f;
    [SerializeField] private float wallJumpCoyoteTime = 0.15f;
    [SerializeField] private float wallJumpInputLockTime = 0.15f;

    [Header("Jump Correct")]
    [SerializeField] private float ceilingCheckDistance = 0.1f;
    [SerializeField] private float ceilingCheckWidth = 0.8f;
    [SerializeField] private float ceilingCornerEjectSpeed = 3f;
    [SerializeField] private float ceilingEjectDuration = 0.12f;
    [SerializeField] private float ceilingEjectRiseFloor = 2f;

    [Header("Air Control")]
    [SerializeField] private float airControlSpeed = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;

    private static readonly int IsWallJumpHash = Animator.StringToHash("IsWallJump");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsAirHash = Animator.StringToHash("IsAir");
    private static readonly int VelocityYHash = Animator.StringToHash("Velocity_Y");
    private static readonly int ChargeProgressHash = Animator.StringToHash("ChargeProgress");

    private const float GroundIgnoreAfterJump = 0.1f;

    private bool _chargeModeEnabled;
    private float _chargeStartTime = -1f;
    private float _chargeProgress;
    private float _coyoteTimer;
    private float _jumpBufferTimer;

    public bool IsChargeMode => _chargeModeEnabled;
    public float ChargeProgress => _chargeProgress;

    private int _lastWallJumpSide;
    private int _wallContactSide;
    private float _wallMemoryTimer;
    private float _wallJumpInputLockTimer;
    private bool _pendingWallJumpAnim;
    private bool _isWallJump;

    private int _ceilingEjectDir;
    private float _ceilingEjectTimer;
    private float _ceilingEjectRiseVelocity;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        gravityScale = 0.8f;
        fallGravityMultiplier = 2.5f;
        _chargeModeEnabled = false;
    }

    public override void Die()
    {
        _chargeStartTime = -1f;
        _ceilingEjectTimer = 0f;
        base.Die();
    }

    private InputAction _toggleChargeAction;

    private void OnEnable()
    {
        _toggleChargeAction = new InputAction(binding: "<Keyboard>/j");
        _toggleChargeAction.performed += OnToggleCharge;
        _toggleChargeAction.Enable();
    }

    private void OnDisable()
    {
        _toggleChargeAction?.Disable();
        _toggleChargeAction?.Dispose();
    }

    private void OnToggleCharge(InputAction.CallbackContext ctx)
    {
        _chargeModeEnabled = !_chargeModeEnabled;
        _chargeProgress = 0f;
    }

    private void Update()
    {
        if (_chargeModeEnabled && _chargeStartTime >= 0f)
            _chargeProgress = ComputeChargeProgress();
        else if (_chargeStartTime < 0f)
            _chargeProgress = 0f;
    }

    public void OnJumpPressed()
    {
        if (TryWallJump())
            return;

        if (!_chargeModeEnabled)
        {
            if (CanJump())
                DoNormalJump();
            else
                _jumpBufferTimer = jumpBufferTime;
            return;
        }

        if (CanJump())
        {
            _chargeStartTime = Time.time;
            currentState = ActionState.SpecialAction;
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        else
            _jumpBufferTimer = jumpBufferTime;
    }

    public void OnJumpReleased()
    {
        if (!_chargeModeEnabled)
        {
            if (currentState == ActionState.Jumping && rb.velocity.y > 0 && !_isWallJump)
                ApplyJumpCut();
            return;
        }

        if (_chargeStartTime < 0f)
        {
            if (currentState == ActionState.Jumping && rb.velocity.y > 0 && !_isWallJump)
                ApplyJumpCut();
            return;
        }

        float progress = ComputeChargeProgress();

        if (!CanJump())
        {
            _chargeStartTime = -1f;
            currentState = ActionState.Idle;
            return;
        }

        _chargeStartTime = -1f;
        DoProgressiveJump(progress);
    }

    private void DoProgressiveJump(float progress)
    {
        float force = Mathf.Lerp(normalJumpForce, maxChargedJumpForce, progress);
        float cost = jumpStaminaCost * progress;

        if (stamina != null && cost > 0.5f)
        {
            if (!stamina.Spend(cost))
            {
                float availableRatio = stamina.Current / cost;
                force = Mathf.Lerp(normalJumpForce, force, availableRatio);
                stamina.Spend(stamina.Current);
            }
        }

        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _ceilingEjectTimer = 0f;
        _isWallJump = false;

        rb.velocity = new Vector2(rb.velocity.x, force);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + GroundIgnoreAfterJump;
        IsGrounded = false;

        PlaySfx(jumpClip);
    }

    protected override void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        if (currentState == ActionState.Jumping && !IsGrounded)
        {
            if (_wallJumpInputLockTimer <= 0f)
                rb.velocity = new Vector2(horizontal * airControlSpeed, rb.velocity.y);
            return;
        }

        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            UpdateIdleOrMovingState(horizontal);
    }

    private bool CanJump() =>
        currentState == ActionState.Idle ||
        currentState == ActionState.Moving ||
        _chargeStartTime >= 0f ||
        _coyoteTimer > 0f;

    private float ComputeChargeProgress()
    {
        return Mathf.Clamp01((Time.time - _chargeStartTime) / chargeTimeToMax);
    }

    private void DoNormalJump()
    {
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _ceilingEjectTimer = 0f;
        _isWallJump = false;

        rb.velocity = new Vector2(rb.velocity.x, normalJumpForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + GroundIgnoreAfterJump;
        IsGrounded = false;

        PlaySfx(jumpClip);
    }

    private void ApplyJumpCut()
    {
        if (rb.velocity.y > 0)
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
    }

    protected override void HandleLanding()
    {
        bool landingThisFrame = IsGrounded &&
            (currentState == ActionState.Falling || currentState == ActionState.Jumping || currentState == ActionState.WallCling);

        if (landingThisFrame && _jumpBufferTimer > 0f)
        {
            _jumpBufferTimer = 0f;
            DoNormalJump();
            return;
        }

        base.HandleLanding();

        if (landingThisFrame)
        {
            _ceilingEjectTimer = 0f;
            PlaySfx(landClip);
        }
    }

    protected override void UpdateAirState()
    {
        bool wasIdleOrMoving = currentState == ActionState.Idle || currentState == ActionState.Moving;

        base.UpdateAirState();

        if (wasIdleOrMoving && currentState == ActionState.Falling)
            _coyoteTimer = coyoteTime;
    }

    protected override void FixedUpdate()
    {
        if (currentState == ActionState.Dead) return;

        base.FixedUpdate();

        // Ceiling-corner correction: a sustained window re-applies the eject velocity each fixed
        // step so the frog wins over the physics solver's head-bonk response and keeps rising.
        if (_ceilingEjectTimer > 0f)
        {
            _ceilingEjectTimer -= Time.fixedDeltaTime;
            rb.velocity = new Vector2(_ceilingEjectDir * ceilingCornerEjectSpeed, _ceilingEjectRiseVelocity);
            currentState = ActionState.Jumping;
        }
        else if (currentState == ActionState.Jumping && rb.velocity.y > 0f)
        {
            var (ceilingLeft, ceilingRight) = DetectCeiling(ceilingCheckDistance, ceilingCheckWidth, wallLayer);

            if (ceilingLeft != ceilingRight)
            {
                _ceilingEjectDir = ceilingLeft ? 1 : -1;
                _ceilingEjectRiseVelocity = Mathf.Max(rb.velocity.y, ceilingEjectRiseFloor);
                _ceilingEjectTimer = ceilingEjectDuration;
                rb.velocity = new Vector2(_ceilingEjectDir * ceilingCornerEjectSpeed, _ceilingEjectRiseVelocity);
            }
            else if (ceilingLeft && ceilingRight)
            {
                rb.velocity = new Vector2(rb.velocity.x, 0f);
                currentState = ActionState.Falling;
            }
        }

        var (wallLeft, wallRight) = DetectWalls(wallCheckDistance, wallCheckInset, wallRayCount, wallLayer);

        if (wallRight) _wallContactSide = 1;
        else if (wallLeft) _wallContactSide = -1;
        else _wallContactSide = 0;

        if (IsGrounded)
        {
            _lastWallJumpSide = 0;
            _wallMemoryTimer = 0f;
        }

        if (_wallContactSide != 0)
            _wallMemoryTimer = wallJumpCoyoteTime;
        else if (_wallMemoryTimer > 0f)
            _wallMemoryTimer -= Time.fixedDeltaTime;

        // Consume the buffered jump as a wall jump as soon as the wall becomes available.
        if (_jumpBufferTimer > 0f)
            TryWallJump();

        if (_chargeStartTime >= 0f && !CanJump())
        {
            _chargeStartTime = -1f;
            currentState = ActionState.Idle;
        }

        if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.fixedDeltaTime;
        if (_wallJumpInputLockTimer > 0f) _wallJumpInputLockTimer -= Time.fixedDeltaTime;

        SyncAnimator();
    }

    private bool TryWallJump()
    {
        if (IsGrounded) return false;
        if (_wallContactSide == 0 && _wallMemoryTimer <= 0f) return false;
        if (currentState == ActionState.SpecialAction) return false;

        int side = _wallContactSide != 0 ? _wallContactSide : -_lastWallJumpSide;
        if (side == 0 || side == _lastWallJumpSide) return false;

        // Require horizontal input away from the wall so the jump always separates from it.
        if (PlayerInputReader.HasInstance)
        {
            float awayDir = side > 0 ? -1f : 1f;
            if (PlayerInputReader.Instance.MoveValue.x * awayDir <= 0f)
                return false;
        }

        DoWallJump(side);
        return true;
    }

    private void DoWallJump(int wallSide)
    {
        _chargeStartTime = -1f;
        _lastWallJumpSide = wallSide;
        _wallMemoryTimer = 0f;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _wallJumpInputLockTimer = wallJumpInputLockTime;
        _ceilingEjectTimer = 0f;
        _isWallJump = true;
        _pendingWallJumpAnim = true;

        float awayDir = wallSide > 0 ? -1f : 1f;
        rb.velocity = new Vector2(awayDir * wallJumpHorizontalForce, wallJumpVerticalForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + GroundIgnoreAfterJump;
        IsGrounded = false;

        PlaySfx(jumpClip);
    }

    private void SyncAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        if (_pendingWallJumpAnim)
        {
            animator.SetTrigger(IsWallJumpHash);
            _pendingWallJumpAnim = false;
        }

        bool isCharging = _chargeStartTime >= 0f;
        bool isMoving = currentState == ActionState.Moving;
        bool isAir = currentState == ActionState.Jumping || currentState == ActionState.Falling;

        animator.SetBool(IsChargingHash, isCharging);
        animator.SetBool(IsMovingHash, !isCharging && isMoving);
        animator.SetBool(IsAirHash, !isCharging && isAir);
        animator.SetFloat(VelocityYHash, (isAir && !isCharging) ? Mathf.Clamp(rb.velocity.y, -1f, 1f) : 0f);
        animator.SetFloat(ChargeProgressHash, _chargeProgress);
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (GetComponent<Collider2D>() is { } col)
        {
            DrawWallCheckGizmos(col, wallCheckDistance, wallCheckInset, wallRayCount);
            DrawCeilingCheckGizmos(col, ceilingCheckDistance, ceilingCheckWidth);
        }
    }
#endif
}