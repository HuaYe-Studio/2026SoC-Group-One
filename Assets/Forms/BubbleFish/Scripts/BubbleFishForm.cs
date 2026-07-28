using UnityEngine;

public enum BubbleState
{
    Shrunk,
    Expanding,
    Expanded,
    Shrinking
}

public partial class BubbleFishForm : BaseForm
{
    [Header("Bubble Settings")]
    [SerializeField] private float expansionSpeed = 2.0f;
    [SerializeField] private float buoyancyForce = 8.0f; 
    [SerializeField] private float pressureForce = 5.0f;   
    [SerializeField] private float underwaterMoveSpeed = 3.5f;

    private BubbleState bubbleState = BubbleState.Shrunk;
    private float currentExpansion = 0f;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = underwaterMoveSpeed;
    }

    public void ToggleExpansion()
    {
        if (bubbleState == BubbleState.Shrunk || bubbleState == BubbleState.Shrinking)
            bubbleState = BubbleState.Expanding;
        else if (bubbleState == BubbleState.Expanded || bubbleState == BubbleState.Expanding)
            bubbleState = BubbleState.Shrinking;
    }

    protected override void FixedUpdate()
    {
        if (rb == null) return;
        UpdateBubbleState();
        ApplyBubblePhysics();
        base.FixedUpdate();
    }

    private void UpdateBubbleState()
    {
        if (bubbleState == BubbleState.Expanding)
        {
            currentExpansion = Mathf.Clamp01(currentExpansion + Time.fixedDeltaTime * expansionSpeed);
            if (currentExpansion >= 1f) bubbleState = BubbleState.Expanded;
        }
        else if (bubbleState == BubbleState.Shrinking)
        {
            currentExpansion = Mathf.Clamp01(currentExpansion - Time.fixedDeltaTime * expansionSpeed);
            if (currentExpansion <= 0f) bubbleState = BubbleState.Shrunk;
        }
    }

    private void ApplyBubblePhysics()
    {
        if (currentExpansion > 0.01f)
        {
            rb.AddForce(Vector2.up * buoyancyForce * currentExpansion, ForceMode2D.Force);
        }
        
        if (currentExpansion < 0.99f)
        {
            rb.AddForce(Vector2.down * pressureForce * (1f - currentExpansion), ForceMode2D.Force);
        }
    }

    public override void DoMovement(float horizontal)
    {
        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);
    }
}
