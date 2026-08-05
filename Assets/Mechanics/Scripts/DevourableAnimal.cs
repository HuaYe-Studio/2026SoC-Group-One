using UnityEngine;

public class DevourableAnimal : MonoBehaviour, IDevourable
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Spit")]
    [Tooltip("被吐出后的眩晕时长（秒），期间 AI 不感知不行动，防止受惊蹿出")]
    [SerializeField] private float stunDurationAfterSpit = 2.5f;

    [Header("Devour Config")]
    [SerializeField] private float priority = 0f;
    [SerializeField] private bool destroyAfterDevour = false;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

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

    public void PlayBeingDevoured()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;

        if (_animalBase != null)
            _animalBase.OnDevoured(stunDurationAfterSpit);

<<<<<<< HEAD
=======
        // 广播"同类被吞噬"事件，供复仇机制感知（如冲冲羊）
>>>>>>> 9a51393 (feat: 完成蜘蛛和羊行为树的构建)
        MockEventCenter.TriggerAnimalDevoured(gameObject);

        SafeSetTrigger("Devoured");
    }

    public void PlayBeingSpitOut(Vector2 direction)
    {
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }
        transform.localScale = Vector3.one;

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
