using UnityEngine;

/// <summary>
/// 动物受伤反馈组件：参考 RevengeBehavior 的"通用组件"模式，挂上即获得受伤反馈能力。
/// 监听 MockEventCenter.OnAnimalAttacked（由 IHazardSource 的接触伤害源触发），
/// 当自己受伤时进入"受伤反馈"状态，供行为树高优先级分支读取并执行弹跳 + 位移。
/// 职责边界：本组件维护状态（IsHurting / HurtDirection / 无敌 / 生命值 / 伤害源记忆 + 无敌闪烁），
/// 实际的弹跳/位移动作由 BTHurtFeedbackAction 行为树节点执行（保持"感知-决策-动作"分层）。
/// </summary>
public class AnimalHurtFeedback : MonoBehaviour
{
    [Header("受伤反馈")]
    [Tooltip("无敌时长（秒）：受伤瞬间生效，期间重复触碰伤害源不触发")]
    [SerializeField] private float _invincibleDuration = 1f;

    [Tooltip("受伤弹跳高度（米）：受伤瞬间向上弹跳的高度")]
    [SerializeField] private float _hopHeight = 1f;

    [Tooltip("受伤横向逃离速度（米/秒）：受伤瞬间水平逃离的速度，应明显大于普通移动速度以瞬间脱离伤害源")]
    [SerializeField] private float _hurtFleeSpeed = 6f;

    [Tooltip("无敌闪烁间隔（秒）：无敌期间在「完全不透明↔半透明红色」之间切换的间隔")]
    [SerializeField] private float _blinkInterval = 0.1f;

    [Tooltip("无敌闪烁最低透明度（0~1）：半透明态的目标透明度，保留可见、比整只消失更明显")]
    [SerializeField, Range(0.05f, 1f)] private float _blinkMinAlpha = 0.2f;

    [Tooltip("闪烁红色强度（0~1）：叠加到精灵上的红色强度，让受伤无敌更醒目")]
    [SerializeField, Range(0f, 1f)] private float _blinkRedIntensity = 0.7f;

    [Tooltip("最大生命值：被普通伤害源命中扣减，归零即死亡（SetActive(false)）。0=不启用血量、永不因普通伤害死亡")]
    [SerializeField] private int _maxHealth = 3;

    private int _currentHealth;

    private AnimalBase _animal;
    private Blackboard _bb;
    private SpriteRenderer _sprite;
    private Color _baseColor = Color.white;
    private float _nextBlinkTime;
    private bool _blinkVisible = true;
    private bool _visualDirty;   // 精灵当前是否处于染色态（无敌一结束即恢复，与停在哪个闪烁相位无关）

    /// <summary>是否处于受伤反馈中（弹跳/位移进行时）。行为树高优先级分支据此抢占。</summary>
    public bool IsHurting { get; private set; }

    /// <summary>受伤横向位移方向（±1，远离伤害源）。</summary>
    public float HurtDirection { get; private set; } = 1f;

    /// <summary>受伤弹跳高度（米）。</summary>
    public float HopHeight => _hopHeight;

    /// <summary>受伤横向逃离速度（米/秒）。</summary>
    public float HurtFleeSpeed => _hurtFleeSpeed;

    private void Awake()
    {
        _animal = GetComponent<AnimalBase>();
        if (_animal != null)
        {
            _bb = _animal.Board;
            _sprite = _animal.SpriteRenderer;
        }
        if (_sprite == null)
            _sprite = GetComponent<SpriteRenderer>();
        if (_sprite != null)
            _baseColor = _sprite.color;
    }

    private void OnEnable()
    {
        MockEventCenter.OnAnimalAttacked += HandleAttacked;
        _currentHealth = _maxHealth;
    }

    private void OnDisable()
    {
        MockEventCenter.OnAnimalAttacked -= HandleAttacked;
        RestoreVisual();
    }

