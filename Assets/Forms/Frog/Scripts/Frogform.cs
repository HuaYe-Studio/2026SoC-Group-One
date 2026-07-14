using UnityEngine;

public class FrogForm : BaseForm
{
    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        gravityScale = 0.7f;
        fallGravityMultiplier = 1.2f;
    }

    protected override void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanJump())
            DoJump();

        if (Input.GetMouseButtonUp(0))
            ApplyJumpCut();
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
    }

    private void ApplyJumpCut()
    {
        if (rb.velocity.y > 0)
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
    }
}