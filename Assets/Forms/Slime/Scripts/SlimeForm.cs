using UnityEngine;

public class SlimeForm : BaseForm
{
    [Header("Slime Settings")]
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float deceleration = 0.5f;

    private float _currentVelocityX;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 4f;
        jumpForce = 9f;
        gravityScale = 1f;
        fallGravityMultiplier = 1.8f;
        jumpCutMultiplier = 0.5f;
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
    }

    public override void OnFormActivated()
    {
        _currentVelocityX = rb != null ? rb.velocity.x : 0f;
    }
}