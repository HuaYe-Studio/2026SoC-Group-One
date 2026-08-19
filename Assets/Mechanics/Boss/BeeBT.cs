using UnityEngine;

/// <summary>
/// [BT] 蜜蜂行为树：驱动 BeeAI 的三态切换（移动全部走 BeeAStarMoveAction，A* 寻路）。
/// 优先级（高→低）：
///   1. 飞散 —— 目标受创到阈值（OnWeakened）或被击败 → 背离目标直飞出屏销毁
///   2. 攻击 —— 蜂巢已破坏且目标存活 → A* 接近目标 → 贴身蜜刺
///   3. 守护 —— A* 绕蜂巢巡游点轮换（默认态，像鱼群一样环境巡游）
/// A* 寻路复用 NavGrid2D（避开 ground/障碍）+ AnimalRegion 区域限定（BeeAI.CostAt），
/// 移动由 BeeAI.MoveAlong 执行（含 Boids 三力修正）。
/// 目标面向接口 IAttackTarget 编程，蜜蜂不依赖任何具体目标类。
/// </summary>
[RequireComponent(typeof(BeeAI))]
public class BeeBT : MonoBehaviour
{
    private BeeAI _bee;
    private BTSelector _root;

    private void Awake()
    {
        _bee = GetComponent<BeeAI>();

        _root = new BTSelector(
            // 1. 飞散：目标受创/被击败 → 出屏销毁（最高优先级，一旦触发不再回归攻击/守护）
            new BTSequence(
                new BTCondition(() => _bee.ShouldScatter),
                new BTAction(() =>
                {
                    _bee.Scatter();
                    return BTNode.State.Running;
                })
            ),
            // 2. 攻击：蜂巢已破坏 + 目标存活 → A* 接近（到达半径=蜜刺范围）→ 贴身蜜刺
            new BTSequence(
                new BTCondition(() => _bee.HiveDestroyed && _bee.IsTargetAlive),
                new BeeAStarMoveAction(_bee,
                    targetProvider: () => _bee.TargetPosition,
                    costAt: _bee.CostAt,
                    arriveRadius: _bee.StingRange,
                    move: (dir, mult) => _bee.MoveAlong(dir, mult)),
                new BTAction(() =>
                {
                    _bee.StingIfClose();
                    return BTNode.State.Success;
                })
            ),
            // 3. 守护：A* 绕蜂巢巡游点轮换（默认态）；到达后 onArrive 换下一个点，持续巡游
            new BeeAStarMoveAction(_bee,
                targetProvider: () => _bee.GetNextGuardPoint(),
                costAt: _bee.CostAt,
                arriveRadius: _bee.GuardArriveRadius,
                move: (dir, mult) => _bee.MoveAlong(dir, mult),
                onArrive: () => _bee.AdvanceGuardPoint())
        );
    }

    private void Update()
    {
        if (_root == null)
        {
            _bee = GetComponent<BeeAI>();
            _root = BuildTree();
        }

        _root.Tick();
    }

    private BTSelector BuildTree()
    {
        return new BTSelector(
            new BTSequence(
                new BTCondition(() => _bee.ShouldScatter),
                new BTAction(() =>
                {
                    _bee.Scatter();
                    return BTNode.State.Running;
                })
            ),
            new BTSequence(
                new BTCondition(() => _bee.HiveDestroyed && _bee.IsTargetAlive),
                new BeeAStarMoveAction(_bee,
                    targetProvider: () => _bee.TargetPosition,
                    costAt: _bee.CostAt,
                    arriveRadius: _bee.StingRange,
                    move: (dir, mult) => _bee.MoveAlong(dir, mult)),
                new BTAction(() =>
                {
                    _bee.StingIfClose();
                    return BTNode.State.Success;
                })
            ),
            new BeeAStarMoveAction(_bee,
                targetProvider: () => _bee.GetNextGuardPoint(),
                costAt: _bee.CostAt,
                arriveRadius: _bee.GuardArriveRadius,
                move: (dir, mult) => _bee.MoveAlong(dir, mult),
                onArrive: () => _bee.AdvanceGuardPoint())
        );
    }
}
