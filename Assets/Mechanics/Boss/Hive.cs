using UnityEngine;

/// <summary>
/// 蜂巢：BOSS 战的破坏目标。具体表现（模型/粒子/音效）由表现层实现，
/// Boss 侧只依赖本契约调用 TakeHit / 订阅 OnDestroyed。
/// 破坏全部蜂巢 → BossController.NotifyHiveDestroyed → 进入胜利时序（PendingVictory）。
/// </summary>
public class Hive : MonoBehaviour
{
    [Header("蜂巢配置")]
    [SerializeField] private int hiveIndex = 0;

    [Tooltip("破坏所需命中次数")]
    [SerializeField] private int hitsToDestroy = 1;

    private int _currentHits;
    private bool _isDestroyed;

    /// <summary>蜂巢编号（1/2/3，供 OnHiveDestroyed 事件与 UI 定位）。</summary>
    public int HiveIndex => hiveIndex;

    /// <summary>是否已破坏。</summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>剩余所需命中次数。</summary>
    public int RemainingHits => Mathf.Max(0, hitsToDestroy - _currentHits);

    /// <summary>蜂巢被破坏事件（→ BossController.NotifyHiveDestroyed）。</summary>
    public event System.Action<Hive> OnDestroyed;

    /// <summary>
    /// 受击（BOSS 落点命中判定调用）。hitPoint 在蜂巢判定盒内时累计次数，达阈值即破坏。
    /// 破坏只触发一次：触发 OnDestroyed 事件 + 广播 MockEventCenter.OnHiveDestroyed。
    /// </summary>
    public void TakeHit(Vector2 hitPoint)
    {
        if (_isDestroyed) return;

        _currentHits++;
        if (_currentHits < hitsToDestroy) return;

        _isDestroyed = true;
        OnDestroyed?.Invoke(this);
        MockEventCenter.TriggerHiveDestroyed(hiveIndex);
        // 表现（模型隐藏/粒子/音效）由表现层处理，这里仅禁用碰撞与渲染占位
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
    }

    /// <summary>重置蜂巢（BOSS 重开/重打时用）。</summary>
    public void ResetHive()
    {
        _currentHits = 0;
        _isDestroyed = false;
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = true;
    }
}
