using UnityEngine;

/// <summary>
/// BOSS 场地触发器（测试用）：负责调用 BossController.EnterArena() 激活 BOSS 战。
/// 两种模式（Inspector 勾选切换，可随时替换）：
/// - 自动挡（autoEnterOnStart=true）：进场景后延迟 enterDelay 秒自动激活，一进场景就开打，最省事；
/// - 触发区（autoEnterOnStart=false）：玩家走进触发区才激活，更接近正式玩法。
///   需本组件挂 BoxCollider2D(isTrigger) 且玩家挂 Rigidbody2D+Collider2D。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossArenaTrigger : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("要激活的 BOSS。留空则自动 FindObjectOfType<BossController>")]
    [SerializeField] private BossController _boss;

    [Header("触发方式")]
    [Tooltip("勾选=进场景自动激活（测试省事）；取消勾选=玩家进触发区激活")]
    [SerializeField] private bool _autoEnterOnStart = true;

    [Tooltip("自动激活延迟（秒），仅自动挡生效")]
    [SerializeField] private float _enterDelay = 0.5f;

    private bool _triggered;

    private void Awake()
    {
        if (_boss == null)
            _boss = FindObjectOfType<BossController>();

        // 触发区模式要求触发器；自动挡无需碰撞但保留也无妨
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        if (_autoEnterOnStart)
            Invoke(nameof(TriggerArena), _enterDelay);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_autoEnterOnStart) return; // 自动挡下忽略触发区

        if (other.CompareTag("Player"))
            TriggerArena();
    }

    private void TriggerArena()
    {
        if (_triggered || _boss == null) return;
        _triggered = true;
        _boss.EnterArena();
    }
}
