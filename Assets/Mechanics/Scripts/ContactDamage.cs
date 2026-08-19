using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 接触伤害源：实现 IHazardSource 的通用伤害体（尖刺、火焰喷射器等）。
/// 命中目标层后，对玩家直接扣血 + 击退；对动物触发 MockEventCenter.OnAnimalAttacked 事件
/// （由 AnimalHurtFeedback / RevengeBehavior / FearSpreader 消费）+ 击退。
/// isInstantKill 为 true 时是"直接死亡源"，即死逻辑由消费方（AnimalHurtFeedback）处理。
/// 同时支持 solid（OnCollisionEnter2D）与 trigger（OnTriggerEnter2D/Stay2D）两种碰撞体配置：
/// trigger 伤害区（如 TM_Spikes 的 CompositeCollider2D trigger）停留时按 repeatInterval 节流持续生效，
/// 重复扣血的节流由消费方负责（动物 AnimalHurtFeedback 无敌期、玩家 PlayerHP.IsInvincible）。
/// </summary>
public class ContactDamage : MonoBehaviour, IHazardSource
{
    [Header("伤害源")]
    [Tooltip("是否直接死亡源（触碰即死）。为 true 时 damage 不生效，由消费方处理即死")]
    [SerializeField] private bool isInstantKill = false;

    [Tooltip("普通伤害值（非即死源时生效）")]
    [SerializeField] private int damage = 1;

    [Tooltip("击退力（x=水平，y=垂直）")]
    [SerializeField] private Vector2 knockback = new Vector2(8f, 4f);

    [Tooltip("生效目标层。0=所有层；否则仅命中勾选的层")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("同一目标重复触发间隔（秒）：停留在伤害区内持续生效的节流，避免每物理帧重复扣血/击退")]
    [SerializeField] private float repeatInterval = 0.5f;

    // 每目标上次触发时间（进入/停留统一节流；条目数=接触过的目标数，规模小）
    private readonly Dictionary<int, float> _lastHitTime = new Dictionary<int, float>();

    // ---- IHazardSource ----
    public bool IsInstantKill => isInstantKill;
    public int Damage => damage;
    public Vector2 Knockback => knockback;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleContact(other.gameObject);
    }

    private void HandleContact(GameObject target)
    {
        if (!IsTargetLayer(target.layer)) return;

        // 每目标节流：Enter/Stay 统一走此处，站在伤害区内不会每物理帧重复触发
        int id = target.GetInstanceID();
        if (_lastHitTime.TryGetValue(id, out float last) && Time.time - last < repeatInterval)
            return;
        _lastHitTime[id] = Time.time;

        // 1. 玩家：直接扣血 + 击退（即死逻辑由 PlayerHP 体系另行处理，这里保持原行为）
        PlayerHP hp = target.GetComponentInParent<PlayerHP>();
        if (hp != null)
        {
            hp.TakeDamage(damage);
            if (!hp.IsDead)
                ApplyKnockback(target);
            return;
        }

        // 2. 动物：触发受击事件（victim 传 AnimalBase 所在对象，保证 RevengeBehavior 等能正确匹配 self）+ 击退
        AnimalBase animal = target.GetComponentInParent<AnimalBase>();
        if (animal != null)
        {
            MockEventCenter.TriggerAnimalAttacked(animal.gameObject, gameObject, damage);
            ApplyKnockback(animal.gameObject);
        }
    }

    private bool IsTargetLayer(int layer)
    {
        return targetLayer.value == 0 || (targetLayer.value & (1 << layer)) != 0;
    }

    private void ApplyKnockback(GameObject target)
    {
        Rigidbody2D rb = target.GetComponentInParent<Rigidbody2D>();
        if (rb == null) return;
        float dir = Mathf.Sign(rb.transform.position.x - transform.position.x);
        if (dir == 0f) dir = 1f;
        rb.velocity = new Vector2(knockback.x * dir, knockback.y);
    }
}