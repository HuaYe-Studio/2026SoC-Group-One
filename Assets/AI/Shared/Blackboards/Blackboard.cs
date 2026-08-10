using UnityEngine;

/// <summary>
/// AI 认知黑板：记录动物的内部认知状态，供行为树查询决策。
/// 感知层（EnvironmentMonitor）负责写入原始感知数据，
/// 认知层（NeedsSystem 等）负责写入内部状态，
/// 决策层（BT）只读取语义化状态，不直接依赖物理检测。
/// </summary>
[System.Serializable]
public class Blackboard
{
    // ---- 威胁认知（由 EnvironmentMonitor 写入）----
    /// <summary>连续威胁值：0=安全，50=警觉，100=极度危险。</summary>
    public float ThreatLevel;

    /// <summary>玩家最后已知位置，用于搜索或逃跑。</summary>
    public Vector2 LastKnownPlayerPos;

    /// <summary>最后感知到玩家的时间戳，用于记忆衰减。</summary>
    public float LastSeenPlayerTime = float.NegativeInfinity;

    /// <summary>当前帧是否实际感知到玩家（在视野锥或听觉范围内）。</summary>
    public bool IsPlayerVisible;

    /// <summary>玩家当前形态是否与自身相同（同形态=友好，不产生威胁）。</summary>
    public bool IsPlayerSameForm;

    /// <summary>到玩家的当前距离（仅 IsPlayerVisible 时有效）。</summary>
    public float PlayerDistance;

    /// <summary>玩家相对于自身的方向（归一化，仅 IsPlayerVisible 时有效）。</summary>
    public Vector2 PlayerDirection;

    /// <summary>动物自身当前位置（由 EnvironmentMonitor 每帧写入，用于近似距离计算）。</summary>
    public Vector2 AnimalPosition;

    /// <summary>威胁记忆是否仍有效（未超过记忆保留时长）。</summary>
    public bool HasThreatMemory => Time.time - LastSeenPlayerTime <= MemoryDuration;

    /// <summary>记忆保留时长（秒），超时彻底遗忘。</summary>
    public float MemoryDuration = 5f;

    /// <summary>逃跑触发半径：玩家进入此距离视为紧迫威胁（进入阈值）。</summary>
    public float FleeRadius = 5f;

    /// <summary>威胁解除半径：玩家离开此距离才视为安全（解除阈值，> FleeRadius，避免边界抖动）。</summary>
    public float SafeRadius = 8f;

    /// <summary>威胁消退阈值：玩家仍可见但威胁值低于该值视为已安全（静止伪装/远离），否则保持警戒。
    /// 阈值取 60：玩家静止伪装时威胁以 ~10/s 下降到该值以下，约 6s 后鱼恢复巡游；
    /// 玩家一旦移动威胁即 >60，鱼持续警戒，避免"玩家守在回撤点还硬要回去"的来回荡。</summary>
    public float CalmThreatThreshold = 60f;

    // ---- 食物感知（由 EnvironmentMonitor 写入）----
    /// <summary>当前是否检测到食物。</summary>
    public bool IsFoodDetected;

    /// <summary>最近食物的方向（归一化，仅 IsFoodDetected 时有效）。</summary>
    public Vector2 FoodDirection;

    /// <summary>到最近食物的距离（仅 IsFoodDetected 时有效）。</summary>
    public float FoodDistance;

    /// <summary>最近食物的 Transform（仅 IsFoodDetected 时有效）。</summary>
    public Transform NearestFood;

    // ---- 地形感知（由 EnvironmentMonitor 写入）----
    /// <summary>前方是否有地面。</summary>
    public bool IsGroundedAhead;

    /// <summary>前方是否有墙壁。</summary>
    public bool IsWallAhead;

    /// <summary>前方是否有沟壑（无地面）。</summary>
    public bool IsGapAhead;

    // ---- 内部需求（由 NeedsSystem 写入，预留）----
    /// <summary>饥饿度（0-100），驱动觅食行为。</summary>
    public float HungerLevel;

    /// <summary>当前正在执行的行为 ID，供调试或 UI 使用。</summary>
    public string CurrentBehavior = "";

    // ---- 眩晕状态（由吞噬系统等外部写入）----
    /// <summary>眩晕截止时间点（Time.time）。被吞噬/受击时由外部设置。</summary>
    public float StunUntilTime = float.NegativeInfinity;

    /// <summary>当前是否处于眩晕中（僵直，不感知、不行动）。</summary>
    public bool IsStunned => Time.time < StunUntilTime;

    // ---- 回撤状态（由逃生节点写入，回撤节点读取）----
    /// <summary>逃生起点：脱离危险后需要返回的位置（由逃生节点进入时记录）。</summary>
    public Vector2 RetreatTarget;

    /// <summary>当前是否存在未完成的回撤目标。</summary>
    public bool HasRetreatTarget;

    // ---- 便捷语义属性（决策层常用组合判断）----
    // 迟滞状态：当前是否处于威胁中（避免 IsThreatUrgent 在 FleeRadius 边界反复跳变）
    private bool _isThreatUrgent;

