using UnityEngine;

/// <summary>
/// 通用复仇组件：任何动物挂上此组件即可获得"受击报复"能力。
/// 触发条件（任一满足即进入复仇状态，目标 = 攻击者）：
///   1. 自己受到攻击（任何来源：玩家吞噬、敌方碰撞、陷阱等）→ 全额复仇时长
///   2. 感知范围内有同类（Tag 相同）受到攻击 → 复仇时长按距离衰减（近者同仇敌忾）
/// 复仇状态持续 _revengeDuration（×时长系数）后自动解除；复仇目标被销毁时立即解除。
/// 攻击来源抽象：统一监听 MockEventCenter.OnAnimalAttacked(victim, attacker, damage)，
/// 因此攻击者既可以是玩家，也可以是敌方 NPC（实现"借刀杀人"：敌方误伤羊 → 羊反打敌方）。
/// 行为树通过 IsRevenge / RevengeTarget 读取复仇状态与目标。
/// </summary>
public class RevengeBehavior : MonoBehaviour
{
    [Header("Revenge")]
    [Tooltip("同类受击的感知范围（米）：范围内同类被攻击才会触发复仇")]
    [SerializeField] private float _senseRadius = 10f;

    [Tooltip("复仇状态持续时长（秒）：超时后解除复仇")]
    [SerializeField] private float _revengeDuration = 6f;

    [Tooltip("同类 Tag：受害者的 Tag 与此相同视为同类。留空 = 只对自己受击做出反应")]
    [SerializeField] private string _kinTag = "Animal";

    [Tooltip("同类受击的复仇时长距离衰减下限：范围边缘的同类受击，复仇时长 = 全额 × 此系数（自己受击恒为全额）")]
    [SerializeField, Range(0.1f, 1f)] private float _minKinDurationScale = 0.4f;

    /// <summary>当前是否处于复仇状态。</summary>
    public bool IsRevenge { get; private set; }

    /// <summary>复仇目标（攻击者）。攻击者已销毁/离开时可能为 null，行为树应做空判断。</summary>
    public GameObject RevengeTarget { get; private set; }

    private float _revengeUntil;

    private void OnEnable()
    {
        MockEventCenter.OnAnimalAttacked += HandleAnimalAttacked;
    }

    private void OnDisable()
    {
        MockEventCenter.OnAnimalAttacked -= HandleAnimalAttacked;
    }

    /// <summary>
    /// 受击事件回调：判断是否与自己相关（自己受击 / 范围内同类受击），
    /// 命中则把攻击者设为复仇目标。
    /// 自己受击 → 全额复仇时长；同类受击 → 时长按距离衰减（越远的同类受害，复仇越短暂），
    /// 模拟"离得近才同仇敌忾"的群体仇恨强度。
    /// </summary>
    private void HandleAnimalAttacked(GameObject victim, GameObject attacker, float damage)
    {
        if (attacker == null)
            return;

        bool isSelf = victim == gameObject;
        if (isSelf)
        {
            EnterRevenge(attacker);
            return;
        }

        if (!IsKin(victim))
            return;

        float distance = Vector2.Distance(transform.position, victim.transform.position);
        if (distance > _senseRadius)
            return;

        // 距离衰减：0m 处全额，_senseRadius 处衰减到下限系数
        float durationScale = Mathf.Lerp(1f, _minKinDurationScale, distance / _senseRadius);
        EnterRevenge(attacker, durationScale);
    }

    /// <summary>
    /// 判断受害者是否与本组件同类（Tag 相同）。
    /// _kinTag 为空时仅自身受击触发，不响应任何同类。
    /// </summary>
    private bool IsKin(GameObject other)
    {
        if (other == null || string.IsNullOrEmpty(_kinTag))
            return false;
        return other.CompareTag(_kinTag);
    }

    /// <summary>
    /// 进入复仇状态：锁定攻击者为复仇目标。
    /// </summary>
    /// <param name="attacker">攻击者（复仇目标）</param>
    /// <param name="durationScale">时长系数（同类受击的距离衰减用；自己受击传 1）</param>
    public void EnterRevenge(GameObject attacker, float durationScale = 1f)
    {
        RevengeTarget = attacker;
        IsRevenge = true;
        _revengeUntil = Time.time + _revengeDuration * Mathf.Clamp01(durationScale);
    }

    private void Update()
    {
        // 复仇目标已销毁（fake null）→ 立即解除，避免行为树对空目标空转
        if (IsRevenge && RevengeTarget == null)
        {
            ClearRevenge();
            return;
        }

        // 复仇超时 → 解除
        if (IsRevenge && Time.time >= _revengeUntil)
            ClearRevenge();
    }

    /// <summary>清除复仇状态（超时或外部主动解除）。</summary>
    public void ClearRevenge()
    {
        IsRevenge = false;
        RevengeTarget = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _senseRadius);
    }
#endif
}
