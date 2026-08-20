using UnityEngine;

/// <summary>
/// 红框预警组件：攻击前显示伤害区域，给玩家反应窗口。
/// 表现（红框 Sprite/LineRenderer/闪烁动画）由表现层实现，Boss 侧只依赖本契约。
/// </summary>
public class BossTelegraph : MonoBehaviour
{
    [Tooltip("红框渲染组件（表现层实现时挂具体渲染器，这里预留引用）")]
    [SerializeField] private SpriteRenderer _boxRenderer;

    [Tooltip("红框尺寸初始值（由 BossAttack.hitboxSize 驱动）")]
    [SerializeField] private Vector2 _boxSize = new Vector2(3f, 1.5f);

    private bool _isActive;

    /// <summary>当前是否处于预警中。</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 可用红框渲染器：只有「独立于本组件挂载物体的 SpriteRenderer」才可用。
    /// 若 _boxRenderer 恰好是 BOSS 自身的渲染器（根物体/身体子物体），对其做位移/显隐会直接作用到 BOSS 身上，
    /// 造成闪现上升、身体隐藏等灾难性副作用——此类情况一律按无红框处理（no-op），直到表现层挂独立红框子物体。
    /// </summary>
    private SpriteRenderer Box => (_boxRenderer != null && _boxRenderer.transform != transform) ? _boxRenderer : null;

    /// <summary>红框尺寸（由攻击实现按 hitboxSize 设置）。</summary>
    public Vector2 BoxSize
    {
        get => _boxSize;
        set
        {
            _boxSize = value;
            if (Box != null)
                Box.size = _boxSize;
        }
    }

    private void Awake()
    {
        // 只查自身渲染器：不能向下查找，否则会误抓 BOSS 身体子物体的 SpriteRenderer。
        // 注意：即便抓到 BOSS 自身渲染器，Box 守卫也会拦截所有操作（见 Box 属性注释）。
        if (_boxRenderer == null)
            _boxRenderer = GetComponent<SpriteRenderer>();
        Hide();
    }

    /// <summary>跟随预警：红框跟随目标持续 followDuration（由攻击实现负责时长与隐藏）。</summary>
    public void ShowFollow(Transform target)
    {
        _isActive = true;
        if (Box != null)
        {
            Box.enabled = true;
            Box.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        }
    }

    /// <summary>锁定预警：红框锁定在 pos，闪烁 lockDuration（由攻击实现负责时长与隐藏）。</summary>
    public void ShowLock(Vector2 pos)
    {
        _isActive = true;
        if (Box != null)
        {
            Box.transform.position = pos;
            Box.enabled = true;
        }
    }

    /// <summary>隐藏红框（只禁渲染器，不 SetActive 挂载物体——本组件可能挂在 BOSS 根物体上）。</summary>
    public void Hide()
    {
        _isActive = false;
        if (Box != null)
            Box.enabled = false;
    }

    /// <summary>设置红框可见性（锁定阶段闪烁用，不改变 _isActive 状态）。</summary>
    public void SetVisible(bool visible)
    {
        if (Box != null)
            Box.enabled = visible;
    }

    /// <summary>定位红框到指定位置（跟随预警每帧调用）。只移动红框渲染器，不影响挂载物体。</summary>
    public void FollowTo(Vector2 pos)
    {
        if (_isActive && Box != null)
            Box.transform.position = pos;
    }
}
