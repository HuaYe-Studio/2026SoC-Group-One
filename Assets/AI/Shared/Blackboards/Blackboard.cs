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

    /// <summary>威胁记忆是否仍有效（未超过记忆保留时长）。</summary>
    public bool HasThreatMemory => Time.time - LastSeenPlayerTime <= MemoryDuration;

    /// <summary>记忆保留时长（秒），超时彻底遗忘。</summary>
    public float MemoryDuration = 5f;

    /// <summary>逃跑触发半径：玩家进入此距离无论威胁值高低都视为紧迫威胁。</summary>
    public float FleeRadius = 5f;

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

    // ---- 便捷语义属性（决策层常用组合判断）----
    /// <summary>玩家是否构成紧迫威胁（威胁值足够高，或已贴脸；同形态友好除外）。</summary>
    public bool IsThreatUrgent => IsPlayerVisible && !IsPlayerSameForm
        && (ThreatLevel >= 50f || PlayerDistance <= FleeRadius);

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
    }
}