    /// <summary>威胁解除的最小持续时长（秒）：进入威胁后至少维持这么久才允许解除，防止单帧抖动。</summary>
    public float ThreatMinHoldDuration = 0.5f;

    /// <summary>进入威胁的时间点（Time.time），用于最小持续时长判定。</summary>
    private float _threatEnterTime = float.NegativeInfinity;

    /// <summary>
    /// 玩家是否构成紧迫威胁（带迟滞 + 最小持续时长：进入用 FleeRadius，解除用 SafeRadius，
    /// 且进入后至少持续 ThreatMinHoldDuration 才允许解除，避免边界抖动导致的逃生/回撤死循环）。
    /// 每帧由感知层或决策层调用 RefreshThreatUrgent() 刷新。
    /// </summary>
    public bool IsThreatUrgent => _isThreatUrgent;

    /// <summary>
    /// 刷新威胁紧迫状态（迟滞 + 最小持续时长判断）。同形态友好时恒为安全。
    /// </summary>
    public void RefreshThreatUrgent()
    {
        // 同形态友好：恒为安全
        if (IsPlayerSameForm)
        {
            SetThreatUrgent(false);
            return;
        }

        // 高威胁值立即视为紧迫——仅当距离仍在解除阈值内时生效。
        // 距离一旦超过 SafeRadius，即使威胁值仍高也走下方距离迟滞解除，
        // 避免"玩家一直可见且移动"时威胁值锁死、鱼一路直线逃到地图外（一去不回）。
        if (IsPlayerVisible && ThreatLevel >= 50f && PlayerDistance <= SafeRadius)
        {
            SetThreatUrgent(true);
            return;
        }

        // 玩家不可见时：用鱼到最后已知位置的距离做近似。
        // 玩家离开后实际距离必然增大，近似距离随鱼远离而增大，迟滞窗口能正常解除威胁。
        if (!IsPlayerVisible)
        {
            if (LastKnownPlayerPos == Vector2.zero)
            {
                // 无记忆：安全
                SetThreatUrgent(false);
                return;
            }
            float approxDistance = Vector2.Distance(AnimalPosition, LastKnownPlayerPos);
            if (approxDistance >= SafeRadius && Time.time - _threatEnterTime >= ThreatMinHoldDuration)
                SetThreatUrgent(false);
            return;
        }

        // 距离迟滞：进入阈值触发，离开解除阈值才解除
        if (PlayerDistance <= FleeRadius)
        {
            SetThreatUrgent(true);
        }
        else if (PlayerDistance >= SafeRadius)
        {
            // 视觉条件：玩家仍可见且威胁值未消退（玩家在移动/未伪装）时保持警戒，
            // 避免鱼"当着玩家面"解除威胁进入巡游（B3）。玩家已不可见或威胁值已消退才允许解除。
            if (IsPlayerVisible && ThreatLevel > CalmThreatThreshold)
                return;

            // 解除前检查最小持续时长：进入时间过短则仍保持威胁，防止临界区单帧抖动
            if (Time.time - _threatEnterTime >= ThreatMinHoldDuration)
                SetThreatUrgent(false);
        }
        // FleeRadius < PlayerDistance < SafeRadius：保持 _isThreatUrgent 当前值不变
    }

    private void SetThreatUrgent(bool value)
    {
        if (_isThreatUrgent == value)
            return;

        _isThreatUrgent = value;
        _threatEnterTime = value ? Time.time : float.NegativeInfinity;
    }

    /// <summary>是否需要前往最后已知位置搜索（有记忆但当前不可见；同形态友好除外）。</summary>
    public bool ShouldSearch => !IsPlayerVisible && !IsPlayerSameForm && HasThreatMemory && ThreatLevel > 20f;

    /// <summary>是否彻底安全（无可见威胁且威胁值已消退）。</summary>
    public bool IsSafe => !IsPlayerVisible && ThreatLevel <= 10f;

    /// <summary>清空威胁认知（威胁值/记忆/可见性），用于吞噬后或形态切换后重置。</summary>
    public void ClearThreat()
    {
        ThreatLevel = 0f;
        LastKnownPlayerPos = Vector2.zero;
        LastSeenPlayerTime = float.NegativeInfinity;
        IsPlayerVisible = false;
    }

    /// <summary>重置所有认知状态（用于 AI 重置或出生）。</summary>
    public void Clear()
    {
        ClearThreat();
        IsPlayerSameForm = false;
        StunUntilTime = float.NegativeInfinity;
        PlayerDistance = 0f;
        PlayerDirection = Vector2.zero;
        IsFoodDetected = false;
        FoodDirection = Vector2.zero;
        FoodDistance = 0f;
        NearestFood = null;
        IsGroundedAhead = false;
        IsWallAhead = false;
        IsGapAhead = false;
        HungerLevel = 0f;
        CurrentBehavior = "";
        RetreatTarget = Vector2.zero;
        HasRetreatTarget = false;
        _isThreatUrgent = false;
        _threatEnterTime = float.NegativeInfinity;
    }
}
