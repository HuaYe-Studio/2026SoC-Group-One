using UnityEngine;

public class DevourableAnimal : MonoBehaviour, IDevourable
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Spit")]
    [Tooltip("被吐出后的眩晕时长（秒），期间 AI 不感知不行动，防止受惊蹿出")]
    [SerializeField] private float stunDurationAfterSpit = 2.5f;

    [Header("Devour Config")]
    [SerializeField] private float priority;
    [SerializeField] private bool destroyAfterDevour;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

    /// <summary>
    /// 吞噬者（玩家）的 GameObject。吞噬总是由玩家执行，通过 Tag 查找。
    /// 找不到时返回 null（攻击者未知，复仇组件会忽略）。
    /// </summary>
    private static GameObject PlayerAttacker
    {
        get { return GameObject.FindGameObjectWithTag("Player"); }
    }

    private Rigidbody2D _rb;
    private Animator _animator;
    private AnimalBase _animalBase;

    // IDevourable
    Transform IDevourable.Transform => transform;
    float IDevourable.Priority => priority;
    bool IDevourable.DestroyAfterDevour => destroyAfterDevour;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _animalBase = GetComponent<AnimalBase>();
    }

    bool IDevourable.CanBeDevoured(PlayerController playerController)
    {
        return !playerController.IsFormUnlocked(grantedForm);
    }

    void IDevourable.OnBeingDevoured()
    {
        PlayBeingDevoured();
    }

    void IDevourable.ExecuteDevourOutcome(PlayerController playerController)
    {
        bool isNewForm = !playerController.IsFormUnlocked(grantedForm);
        if (isNewForm)
        {
            MockEventCenter.TriggerFormUnlock(grantedForm);
            playerController.SwitchToFormByType(grantedForm);
        }
    }

    void IDevourable.OnBeingSpitOut(Vector2 direction)
    {
        PlayBeingSpitOut(direction);
    }

    private void PlayBeingDevoured()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;

        if (_animalBase != null)
            _animalBase.OnDevoured(stunDurationAfterSpit);

        // 广播"受击"事件（攻击者=玩家）：供通用复仇机制感知（如冲冲羊被/同类被吞噬后反击）
        MockEventCenter.TriggerAnimalAttacked(gameObject, PlayerAttacker, 0f);

        SafeSetTrigger("Devoured");
    }

    private void PlayBeingSpitOut(Vector2 direction)
    {
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = Color.white;
        }
        transform.localScale = Vector3.one;

        if (_rb != null)
            _rb.velocity = Vector2.zero;

        SafeSetTrigger("SpitOut");
    }

    private void SafeSetTrigger(string triggerName)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.SetTrigger(triggerName);
                return;
            }
        }
    }
}
