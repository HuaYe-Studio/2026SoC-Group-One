using UnityEngine;

public class DevourableAnimal : MonoBehaviour, IDevourable
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Devour Config")]
    [SerializeField] private float priority;
    [SerializeField] private bool destroyAfterDevour;

    [Header("Audio")]
    [SerializeField] private AudioClip devourSound;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

    private bool _isInDevourSequence;
    private bool _wasFirstDevour;

    public bool WasFirstDevour => _wasFirstDevour;
    public bool IsInDevourSequence
    {
        get => _isInDevourSequence;
        set => _isInDevourSequence = value;
    }

    private static GameObject PlayerAttacker =>
        GameObject.FindGameObjectWithTag("Player");

    private Rigidbody2D _rb;
    private Animator _animator;
    private AnimalBase _animalBase;

    // IDevourable
    Transform IDevourable.Transform => transform;
    float IDevourable.Priority => GetPriority();
    bool IDevourable.DestroyAfterDevour => GetDestroyAfterDevour();

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _animalBase = GetComponent<AnimalBase>();
    }

    private void OnEnable()
    {
        // Defensive self-heal: if a level culling system disables this animal while
        // _isInDevourSequence is stuck (e.g. disabled mid-devour before the
        // DecelerateAndStun coroutine could clear it), the flag would stay true forever
        // and the animal would become permanently undevourable after re-enable.
        // Re-enabling always resets the flag so devour works again.
        _isInDevourSequence = false;
    }

    private void OnDisable()
    {
        // Symmetric with the OnEnable self-heal: disabling this animal mid-devour kills
        // the DecelerateAndStun coroutine (the only normal clearer of the flag), which used
        // to leave _isInDevourSequence stuck true until a scene reload. Clear it on disable
        // so the animal is devourable again the moment it is re-enabled.
        _isInDevourSequence = false;
        IsTargeted = false;
    }

    bool IDevourable.CanBeDevoured(PlayerController pc) => CanBeDevouredOverride(pc);

    void IDevourable.OnBeingDevoured() => OnBeingDevouredOverride();

    void IDevourable.ExecuteDevourOutcome(PlayerController pc) => ExecuteDevourOutcomeOverride(pc);

    void IDevourable.OnBeingSpitOut(Vector2 direction) => OnBeingSpitOutOverride(direction);

    // ── protected virtual hooks for subclass overrides ──

    protected virtual float GetPriority() => priority;

    protected virtual bool GetDestroyAfterDevour() => destroyAfterDevour;

    protected virtual bool CanBeDevouredOverride(PlayerController pc)
    {
        if (_isInDevourSequence) return false;
        return true;
    }

    protected virtual void OnBeingDevouredOverride()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;

        if (devourSound != null)
            AudioManager.Instance?.PlaySfx(devourSound);

        MockEventCenter.TriggerAnimalAttacked(gameObject, PlayerAttacker, 0f);

        _isInDevourSequence = true;
        SafeSetTrigger("Devoured");
    }

    public void LaunchAndStun(Vector2 direction, float speed)
    {
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.velocity = direction * speed;
        }
        StartCoroutine(DecelerateAndStun());
    }

    private System.Collections.IEnumerator DecelerateAndStun()
    {
        yield return new WaitForSeconds(0.15f);
        if (_rb != null)
            _rb.velocity *= 0.15f;
        _animalBase?.OnDevoured(0.5f);
        yield return new WaitForSeconds(0.5f);
        _isInDevourSequence = false;
    }

    protected virtual void ExecuteDevourOutcomeOverride(PlayerController pc)
    {
        if (!pc.IsFormUnlocked(grantedForm))
        {
            MockEventCenter.TriggerFormUnlock(grantedForm);
            _wasFirstDevour = true;
        }
    }

    protected virtual void OnBeingSpitOutOverride(Vector2 direction)
    {
        if (SpriteRenderer != null)
            SpriteRenderer.color = Color.white;

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
