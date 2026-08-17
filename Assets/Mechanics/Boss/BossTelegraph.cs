using UnityEngine;

/// <summary>
/// 红框预警组件：攻击前显示伤害区域，给玩家反应窗口。
/// 由 UI/组员实现表现（红框 Sprite/LineRenderer/闪烁动画），Boss 侧只依赖本契约。
/// </summary>
public class BossTelegraph : MonoBehaviour
{
    [Tooltip("红框渲染组件（组员实现时挂具体渲染器，这里预留引用）")]
    [SerializeField] private SpriteRenderer _boxRenderer;

    [Tooltip("红框尺寸初始值（由 BossAttack.hitboxSize 驱动）")]
    [SerializeField] private Vector2 _boxSize = new Vector2(3f, 1.5f);

    private bool _isActive;

    /// <summary>当前是否处于预警中。</summary>
    public bool IsActive => _isActive;

    /// <summary>红框尺寸（由攻击实现按 hitboxSize 设置）。</summary>
    public Vector2 BoxSize
    {
        get => _boxSize;
        set
        {
            _boxSize = value;
            if (_boxRenderer != null)
                _boxRenderer.size = _boxSize;
        }
    }

    private void Awake()
    {
        if (_boxRenderer == null)
            _boxRenderer = GetComponent<SpriteRenderer>();
        Hide();
    }

    /// <summary>跟随预警：红框跟随目标持续 followDuration（由攻击实现负责时长与隐藏）。</summary>
    public void ShowFollow(Transform target)
    {
        _isActive = true;
        gameObject.SetActive(true);
        if (_boxRenderer != null)
        {
            _boxRenderer.enabled = true;
            _boxRenderer.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        }
    }

    /// <summary>锁定预警：红框锁定在 pos，闪烁 lockDuration（由攻击实现负责时长与隐藏）。</summary>
    public void ShowLock(Vector2 pos)
    {
        _isActive = true;
        gameObject.SetActive(true);
        transform.position = pos;
        if (_boxRenderer != null)
            _boxRenderer.enabled = true;
    }

    /// <summary>隐藏红框。</summary>
    public void Hide()
    {
        _isActive = false;
        gameObject.SetActive(false);
        if (_boxRenderer != null)
            _boxRenderer.enabled = false;
    }

    /// <summary>定位到指定位置（跟随预警每帧调用）。</summary>
    public void FollowTo(Vector2 pos)
    {
        if (_isActive)
            transform.position = pos;
    }
}
