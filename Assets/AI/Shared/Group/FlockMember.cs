using UnityEngine;

/// <summary>
/// [群体] 群体成员标识：挂载后动物自动加入群体（FlockManager 注册），
/// 供 Boids 群体行为查询邻居、质心、平均朝向。
/// 群 ID：默认取自身 Tag（同物种同群）；可手动覆写以细分小群
/// （如两片水域的鱼群配置不同 ID，互不混群）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FlockMember : MonoBehaviour
{
    [Tooltip("群 ID：同 ID 的动物互为群体邻居。留空 = 用自身 Tag（同物种一群）")]
    [SerializeField] private string _flockId = "";

    private Rigidbody2D _rb;

    /// <summary>群 ID（未配置时回落到自身 Tag）。</summary>
    public string FlockId => string.IsNullOrEmpty(_flockId) ? gameObject.tag : _flockId;

    /// <summary>运行时设置群 ID（蜜蜂等动态生成个体按巢成群用）。</summary>
    public void SetFlockId(string flockId) => _flockId = flockId;

    /// <summary>当前速度（Boids 对齐力输入）。</summary>
    public Vector2 Velocity => _rb != null ? _rb.velocity : Vector2.zero;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        FlockManager.Register(this);
    }

    private void OnDisable()
    {
        FlockManager.Unregister(this);
    }
}
