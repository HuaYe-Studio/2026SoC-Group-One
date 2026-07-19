using UnityEngine;
public enum ActionState
{
    Idle,
    Moving,
    Jumping,
    Falling,
    SpecialAction,
    Locked
}

public abstract class BaseForm : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private FormType formType;
    public FormType FormType => formType;

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 5f;

    [Header("Gravity")]
    [SerializeField] protected float gravityScale = 1f;
    [SerializeField] protected float fallGravityMultiplier = 1.5f;

    [Header("Ground Check")]
    [SerializeField] protected float groundCheckWidth = 0.8f;
    [SerializeField] protected float groundCheckHeight = 0.05f;
    [SerializeField] protected float groundCheckVerticalOffset = 0.02f;
    [SerializeField] protected LayerMask groundLayer;

    protected Rigidbody2D rb;
    protected Collider2D myCollider;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected PlayerController controller;

    protected ActionState currentState = ActionState.Idle;
    protected float ignoreGroundUntil;

    public bool IsGrounded { get; protected set; }
    public ActionState CurrentState => currentState;
    public Animator Animator => animator;

    public virtual void Initialize(PlayerController ctrl)
    {
        controller = ctrl;
        rb = GetComponentInParent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    protected virtual bool CanMove() =>
        currentState == ActionState.Idle ||
        currentState == ActionState.Moving ||
        currentState == ActionState.Jumping ||
        currentState == ActionState.Falling;

    public void SetActionState(ActionState state)
    {
        currentState = state;
    }

    public void SetAnimatorBool(string name, bool value)
    {
        if (animator != null)
            animator.SetBool(name, value);
    }

    public void ProcessInput(float horizontal)
    {
        if (currentState == ActionState.Locked || currentState == ActionState.SpecialAction) return;

        DoMovement(horizontal);
        UpdateFacing(horizontal);

        HandleInput();
    }

    public virtual void DoMovement(float horizontal)
    {
        if (!CanMove()) return;

        rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        if (currentState == ActionState.Idle || currentState == ActionState.Moving)
            currentState = Mathf.Abs(horizontal) > 0.1f ? ActionState.Moving : ActionState.Idle;
    }

    protected virtual void UpdateFacing(float horizontal)
    {
        if (spriteRenderer == null) return;
        if (horizontal > 0.1f)
            spriteRenderer.flipX = false;
        else if (horizontal < -0.1f)
            spriteRenderer.flipX = true;
    }

    protected virtual void HandleInput() { }

    protected virtual void FixedUpdate()
    {
        PerformGroundCheck();
        UpdateAirState();
        HandleLanding();
        ApplyGravity();
    }

    protected virtual Vector2 GetGroundCheckOrigin()
    {
        if (myCollider == null)
            return (Vector2)transform.position + Vector2.down * 0.5f;

        Bounds bounds = myCollider.bounds;
        return new Vector2(bounds.center.x, bounds.min.y) + Vector2.up * groundCheckVerticalOffset;
    }

    protected virtual Vector2 GetGroundCheckSize()
    {
        float width = myCollider != null ? myCollider.bounds.size.x * groundCheckWidth : 0.4f;
        return new Vector2(width, groundCheckHeight);
    }

    protected virtual void PerformGroundCheck()
    {
        if (Time.fixedTime < ignoreGroundUntil) return;

        Vector2 origin = GetGroundCheckOrigin();
        Vector2 size = GetGroundCheckSize();
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.05f, groundLayer);
        IsGrounded = hit.collider != null;
    }

    protected virtual void UpdateAirState()
    {
        if (currentState == ActionState.Jumping && rb.velocity.y < 0)
            currentState = ActionState.Falling;
    }

    protected virtual void HandleLanding()
    {
        if (IsGrounded && (currentState == ActionState.Falling || currentState == ActionState.Jumping))
            currentState = ActionState.Idle;
    }

    protected virtual void ApplyGravity()
    {
        bool falling = rb.velocity.y < -0.1f;
        rb.gravityScale = falling ? gravityScale * fallGravityMultiplier : gravityScale;
    }

    public virtual void OnFormActivated() { }
    public virtual void OnFormDeactivated() { }

    protected GameObject SpawnPersistent(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (controller == null || controller.SpawnedObjectContainer == null)
            return Instantiate(prefab, position, rotation);

        return Instantiate(prefab, position, rotation, controller.SpawnedObjectContainer);
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(GetGroundCheckOrigin(), GetGroundCheckSize());
    }
#endif
}
