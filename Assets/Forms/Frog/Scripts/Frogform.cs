using UnityEngine;

public class FrogForm : BaseForm
{
    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Air Control")]
    [SerializeField] private float airControlSpeed = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;

    private bool _isBigJump;
    private float _coyoteTimer;
    private float _jumpBufferTimer;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        gravityScale = 0.7f;
        fallGravityMultiplier = 1.2f;
    }

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAbility1Started += OnAbilityPressed;
            PlayerInputReader.Instance.OnAbility1Canceled += OnAbilityReleased;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAbility1Started -= OnAbilityPressed;
            PlayerInputReader.Instance.OnAbility1Canceled -= OnAbilityReleased;
        }
    }

    private void OnAbilityPressed()
    {
        if (CanJump())
            DoJump();
        else
            _jumpBufferTimer = jumpBufferTime;
    }

    private void OnAbilityReleased()
    {
        ApplyJumpCut();
    }

    public override void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        if (_isBigJump && !IsGrounded)
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
        _coyoteTimer > 0f;

    private void DoJump()
    {
        if (!CanJump()) return;

        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + 0.1f;
        IsGrounded = false;
        _isBigJump = true;

        PlaySFX(jumpClip);
    }

    private void ApplyJumpCut()
    {
        if (rb.velocity.y > 0)
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
    }

    protected override void HandleLanding()
    {
        if (IsGrounded && (currentState == ActionState.Falling || currentState == ActionState.Jumping))
        {
            _isBigJump = false;

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer = 0f;
                DoJump();
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

        if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.fixedDeltaTime;

        SyncAnimator();
    }

    private void SyncAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        bool isMoving = currentState == ActionState.Moving;
        bool isAir = currentState == ActionState.Jumping || currentState == ActionState.Falling;

        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsAir", isAir);
        animator.SetFloat("Velocity_Y", isAir ? Mathf.Clamp(rb.velocity.y, -1f, 1f) : 0f);
    }
}