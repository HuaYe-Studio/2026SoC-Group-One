using UnityEngine;

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
    private bool _pendingWallJumpAnim;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        gravityScale = 0.8f;
        fallGravityMultiplier = 2.5f;
        _chargeModeEnabled = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            _chargeModeEnabled = !_chargeModeEnabled;
            _chargeProgress = 0f;
            Debug.Log($"Frog jump mode: {(_chargeModeEnabled ? "Charged" : "Normal")}");
        }

        if (_chargeModeEnabled && _chargeStartTime >= 0f)
            _chargeProgress = Mathf.Clamp01((Time.time - _chargeStartTime) / chargeTimeToMax);
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
            if (currentState == ActionState.Jumping && rb.velocity.y > 0)
                ApplyJumpCut();
            return;
        }

        if (_chargeStartTime < 0f)
        {
            if (currentState == ActionState.Jumping && rb.velocity.y > 0)
                ApplyJumpCut();
            return;
        }

        float progress = Mathf.Clamp01((Time.time - _chargeStartTime) / chargeTimeToMax);

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

        rb.velocity = new Vector2(rb.velocity.x, force);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + 0.1f;
        IsGrounded = false;

        PlaySFX(jumpClip);
    }

    public override void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        if (currentState == ActionState.Jumping && !IsGrounded)
        {
            rb.velocity = new Vector2(horizontal * airControlSpeed, rb.velocity.y);
            return;
        }

        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            currentState = Mathf.Abs(horizontal) > 0.1f ? ActionState.Moving : ActionState.Idle;
    }

    private bool CanJump() =>
        currentState == ActionState.Idle ||
        currentState == ActionState.Moving ||
        _chargeStartTime >= 0f ||
        _coyoteTimer > 0f;

    private void DoNormalJump()
    {
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;

        rb.velocity = new Vector2(rb.velocity.x, normalJumpForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + 0.1f;
        IsGrounded = false;

        PlaySFX(jumpClip);
    }

    private void ApplyJumpCut()
    {
        if (rb.velocity.y > 0)
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
    }

    protected override void HandleLanding()
    {
        if (IsGrounded && (currentState == ActionState.Falling || currentState == ActionState.Jumping || currentState == ActionState.WallCling))
        {
            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer = 0f;
                DoNormalJump();
                return;
            }

            currentState = Mathf.Abs(rb.velocity.x) > 0.1f ? ActionState.Moving : ActionState.Idle;

            PlaySFX(landClip);
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
        base.FixedUpdate();

        var (wallLeft, wallRight) = DetectWalls();

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

        if (_chargeStartTime >= 0f && !CanJump())
        {
            _chargeStartTime = -1f;
            currentState = ActionState.Idle;
        }

        if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.fixedDeltaTime;

        SyncAnimator();
    }

    private (bool left, bool right) DetectWalls()
    {
        if (myCollider == null) return (false, false);
        Bounds bounds = myCollider.bounds;
        float startY = bounds.min.y + 0.1f;
        float endY = bounds.max.y - 0.1f;
        float step = wallRayCount > 1 ? (endY - startY) / (wallRayCount - 1) : 0f;
        float dist = wallCheckDistance + wallCheckInset;

        bool hitL = false, hitR = false;
        for (int i = 0; i < wallRayCount; i++)
        {
            float y = startY + step * i;

            Vector2 rightOrigin = new Vector2(bounds.max.x - wallCheckInset, y);
            bool hitRight = Physics2D.Raycast(rightOrigin, Vector2.right, dist, wallLayer);
            if (hitRight) hitR = true;

            Vector2 leftOrigin = new Vector2(bounds.min.x + wallCheckInset, y);
            bool hitLeft = Physics2D.Raycast(leftOrigin, Vector2.left, dist, wallLayer);
            if (hitLeft) hitL = true;

            Debug.DrawRay(rightOrigin, Vector2.right * dist, hitRight ? Color.green : Color.red);
            Debug.DrawRay(leftOrigin, Vector2.left * dist, hitLeft ? Color.green : Color.blue);
        }
        return (hitL, hitR);
    }

    private bool TryWallJump()
    {
        if (IsGrounded) return false;
        if (_wallContactSide == 0 && _wallMemoryTimer <= 0f) return false;
        if (currentState == ActionState.SpecialAction || currentState == ActionState.Locked) return false;

        int side = _wallContactSide != 0 ? _wallContactSide : -_lastWallJumpSide;
        if (side == 0 || side == _lastWallJumpSide) return false;

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
        _pendingWallJumpAnim = true;

        float awayDir = wallSide > 0 ? -1f : 1f;
        rb.velocity = new Vector2(awayDir * wallJumpHorizontalForce, wallJumpVerticalForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + 0.1f;
        IsGrounded = false;

        PlaySFX(jumpClip);
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
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Bounds bounds = col.bounds;
        float startY = bounds.min.y + 0.1f;
        float endY = bounds.max.y - 0.1f;
        float step = wallRayCount > 1 ? (endY - startY) / (wallRayCount - 1) : 0f;
        float dist = wallCheckDistance + wallCheckInset;

        for (int i = 0; i < wallRayCount; i++)
        {
            float y = startY + step * i;

            Vector3 rightOrigin = new Vector3(bounds.max.x - wallCheckInset, y, 0f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(rightOrigin, Vector3.right * dist);

            Vector3 leftOrigin = new Vector3(bounds.min.x + wallCheckInset, y, 0f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(leftOrigin, Vector3.left * dist);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif
}