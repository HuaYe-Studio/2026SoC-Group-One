using UnityEngine;

/// <summary>
/// 泡泡鱼 AI：继承 AnimalBase，适配水生环境的游动移动。
/// 与陆地动物不同，泡泡鱼无视重力，可以在水中自由游动。
/// 覆写 PerformMove 实现平滑游动。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BubbleFishAI : AnimalBase
{
    [Header("Swim Settings")]
    [SerializeField] private float _swimSpeed = 2f;
    [SerializeField] private float _swimVerticalDrift = 0.5f; // 游动时的轻微上下漂移

    [Tooltip("垂直速度比例：上下游动速度 = 水平速度 × 此系数。鱼体体积感——上下游动明显慢于水平")]
    [SerializeField, Range(0.05f, 1f)] private float _verticalSpeedRatio = 0.4f;

    [Tooltip("上升减速比例：上升速度在垂直基础上再 × 此系数（上升比下降更慢，模拟浮力/体积感）")]
    [SerializeField, Range(0.05f, 1f)] private float _ascendSpeedRatio = 0.5f;

    [Header("Vertical Expansion (上下膨胀体积感)")]
    [Tooltip("方向检测采样间隔（秒，锚点趋势法）：每间隔记录一次位置，比较差值判断上浮/下沉趋势，过滤瞬时速度波动，不依赖瞬时速度")]
    [SerializeField] private float _expansionSampleInterval = 0.3f;

    [Tooltip("锚点位移判定阈值（米）：采样间隔内纵向位移小于此值视为静止/方向不明")]
    [SerializeField] private float _expansionMoveThreshold = 0.05f;

    [Tooltip("膨胀阻力系数：实际膨胀度 = Lerp(当前值, 目标值, 阻力系数 × deltaTime)。数值越小越平滑，自带惯性，方向突变也不会硬切")]
    [SerializeField, Range(0.5f, 20f)] private float _expansionSmoothFactor = 8f;

    [Tooltip("下沉时（目标膨胀度 0.0）的 Y 轴缩放比例")]
    [SerializeField, Range(0.3f, 1f)] private float _compressedScaleY = 0.8f;

    [Tooltip("上浮时（目标膨胀度 1.0）的 Y 轴缩放比例")]
    [SerializeField, Range(1f, 2f)] private float _stretchedScaleY = 1.2f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：与 BubbleFishForm 的 BubbleState 枚举保持一致
    private const string AnimStateParam = "BubbleState";

    // 独立游泳动画参数：0=Idle 1=SwimForward 2=SwimUp 3=SwimDown
    // 与玩家形态的 BubbleState 解耦，供路径跟随等 NPC 行为使用
    private const string SwimStateParam = "SwimState";

    private float _driftTimer;
    private float _driftDirectionY;

    // ---- 上下膨胀动画状态（三层：锚点趋势 → 目标设定 → Lerp 阻力平滑）----
    private Vector3 _baseScale;          // Awake 捕获的初始缩放（防止非 1 初始缩放被覆盖）
    private Vector2 _lastExpansionAnchor; // 上一次锚点位置
    private float _nextExpansionSampleTime; // 下次锚点采样时间
    private float _targetExpansion;       // 目标膨胀度：上浮 1.0 / 下沉 0.0 / 方向不明保持不变
    private float _currentExpansion;      // 当前膨胀度（Lerp 平滑后的实际值）

    /// <summary>
    /// 泡泡鱼始终处于"水"中，无需地面检测。
    /// </summary>
    public override bool IsGrounded { get => true; protected set { } }

    protected override void Awake()
    {
        base.Awake();

        // 暴露 Animator 给基类，供吞噬系统等外部调用
        if (_animator != null)
            Animator = _animator;

        // 泡泡鱼不受重力影响；手动控制速度，drag 必须为 0，否则物理阻尼会抵消 velocity
        if (Rb != null)
        {
            Rb.gravityScale = 0f;
            Rb.drag = 0f;
        }

        // 膨胀动画：捕获初始缩放作为基准，避免非 1 初始缩放被覆盖
        _baseScale = transform.localScale;
        _lastExpansionAnchor = transform.position;
        _nextExpansionSampleTime = Time.time;
    }

    protected override void Update()
    {
        base.Update();
        UpdateVerticalExpansion();
    }

    /// <summary>
    /// 泡泡鱼的移动方式：平滑游动，带有轻微的上下漂移。
    /// </summary>
    public override void PerformMove(float direction, float speedMultiplier = 1f)
    {
        NotifyMoveCommand();

        // 水平游动
        float horizontalVelocity = direction * _swimSpeed * speedMultiplier;

        // 轻微的垂直漂移，让游动看起来更自然
        _driftTimer += Time.deltaTime;
        if (_driftTimer > 1.5f)
        {
            _driftTimer = 0f;
            _driftDirectionY = Random.Range(-1f, 1f);
        }

        float verticalVelocity = _driftDirectionY * _swimVerticalDrift;

        Rb.velocity = new Vector2(horizontalVelocity, verticalVelocity);

        // 翻转朝向
        if (SpriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            SpriteRenderer.flipX = direction < 0;
    }

    /// <summary>
    /// 朝任意方向游动（路径跟随等垂直+水平混合移动用）。
    /// 各向异性速度：水平分量按 _swimSpeed；垂直（上下）分量 = 水平 × _verticalSpeedRatio，
    /// 上升再 × _ascendSpeedRatio（上升比下降更慢），体现鱼体的体积感——上下游动明显慢于水平游动。
    /// </summary>
    /// <param name="direction">游动方向（归一化向量）</param>
    /// <param name="speedMultiplier">速度倍率</param>
    public void Swim(Vector2 direction, float speedMultiplier = 1f)
    {
        NotifyMoveCommand();

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        // 各向异性：水平 / 垂直 / 上升(更慢) 分开计算
        float horizontalVelocity = direction.x * _swimSpeed * speedMultiplier;
        float verticalSpeed = _swimSpeed * _verticalSpeedRatio * speedMultiplier;
        if (direction.y > 0f)
            verticalSpeed *= _ascendSpeedRatio; // 上升更慢，模拟浮力阻力
        float verticalVelocity = direction.y * verticalSpeed;

        Rb.velocity = new Vector2(horizontalVelocity, verticalVelocity);

        // 翻转朝向：只有水平分量明显时才翻转，防止纯上浮时抖动
        if (SpriteRenderer != null && Mathf.Abs(direction.x) > 0.2f)
            SpriteRenderer.flipX = direction.x < 0;
    }

    /// <summary>
    /// 完全停止游动（覆写基类）：鱼无视重力，垂直速度必须一并归零，
    /// 否则到达安全点停留时会因残留垂直速度上下漂移（抖动源之一）。
    /// </summary>
    public override void StopMoving()
    {
        Rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 垂直方向三层膨胀动画（体态体积感）：
    /// 第一层 方向检测（锚点趋势法）：每 _expansionSampleInterval 秒记录一次位置，比较差值判断上浮/下沉趋势
    ///        → 过滤掉瞬时速度波动，不依赖瞬时速度；
    /// 第二层 目标设定：上浮 → 目标膨胀度 1.0；下沉 → 目标膨胀度 0.0；方向不明 → 目标不变；
    /// 第三层 实际膨胀（带阻力平滑）：膨胀度 = Lerp(当前值, 目标值, 阻力系数 × deltaTime)
    ///        → 自带惯性，方向突变也不会硬切。
    /// 当前实现用 Y 轴缩放模拟（先缩放，与现有移动系统互不矛盾）。
    /// TODO(预留)：后续改为精灵图平滑过渡方案——维护上浮/水平/下沉三套精灵帧，按膨胀度在
    /// 相邻两套精灵间做 sprite 交叉淡入淡出（或按膨胀度切换 sprite），彻底替代 Y 轴缩放，
    /// 视觉更柔和。BubbleFishForm 的 BubbleState 枚举可复用为过渡参考。
    /// </summary>
    private void UpdateVerticalExpansion()
    {
        // 第一层：锚点趋势法采样（每间隔采样一次位置）
        if (Time.time >= _nextExpansionSampleTime)
        {
            _nextExpansionSampleTime = Time.time + _expansionSampleInterval;

            Vector2 delta = (Vector2)transform.position - _lastExpansionAnchor;
            _lastExpansionAnchor = transform.position;

            // 第二层：目标设定
            if (delta.y > _expansionMoveThreshold)
                _targetExpansion = 1f;   // 上浮
            else if (delta.y < -_expansionMoveThreshold)
                _targetExpansion = 0f;   // 下沉
            // 方向不明（|delta.y| <= 阈值）：目标不变，保持惯性
        }

        // 第三层：实际膨胀（阻力平滑，每帧执行，自带惯性）
        _currentExpansion = Mathf.Lerp(_currentExpansion, _targetExpansion,
            _expansionSmoothFactor * Time.deltaTime);

        // 膨胀度 [0,1] 映射到 Y 轴缩放 [compressed, stretched]
        float scaleY = Mathf.Lerp(_compressedScaleY, _stretchedScaleY, _currentExpansion);
        Vector3 scale = _baseScale;
        scale.y = _baseScale.y * scaleY;
        transform.localScale = scale;
    }

    /// <summary>
    /// 根据状态名设置 Animator 动画参数。
    /// 游泳类状态（SwimUp/SwimDown/SwimForward）写入独立的 SwimState 参数，
    /// 其余状态保持原有的 BubbleState 映射不变。
    /// </summary>
    public override void PlayAnimation(string stateName)
    {
        if (_animator == null) return;

        // 游泳类状态：写入独立的 SwimState 参数
        switch (stateName)
        {
            case AnimalAnimNames.SwimForward: SetSwimState(1); return;
            case AnimalAnimNames.SwimUp: SetSwimState(2); return;
            case AnimalAnimNames.SwimDown: SetSwimState(3); return;
        }

        int state = 0; // 默认 Shrunk/Idle
        switch (stateName)
        {
            case AnimalAnimNames.Idle: state = 0; break;
            case AnimalAnimNames.Flee: state = 1; break;
            case AnimalAnimNames.Stunned: state = 3; break;
            case AnimalAnimNames.Expanded: state = 2; break;
        }

        _animator.SetInteger(AnimStateParam, state);
    }

    /// <summary>
    /// 设置游泳动画参数，仅在该值变化时写入，避免每帧重复 SetInteger。
    /// </summary>
    private void SetSwimState(int value)
    {
        if (_animator.GetInteger(SwimStateParam) == value)
            return;

        _animator.SetInteger(SwimStateParam, value);
    }

    /// <summary>
    /// 游动速度（供 BT 节点读取）。
    /// </summary>
    public float SwimSpeed => _swimSpeed;

    /// <summary>
    /// 垂直漂移幅度（供 BT 节点读取）。
    /// </summary>
    public float SwimVerticalDrift => _swimVerticalDrift;

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 绘制巡游半径
        Gizmos.color = Color.cyan;
        Vector2 spawnPos = Application.isPlaying ? SpawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(spawnPos, PatrolRadius);
    }
#endif
}
