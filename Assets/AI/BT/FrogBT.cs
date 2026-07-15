using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 青蛙行为树构建器：组装行为树并挂载到 BehaviorTreeRunner 启动。
/// 逻辑：优先逃跑→捕食→跳跃→休息 循环。
/// </summary>
public class FrogBT : MonoBehaviour
{
    private BehaviorTreeRunner _runner;

    private void Awake()
    {
        _runner = GetComponent<BehaviorTreeRunner>();
    }

    private void Start()
    {
        AnimalBase frog = GetComponent<AnimalBase>();
        if (frog == null)
        {
            Debug.LogError("FrogBT requires AnimalBase on the same GameObject.");
            return;
        }

        BTNode root = BuildFrogTree(frog);
        _runner.SetRoot(root);
    }

    private BTNode BuildFrogTree(AnimalBase frog)
    {
        // 优先级：Flee > Prey > IdleHop > Rest
        //
        // Selector（从左到右尝试，任一成功即成功）
        //  ├─ Sequence: Flee Sequence
        //  │    ├─ IsPlayerDetected?
        //  │    ├─ SetAnimation("Flee")
        //  │    └─ PerformHop(away from player, 1.5x speed)
        //  │
        //  ├─ Sequence: Prey Sequence
        //  │    ├─ IsFoodDetected?
        //  │    ├─ SetAnimation("Prey")
        //  │    └─ PerformHop(toward food, 1.8x speed)
        //  │
        //  └─ Sequence: Idle-Rest Loop
        //       ├─ IsGrounded?
        //       ├─ SetAnimation("Idle")
        //       ├─ WaitAction(1.5~3s)
        //       ├─ PerformHop(random direction, idle speed)
        //       ├─ WaitAction(0.5s)          ← 等落地
        //       ├─ SetAnimation("Rest")
        //       └─ WaitAction(3~6s)          ← 休息

        var root = new Selector(new List<BTNode>
        {
            // 分支1：逃跑
            BuildFleeSequence(frog),
            // 分支2：捕食
            BuildPreySequence(frog),
            // 分支3：Idle → 跳跃 → Rest 循环
            BuildIdleRestSequence(frog)
        });

        return root;
    }

    private BTNode BuildFleeSequence(AnimalBase frog)
    {
        return new Sequence(new List<BTNode>
        {
            new IsPlayerDetectedCondition(frog),
            new SetAnimationAction(frog, "Flee"),
            new PerformHopAction(frog, () =>
            {
                // 朝远离玩家方向
                return frog.IsPlayerDetected ? -Mathf.Sign(frog.PlayerDirection.x) : -1f;
            }, frog.FleeSpeedMultiplier)
        });
    }

    private BTNode BuildPreySequence(AnimalBase frog)
    {
        return new Sequence(new List<BTNode>
        {
            new IsFoodDetectedCondition(frog),
            new SetAnimationAction(frog, "Prey"),
            new PerformHopAction(frog, () =>
            {
                return frog.IsFoodDetected ? Mathf.Sign(frog.FoodDirection.x) : 1f;
            }, 1.8f)
        });
    }

    private BTNode BuildIdleRestSequence(AnimalBase frog)
    {
        var random = new System.Random();

        return new Sequence(new List<BTNode>
        {
            new IsGroundedCondition(frog),
            new SetAnimationAction(frog, "Idle"),
            new WaitAction(Random.Range(frog.PatrolPauseMin, frog.PatrolPauseMax)),
            new PerformHopAction(frog, () => Random.value < 0.5f ? -1f : 1f, 1f),
            new WaitAction(1f),
            new SetAnimationAction(frog, "Rest"),
            new WaitAction(Random.Range(3f, 6f))
        });
    }
}
