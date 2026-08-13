using UnityEngine;

/// <summary>
/// [群体] 恐惧接收者接口（跨物种传播口子）：可接收 FearSpreader 传播来的恐惧。
/// 同物种动物由 FearSpreader 自身实现本接口（可续传，跳数 -1）；
/// 其他物种（如岸边蜘蛛被鱼群惊动）实现本接口即可接收恐惧——
/// 无需挂载 FearSpreader，且跨物种接收时跳数恒为 0（不续传），恐惧量由传播源按系数衰减。
/// </summary>
public interface IFearReceiver
{
    /// <summary>
    /// 接收传播来的恐惧。
    /// </summary>
    /// <param name="sourceKnownPlayerPos">传播源对威胁的认知位置（用于写入威胁记忆）</param>
    /// <param name="sourceKnowsPlayer">传播源是否确知威胁位置（false 时只抬威胁、不写记忆）</param>
    /// <param name="amount">威胁抬升量（跨物种传播已被传播源按系数衰减）</param>
    /// <param name="hops">剩余可续传跳数：同类续传时 &gt;0；跨物种恒为 0（不续传）</param>
    void ReceiveFear(Vector2 sourceKnownPlayerPos, bool sourceKnowsPlayer, float amount, int hops);
}
