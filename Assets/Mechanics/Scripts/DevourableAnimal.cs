using UnityEngine;

public class DevourableAnimal : MonoBehaviour
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Spit")]
    [Tooltip("被吐出后的眩晕时长（秒），期间 AI 不感知不行动，防止受惊蹿出")]
    [SerializeField] private float stunDurationAfterSpit = 2.5f;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

    private Rigidbody2D _rb;
    private Animator _animator;
    private AnimalBase _animalBase;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _animalBase = GetComponent<AnimalBase>();
    }

    public void PlayBeingDevoured()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;

        // 通知 AI：清空威胁认知并进入眩晕
        // 眩晕计时从时间恢复流动后起算（吞噬演出期间 Time.timeScale=0，Time.time 冻结）
        if (_animalBase != null)
            _animalBase.OnDevoured(stunDurationAfterSpit);

        // 广播"同类被吞噬"事件，供复仇机制感知（如冲冲羊）
        MockEventCenter.TriggerAnimalDevoured(gameObject);

        SafeSetTrigger("Devoured");
    }

    public void PlayBeingSpitOut(Vector2 direction)
    {
        // 恢复被吞噬效果改变的视觉状态
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }
        transform.localScale = Vector3.one;

        // 吐出时不再施加冲量：动物处于眩晕状态，原地落下即可，
        // 避免"交换位置后突然蹿出去"的观感问题
        if (_rb != null)
            _rb.velocity = Vector2.zero;

        SafeSetTrigger("SpitOut");
    }

    private void SafeSetTrigger(string name)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.name == name && param.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.SetTrigger(name);
                return;
            }
        }
    }
}
