using UnityEngine;

public class DevourableAnimal : MonoBehaviour, IDevourable
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Devour Config")]
    [SerializeField] private float priority;
    [SerializeField] private bool destroyAfterDevour;

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
