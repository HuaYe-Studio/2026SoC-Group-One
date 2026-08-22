using UnityEngine;
using UnityEngine.Events;

public class DevourableObject : MonoBehaviour, IDevourable
{
    [Header("Devour Config")]
    [SerializeField] private bool devourableOnce = true;
    [SerializeField] private float priority;

    [Header("Effect")]
    [SerializeField] private UnityEvent onDevourEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip devourSound;

    protected bool hasBeenDevoured;

    // IDevourable
    Transform IDevourable.Transform => transform;
    public SpriteRenderer SpriteRenderer { get; private set; }
    public bool IsTargeted { get; set; }

    float IDevourable.Priority => GetPriority();
    bool IDevourable.DestroyAfterDevour => GetDestroyAfterDevour();

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
    }

    bool IDevourable.CanBeDevoured(PlayerController pc) => CanBeDevouredOverride(pc);

    void IDevourable.OnBeingDevoured() => OnBeingDevouredOverride();

    void IDevourable.ExecuteDevourOutcome(PlayerController pc) => ExecuteDevourOutcomeOverride(pc);

    void IDevourable.OnBeingSpitOut(Vector2 direction) => OnBeingSpitOutOverride(direction);

    // ── protected virtual hooks for subclass overrides ──

    protected virtual float GetPriority() => priority;

    protected virtual bool GetDestroyAfterDevour() => true;

    protected virtual bool CanBeDevouredOverride(PlayerController pc)
    {
        return !(devourableOnce && hasBeenDevoured);
    }

    protected virtual void OnBeingDevouredOverride()
    {
        if (devourSound != null)
            AudioManager.Instance?.PlaySfx(devourSound);
    }

    protected virtual void ExecuteDevourOutcomeOverride(PlayerController pc)
    {
        hasBeenDevoured = true;
        onDevourEffect?.Invoke();
    }

    protected virtual void OnBeingSpitOutOverride(Vector2 direction)
    {
        // one-shot objects self-destruct via destroyAfterDevour; no restore needed
    }

    protected void ResetDevoured()
    {
        hasBeenDevoured = false;
        IsTargeted = false;
    }
}
