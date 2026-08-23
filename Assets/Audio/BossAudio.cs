using UnityEngine;

/// <summary>
/// BOSS 全局音效：监听 BOSS 战事件，播放登场低吼 / 狂暴 / 退场音效。
/// 挂在 BOSS 物体（BossController 同一物体）上。
///
/// 攻击类音效（撕咬/横扫/拍击）在各自 BossXxxAttack.Execute 里触发，不在此组件处理。
/// </summary>
public class BossAudio : MonoBehaviour
{
    [Header("BOSS 音效 key（对应 AudioLibrary 的 sfxEntries）")]
    [Tooltip("登场低吼（EnterArena 时）")]
    [SerializeField] private string enterSfxKey = "boss_enter";
    [Tooltip("狂暴（进入 Enrage1/2/3 时）")]
    [SerializeField] private string enrageSfxKey = "boss_enrage";
    [Tooltip("退场/被击败（Defeated 时）")]
    [SerializeField] private string defeatedSfxKey = "boss_defeated";

    private BossController _boss;
    private BossPhase _lastPhase = BossPhase.Normal;
    private bool _wasActive;

    private void Awake()
    {
        _boss = GetComponent<BossController>();
    }

    private void OnEnable()
    {
        MockEventCenter.OnBossPhaseChanged += HandlePhaseChanged;
        MockEventCenter.OnBossDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        MockEventCenter.OnBossPhaseChanged -= HandlePhaseChanged;
        MockEventCenter.OnBossDefeated -= HandleDefeated;
    }

    private void Update()
    {
        // 检测 BOSS 从「未激活」变为「激活」（EnterArena），播登场低吼
        if (_boss == null) return;

        bool active = _boss.IsActive;
        if (active && !_wasActive)
        {
            PlaySfx(enterSfxKey);
        }
        _wasActive = active;
    }

    private void HandlePhaseChanged(BossPhase phase)
    {
        // 进入狂暴（Enrage1/2/3）播狂暴音效
        if (phase == BossPhase.Enrage1 || phase == BossPhase.Enrage2 || phase == BossPhase.Enrage3)
        {
            PlaySfx(enrageSfxKey);
        }

        _lastPhase = phase;
    }

    private void HandleDefeated()
    {
        PlaySfx(defeatedSfxKey);
    }

    private void PlaySfx(string key)
    {
        if (AudioManager.HasInstance && !string.IsNullOrEmpty(key))
            AudioManager.Instance.PlaySfxByKey(key);
    }
}
