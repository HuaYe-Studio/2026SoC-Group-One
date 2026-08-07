using UnityEngine;
using UnityEngine.Events;

public class DevourableObject : MonoBehaviour, IDevourable
{
    [Header("Devour Config")]
    [SerializeField] private bool devourableOnce = true;
    [SerializeField] private float priority;
    [SerializeField] private bool destroyAfterDevour = true;

    [Header("Effect")]
    [SerializeField] private UnityEvent onDevourEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip devourSound;

    private bool _hasBeenDevoured;

    // IDevourable
    Transform IDevourable.Transform => transform;
    public SpriteRenderer SpriteRenderer { get; private set; }
    public bool IsTargeted { get; set; }
    float IDevourable.Priority => priority;
    bool IDevourable.DestroyAfterDevour => destroyAfterDevour;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
    }

    bool IDevourable.CanBeDevoured(PlayerController _)
    {
        return !(devourableOnce && _hasBeenDevoured);
    }

    void IDevourable.OnBeingDevoured()
    {
        if (devourSound != null)
            AudioManager.Instance?.PlaySfx(devourSound);
    }

    void IDevourable.ExecuteDevourOutcome(PlayerController _)
    {
        _hasBeenDevoured = true;
        onDevourEffect?.Invoke();
    }

    void IDevourable.OnBeingSpitOut(Vector2 direction)
    {
        // one-shot objects self-destruct via destroyAfterDevour; no restore needed
    }

    public void ResetDevoured()
    {
        _hasBeenDevoured = false;
        IsTargeted = false;
    }
}
