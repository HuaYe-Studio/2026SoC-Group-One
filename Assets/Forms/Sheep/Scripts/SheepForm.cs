using UnityEngine;

public class SheepForm : BaseForm
{
    [Header("Charge")]
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeDuration = 0.5f;
    [SerializeField] private float chargeCooldown = 1.5f;

    [Header("Ice Slide")]
    [SerializeField] private float slideInitialSpeed = 8f;
    [SerializeField] private float slideDeceleration = 4.5f;

    private static readonly int AnimStateHash = Animator.StringToHash("SHEEP_AnimState");

    private bool _isCharging;
    private bool _isSliding;
    private float _chargeEndTime;
    private float _nextChargeTime;
    private int _chargeDirection;
    private float _slideVelocityX;

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3.5f;
        gravityScale = 1f;
        fallGravityMultiplier = 2f;
        groundLayer = LayerMask.GetMask("Ground");
    }

    public override void Die()
    {
        _isCharging = false;
        _isSliding = false;
        base.Die();
    }

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space += OnChargePressed;
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space -= OnChargePressed;
    }

    public void OnChargePressed()
    {
        if (_isCharging || _isSliding || Time.time < _nextChargeTime) return;
        if (currentState != ActionState.Idle && currentState != ActionState.Moving) return;
        StartCharge();
    }

    private void StartCharge()
    {
        _isCharging = true;
        _isSliding = false;
        _chargeEndTime = Time.time + chargeDuration;
        _chargeDirection = (int)FacingDirection.x;
        if (_chargeDirection == 0) _chargeDirection = 1;
        currentState = ActionState.SpecialAction;
        rb.velocity = new Vector2(_chargeDirection * chargeSpeed, rb.velocity.y);

        PlayerHP hp = controller != null ? controller.GetComponent<PlayerHP>() : null;
        if (hp != null) hp.SetInvincible(chargeDuration);

        SyncAnimator();
    }

    private void StopCharge()
    {
        _isCharging = false;
        _nextChargeTime = Time.time + chargeCooldown;
        currentState = ActionState.Idle;
        rb.velocity = new Vector2(0f, rb.velocity.y);
        SyncAnimator();
    }

    private void StartSlide()
    {
        _isCharging = false;
        _isSliding = true;
        _nextChargeTime = Time.time + chargeCooldown;
        _slideVelocityX = _chargeDirection * slideInitialSpeed;
        currentState = ActionState.SpecialAction;
        SyncAnimator();
    }

    private void UpdateSlide()
    {
        if (!_isSliding) return;

        _slideVelocityX = Mathf.MoveTowards(_slideVelocityX, 0f, slideDeceleration * Time.fixedDeltaTime);
        rb.velocity = new Vector2(_slideVelocityX, rb.velocity.y);

        if (Mathf.Abs(_slideVelocityX) < 0.05f)
        {
            _isSliding = false;
            currentState = ActionState.Idle;
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        SyncAnimator();
    }

    protected override void FixedUpdate()
    {
        if (currentState == ActionState.Dead) return;

        base.FixedUpdate();

        if (_isCharging)
        {
            if (Time.time >= _chargeEndTime)
            {
                if (controller != null && controller.IsOnIce)
                    StartSlide();
                else
                    StopCharge();
            }
            else
            {
                rb.velocity = new Vector2(_chargeDirection * chargeSpeed, rb.velocity.y);
            }
        }
        else if (_isSliding)
        {
            UpdateSlide();
        }

        SyncAnimator();
    }

    protected override void DoMovement(float horizontal)
    {
        if (_isCharging || _isSliding) return;
        base.DoMovement(horizontal);
    }

    public override void OnFormDeactivated()
    {
        base.OnFormDeactivated();
        if (_isCharging) StopCharge();
        if (_isSliding)
        {
            _isSliding = false;
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
    }

    private void SyncAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int state;
        if (_isCharging || _isSliding) state = 2;
        else if (Mathf.Abs(rb.velocity.x) > 0.1f) state = 1;
        else state = 0;
        animator.SetInteger(AnimStateHash, state);
    }
}