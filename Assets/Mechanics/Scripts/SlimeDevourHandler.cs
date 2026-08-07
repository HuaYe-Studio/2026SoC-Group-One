using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SlimeForm))]
public class SlimeDevourHandler : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 4f;
    [SerializeField]
    [FormerlySerializedAs("animalLayer")]
    private LayerMask devourableLayer;

    [Header("Pounce")]
    [SerializeField] private float pounceSpeed = 14f;
    [SerializeField] private float pounceMaxDuration = 0.6f;
    [SerializeField] private float cooldownSeconds = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip pounceClip;
    [SerializeField] private AudioClip devourClip;
    [SerializeField] private AudioClip spitClip;

    private SlimeForm slimeForm;
    private BaseForm baseForm;
    private Rigidbody2D rb;
    private PlayerController playerController;
    private DevourEffectPlayer effectPlayer;

    private IDevourable _currentTarget;
    private bool _isPouncing;
    public bool IsPouncing => _isPouncing;
    private bool _devourSequenceRunning;
    private float _pounceEndTime;
    private float _cooldownEndTime;

    private HashSet<MonoBehaviour> _devourablesInRange = new HashSet<MonoBehaviour>();
    private List<MonoBehaviour> _rangeCheckResults = new List<MonoBehaviour>();
    private Collider2D[] _overlapBuffer = new Collider2D[32];

    private static readonly int IsSwoopingHash = Animator.StringToHash("IsSwooping");

    private void Awake()
    {
        slimeForm = GetComponent<SlimeForm>();
        baseForm = slimeForm;
        rb = GetComponentInParent<Rigidbody2D>();
        playerController = GetComponentInParent<PlayerController>();
    }

    private void Start()
    {
        effectPlayer = Camera.main?.GetComponent<DevourEffectPlayer>();
        if (effectPlayer == null)
            Debug.LogWarning("SlimeDevourHandler: No DevourEffectPlayer on Main Camera.");
    }

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space += TryHandleDevourInput;
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space -= TryHandleDevourInput;

        foreach (MonoBehaviour mb in _devourablesInRange)
        {
            IDevourable d = mb as IDevourable;
            if (d != null)
                MockEventCenter.TriggerDevourableExitRange(d);
        }
        _devourablesInRange.Clear();

        StopAllCoroutines();
        _isPouncing = false;
        _devourSequenceRunning = false;
        _currentTarget = null;
        Time.timeScale = 1f;
        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;
    }

    private void Update()
    {
        UpdateDevourablesInRange();
    }

    private void UpdateDevourablesInRange()
    {
        Vector2 origin = transform.root.position;
        int count = Physics2D.OverlapCircleNonAlloc(origin, detectionRadius, _overlapBuffer, devourableLayer);

        _rangeCheckResults.Clear();
        for (int i = 0; i < count; i++)
        {
            IDevourable devourable = GetDevourable(_overlapBuffer[i]);
            if (devourable != null && devourable.CanBeDevoured(playerController))
                _rangeCheckResults.Add((MonoBehaviour)devourable);
        }

        foreach (MonoBehaviour mb in _devourablesInRange)
        {
            if (!_rangeCheckResults.Contains(mb))
            {
                IDevourable d = mb as IDevourable;
                if (d != null)
                    MockEventCenter.TriggerDevourableExitRange(d);
            }
        }
        _devourablesInRange.RemoveWhere(mb => !_rangeCheckResults.Contains(mb));

        foreach (MonoBehaviour mb in _rangeCheckResults)
        {
            if (_devourablesInRange.Add(mb))
            {
                IDevourable d = mb as IDevourable;
                if (d != null)
                    MockEventCenter.TriggerDevourableEnterRange(d);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_isPouncing || _currentTarget == null) return;

        if (Time.fixedTime >= _pounceEndTime)
        {
            CancelPounce();
            return;
        }

        float dist = Vector2.Distance(transform.root.position, _currentTarget.Transform.position);
        if (dist < 0.5f && !_devourSequenceRunning)
        {
            StartCoroutine(RunDevourSequence(_currentTarget));
            return;
        }

        Vector2 toTarget = (_currentTarget.Transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isPouncing || _currentTarget == null) return;

        IDevourable devourable = GetDevourable(other);
        if (devourable != _currentTarget) return;

        if (_devourSequenceRunning) return;

        StartCoroutine(RunDevourSequence(devourable));
    }

    private void TryHandleDevourInput()
    {
        if (baseForm.CurrentState == ActionState.SpecialAction) return;
        if (baseForm.CurrentState == ActionState.WallCling) return;
        if (_isPouncing) return;
        if (Time.time < _cooldownEndTime) return;

        IDevourable target = FindNearestDevourable();
        if (target == null) return;

        _currentTarget = target;
        _currentTarget.IsTargeted = true;
        StartPounce();
    }

    public void CancelAll()
    {
        StopAllCoroutines();
        effectPlayer?.ResetAll();
        _isPouncing = false;
        _devourSequenceRunning = false;
        if (_currentTarget != null)
        {
            _currentTarget.IsTargeted = false;
            _currentTarget = null;
        }
        rb.velocity = Vector2.zero;
        Time.timeScale = 1f;
        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;
        baseForm.SetActionState(ActionState.Idle);
    }

    private IDevourable FindNearestDevourable()
    {
        Vector2 origin = transform.root.position;
        int count = Physics2D.OverlapCircleNonAlloc(origin, detectionRadius, _overlapBuffer, devourableLayer);
        if (count == 0) return null;

        IDevourable best = null;
        float bestPriority = float.MinValue;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            IDevourable devourable = GetDevourable(_overlapBuffer[i]);
            if (devourable == null) continue;
            if (!devourable.CanBeDevoured(playerController)) continue;

            float dSq = ((Vector2)devourable.Transform.position - origin).sqrMagnitude;
            if (devourable.Priority > bestPriority ||
                (devourable.Priority == bestPriority && dSq < bestDistSq))
            {
                bestPriority = devourable.Priority;
                bestDistSq = dSq;
                best = devourable;
            }
        }

        return best;
    }

    private void StartPounce()
    {
        if (_currentTarget == null) return;

        _isPouncing = true;
        _pounceEndTime = Time.fixedTime + pounceMaxDuration;
        baseForm.SetActionState(ActionState.SpecialAction);
        baseForm.SetAnimatorBool(IsSwoopingHash, true);

        Vector2 toTarget = (_currentTarget.Transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;

        AudioManager.Instance?.PlaySfx(pounceClip);
    }

    private void CancelPounce()
    {
        _isPouncing = false;
        if (_currentTarget != null)
        {
            _currentTarget.IsTargeted = false;
            _currentTarget = null;
        }
        rb.velocity = Vector2.zero;
        baseForm.SetActionState(ActionState.Idle);
        baseForm.SetAnimatorBool(IsSwoopingHash, false);
        _cooldownEndTime = Time.time + cooldownSeconds;
    }

    private IEnumerator RunDevourSequence(IDevourable target)
    {
        _devourSequenceRunning = true;
        _isPouncing = false;
        rb.velocity = Vector2.zero;

        Vector2 spitDirection = (transform.root.position - target.Transform.position).normalized;

        yield return null;

        target.OnBeingDevoured();

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Time.timeScale = 0f;

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomIn(target.Transform.position);
        else
            yield return new WaitForSecondsRealtime(0.4f);

        AudioManager.Instance?.PlaySfx(devourClip);

        if (effectPlayer != null)
            yield return effectPlayer.PlayDevour(target);
        else
            yield return new WaitForSecondsRealtime(1.0f);

        target.ExecuteDevourOutcome(playerController);

        AudioManager.Instance?.PlaySfx(spitClip);

        // null-check: DevourableObject may have self-destructed via UnityEvent
        MonoBehaviour targetMb = target as MonoBehaviour;
        if (targetMb != null)
            target.OnBeingSpitOut(spitDirection);

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomOut();
        else
            yield return new WaitForSecondsRealtime(0.4f);

        Time.timeScale = 1f;

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;

        if (target.DestroyAfterDevour && targetMb != null)
            Destroy(targetMb.gameObject);

        baseForm.SetAnimatorBool(IsSwoopingHash, false);
        baseForm.SetActionState(ActionState.Idle);
        target.IsTargeted = false;
        _currentTarget = null;
        _cooldownEndTime = Time.time + cooldownSeconds;
        _devourSequenceRunning = false;
    }

    private static IDevourable GetDevourable(Component c)
    {
        var animal = c.GetComponent<DevourableAnimal>();
        if (animal != null) return animal;
        return c.GetComponent<DevourableObject>();
    }
}
