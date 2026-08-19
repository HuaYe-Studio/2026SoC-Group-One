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
    [SerializeField] private float idleDamp = 6.0f;

    private BubbleState bubbleState = BubbleState.Shrunk;
    private float currentExpansion = 0.5f;
    private bool _floatHeld;
    private bool _diveHeld;

    private CircleCollider2D _circleCollider;
    private BoxCollider2D _boxCollider;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = underwaterMoveSpeed;
        gravityScale = 0f;
        currentExpansion = 0.5f;

        _circleCollider = GetComponent<CircleCollider2D>();
        _boxCollider = GetComponent<BoxCollider2D>();
        if (_circleCollider == null) _circleCollider = gameObject.AddComponent<CircleCollider2D>();
        if (_boxCollider == null) _boxCollider = gameObject.AddComponent<BoxCollider2D>();
        _circleCollider.radius = 0.5f;
        _boxCollider.size = new Vector2(0.6f, 0.3f);
        UpdateColliders();
    }

    // 绑定：Ability1（鼠标左键）按下 = 上浮
    public void StartExpand()
    {
        _floatHeld = true;
    }

    // 绑定：Ability1（鼠标左键）松开 = 停止上浮，回到中性浮力
    public void StopExpand()
    {
        _floatHeld = false;
    }

    // 绑定：Ability2（鼠标右键）按下 = 下潜
    public void StartShrink()
    {
        _diveHeld = true;
    }

    // 绑定：Ability2（鼠标右键）松开 = 停止下潜，回到中性浮力
    public void StopShrink()
    {
        _diveHeld = false;
    }

    // 保留旧接口，避免其它地方报错
    public void ToggleExpansion()
    {
        if (bubbleState == BubbleState.Shrunk || bubbleState == BubbleState.Shrinking)
            bubbleState = BubbleState.Expanding;
        else
            bubbleState = BubbleState.Shrinking;
    }

    protected override void FixedUpdate()
    {
        if (rb == null) return;
        UpdateBubbleState();
        ApplyBubblePhysics();
        UpdateAnimator();
        base.FixedUpdate();
    }

    private void UpdateColliders()
    {
        bool expanded = currentExpansion >= 0.5f;
        if (_circleCollider != null) _circleCollider.enabled = expanded;
        if (_boxCollider != null) _boxCollider.enabled = !expanded;
    }

    private void UpdateBubbleState()
    {
        if (_floatHeld && !_diveHeld)
        {
            currentExpansion = Mathf.Clamp01(currentExpansion + Time.fixedDeltaTime * expansionSpeed);
            bubbleState = currentExpansion >= 1f ? BubbleState.Expanded : BubbleState.Expanding;
        }
        else if (_diveHeld && !_floatHeld)
        {
            currentExpansion = Mathf.Clamp01(currentExpansion - Time.fixedDeltaTime * expansionSpeed);
            bubbleState = currentExpansion <= 0f ? BubbleState.Shrunk : BubbleState.Shrinking;
        }
        else
        {
            if (currentExpansion >= 0.999f) bubbleState = BubbleState.Expanded;
            else if (currentExpansion <= 0.001f) bubbleState = BubbleState.Shrunk;
        }

        UpdateColliders();
    }

    private void ApplyBubblePhysics()
    {
        if (_floatHeld && !_diveHeld)
        {
            rb.drag = 0f;
            rb.AddForce(Vector2.up * buoyancyForce * currentExpansion, ForceMode2D.Force);
        }
        else if (_diveHeld && !_floatHeld)
        {
            rb.drag = 0f;
            rb.AddForce(Vector2.down * pressureForce * (1f - currentExpansion), ForceMode2D.Force);
        }
        else
        {
            // 中性浮力：无外力，用 drag 让垂直速度逐渐归零（悬浮）
            rb.drag = idleDamp;
        }
    }

    public override void OnFormDeactivated()
    {
        base.OnFormDeactivated();
        _floatHeld = false;
        _diveHeld = false;
        if (rb != null) rb.drag = 0f;
    }

    protected override void DoMovement(float horizontal)
    {
        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);
    }
}