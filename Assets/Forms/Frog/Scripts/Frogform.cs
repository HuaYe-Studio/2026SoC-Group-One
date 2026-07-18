using UnityEngine;
using UnityEngine.InputSystem;   // 必须添加！

public class FrogForm : BaseForm
{
    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("Air Control")]
    [SerializeField] private float airControlSpeed = 4f;

    private bool _isBigJump;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        gravityScale = 0.7f;
        fallGravityMultiplier = 1.2f;
    }

    private void OnEnable()
    {
        if (PlayerInputReader.Instance != null)
        {
            PlayerInputReader.Instance.Ability1Action.started += OnAbilityPressed;
            PlayerInputReader.Instance.Ability1Action.canceled += OnAbilityReleased;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.Instance != null)
        {
            PlayerInputReader.Instance.Ability1Action.started -= OnAbilityPressed;
            PlayerInputReader.Instance.Ability1Action.canceled -= OnAbilityReleased;
        }
    }

    private void OnAbilityPressed(InputAction.CallbackContext ctx)
    {
        if (CanJump()) DoJump();
    }

    private void OnAbilityReleased(InputAction.CallbackContext ctx)
    {
        ApplyJumpCut();
    }

    // 删除原有的 HandleInput 方法，因为已由事件替代
    // protected override void HandleInput() { }

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
        currentState == ActionState.Moving;

    private void DoJump()
    {
        if (!CanJump()) return;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        currentState = ActionState.Jumping;
        ignoreGroundUntil = Time.fixedTime + 0.1f;
        IsGrounded = false;
        _isBigJump = true;
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
            currentState = ActionState.Idle;
            _isBigJump = false;
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
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