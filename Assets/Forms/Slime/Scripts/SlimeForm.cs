using UnityEngine;

public class SlimeForm : BaseForm
{
    [Header("Slime Settings")]
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float deceleration = 0.5f;

    [Header("Devour")]
    [SerializeField] private SlimeDevourHandler devourHandler;

    [Header("Audio")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] private float walkSoundInterval = 0.4f;

    private float _currentVelocityX;
    private float _nextWalkSoundTime;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 4f;
        gravityScale = 1f;
        fallGravityMultiplier = 1.8f;

        if (devourHandler == null)
            devourHandler = GetComponent<SlimeDevourHandler>();
    }

    public override void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        float targetSpeed = horizontal * moveSpeed;
        _currentVelocityX = Mathf.MoveTowards(_currentVelocityX, targetSpeed,
            (Mathf.Abs(horizontal) > 0.1f ? acceleration : deceleration) * Time.fixedDeltaTime * 60f);

        rb.velocity = new Vector2(_currentVelocityX, rb.velocity.y);

        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            currentState = Mathf.Abs(horizontal) > 0.1f ? ActionState.Moving : ActionState.Idle;

        if (currentState == ActionState.Moving && IsGrounded && Time.time >= _nextWalkSoundTime)
        {
            PlaySFX(walkClip);
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
        if (devourHandler != null)
            devourHandler.CancelAll();
    }

    // 删除原来的 HandleInput 方法，因为吞噬输入已由事件驱动
    // protected override void HandleInput() { }

    private void LateUpdate()
    {
        if (spriteRenderer == null || rb == null) return;
        spriteRenderer.flipX = rb.velocity.x < 0f;

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(rb.velocity.x));
    }
}