    private void Update()
    {
        if (_bb == null)
            return;

        if (_bb.IsInvincible)
        {
            // 无敌期间闪烁：在「完全不透明(偏红) ↔ 半透明(偏红)」之间切换，保留可见、更醒目
            if (Time.time >= _nextBlinkTime)
            {
                _nextBlinkTime = Time.time + _blinkInterval;
                _blinkVisible = !_blinkVisible;
                ApplyBlinkVisual(_blinkVisible);
            }
        }
        else if (_visualDirty)
        {
            // 无敌结束：无论停在哪个闪烁相位都恢复原色（此前判 !_blinkVisible 有 50% 概率永久残留红色）
            RestoreVisual();
        }
    }

    /// <summary>应用闪烁视觉：偏红 + 按 visible 切换透明度（半透明闪烁，而非整只消失）。</summary>
    private void ApplyBlinkVisual(bool visible)
    {
        if (_sprite == null)
            return;

        Color c = Color.Lerp(_baseColor, Color.red, _blinkRedIntensity);
        c.a = visible ? 1f : _blinkMinAlpha;
        _sprite.color = c;
        _visualDirty = true;
    }

    /// <summary>恢复精灵原始颜色（无敌结束或组件禁用时调用，避免残留半透明/红色）。</summary>
    private void RestoreVisual()
    {
        _blinkVisible = true;
        _visualDirty = false;
        if (_sprite != null)
            _sprite.color = _baseColor;
    }

    /// <summary>
    /// 受击回调：只处理自己受伤。无敌中忽略；直接死亡源触发即死（B6 接口，暂不出现）。
    /// </summary>
    private void HandleAttacked(GameObject victim, GameObject attacker, float damage)
    {
        if (_animal == null || _bb == null)
            return;

        if (victim != _animal.gameObject)
            return;

        // 无敌中：重复触碰不触发
        if (_bb.IsInvincible)
            return;

        // 只响应"伤害源"（IHazardSource：尖刺等接触伤害体）的攻击。
        // 吞噬不是伤害行为：DevourableAnimal 也复用 OnAnimalAttacked 事件（attacker=玩家，无 IHazardSource），
        // 吞噬有独立的吸入/动画表现，不应走扣血/弹跳/闪烁，也不应记录伤害源位置污染觅食回避。
        // （复仇 RevengeBehavior / 恐惧扩散 FearSpreader 对吞噬事件的响应不受影响，由它们各自消费。）
        IHazardSource hazard = attacker != null ? attacker.GetComponent<IHazardSource>() : null;
        if (hazard == null)
            return;

        // 直接死亡源：即死（SetActive(false)，场景重载自然回归原位）
        if (hazard.IsInstantKill)
        {
            gameObject.SetActive(false);
            return;
        }

        TriggerHurt(attacker, damage);
    }

    /// <summary>
    /// 进入受伤反馈：扣血、记录伤害源位置（软→硬递进回避用）、记录位移方向并立即进入无敌。
    /// </summary>
    private void TriggerHurt(GameObject attacker, float damage)
    {
        // 扣血（普通伤害源），归零即死
        if (_maxHealth > 0)
        {
            _currentHealth -= Mathf.Max(1, Mathf.RoundToInt(damage));
            if (_currentHealth <= 0)
            {
                Die();
                return;
            }
        }

        // 记录伤害源位置记忆（供觅食/巡游回避，软→硬递进）
        Vector2 hazardPos = attacker != null ? (Vector2)attacker.transform.position : (Vector2)transform.position;
        _bb.RememberHazard(hazardPos);

        IsHurting = true;

        if (attacker != null)
        {
            float dx = transform.position.x - attacker.transform.position.x;
            HurtDirection = dx >= 0f ? 1f : -1f;
        }
        else
        {
            // 无来源兜底：默认朝右
            HurtDirection = 1f;
        }

        _bb.InvincibleUntilTime = Time.time + _invincibleDuration;

        // 立即进入半透明红色闪烁态，下一帧 Update 开始交替闪烁
        _blinkVisible = false;
        _nextBlinkTime = Time.time + _blinkInterval;
        ApplyBlinkVisual(false);
    }

    /// <summary>死亡：隐藏本体（SetActive(false)），场景重载自然回归原位，不销毁资源。</summary>
    private void Die()
    {
        IsHurting = false;
        gameObject.SetActive(false);
    }

    /// <summary>结束受伤反馈（位移完成后由行为树节点调用）。</summary>
    public void EndHurt()
    {
        IsHurting = false;
    }
}
