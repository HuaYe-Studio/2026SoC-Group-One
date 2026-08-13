using UnityEngine;

/// <summary>
/// [BT] 眩晕节点：黑板驱动的原地僵直，直到 Board.StunUntilTime 到期。
/// 被吞噬/受击后由外部（DevourableAnimal → AnimalBase.OnDevoured）写入眩晕时间，
/// 本节点只负责在眩晕期间钉住动物、播放眩晕动画。
/// 注意：Reset() 只重置动画标记，不调用物理副作用。
/// </summary>
public class BTStunnedAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;

    private bool _isPlayingAnim;

    /// <param name="animal">动物实例</param>
    public BTStunnedAction(AnimalBase animal)
    {
        _animal = animal;
        _bb = animal.Board;
    }

    public override State Tick()
    {
        // 眩晕结束
        if (!_bb.IsStunned)
        {
            _isPlayingAnim = false;
            return State.Success;
        }

        // 首次进入：播放眩晕动画
        if (!_isPlayingAnim)
        {
            _animal.PlayAnimation(AnimalAnimNames.Stunned);
            _isPlayingAnim = true;
        }

        // 眩晕期间持续钉住水平速度，防止残留冲量或被击退漂移
        _animal.StopMoving();
        return State.Running;
    }

    public override void Reset()
    {
        _isPlayingAnim = false;
    }
}
