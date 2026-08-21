using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SlimeForm))]
public class DevourHandler : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 2f;
    [SerializeField]
    [FormerlySerializedAs("animalLayer")]
    private LayerMask devourableLayer;

    [Header("Pounce")]
    [SerializeField] private float pounceSpeed = 14f;
    [SerializeField] private float pounceMaxDuration = 0.6f;
    [SerializeField] private float cooldownSeconds = 0.5f;

    [Header("Devour Sequence")]
    [SerializeField] private float animalFlightSpeed = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip pounceClip;
    [SerializeField] private AudioClip devourClip;
    [SerializeField] private AudioClip spitClip;

    private SlimeForm _slimeForm;
    private BaseForm _baseForm;
    private Rigidbody2D _rb;
    private PlayerController _playerController;
    private DevourEffectPlayer _effectPlayer;

    private IDevourable _currentTarget;
    private bool _isPouncing;
    public bool IsPouncing => _isPouncing;
    private bool _devourSequenceRunning;
    private Vector2 _pounceStartPos;
    private bool _devourInitiatedSwitchPending;
    private FormType _deferredFormType;
    public bool IsDevourInitiatedSwitchPending => _devourInitiatedSwitchPending;
    private float _pounceEndTime;
    private float _cooldownEndTime;

    private IHoldable _heldObject;
    public bool HasHeldObject => _heldObject != null;

    private readonly HashSet<MonoBehaviour> _devourablesInRange = new HashSet<MonoBehaviour>();
    private readonly List<MonoBehaviour> _rangeCheckResults = new List<MonoBehaviour>();
    private readonly Collider2D[] _overlapBuffer = new Collider2D[32];

    // Solid "player-target" collider pairs temporarily ignored during pounce,
    // so solid devourables (e.g. HeavyStone) can be swooped through to trigger devour.
    private readonly List<(Collider2D player, Collider2D target)> _pounceIgnoredColliders =
        new List<(Collider2D, Collider2D)>();

    private static readonly int IsSwoopingHash = Animator.StringToHash("IsSwooping");

    private void Awake()
    {
        _slimeForm = GetComponent<SlimeForm>();
        _baseForm = _slimeForm;
        _rb = GetComponentInParent<Rigidbody2D>();
        _playerController = GetComponentInParent<PlayerController>();
    }

    private void Start()
    {
        _effectPlayer = Camera.main?.GetComponent<DevourEffectPlayer>();
        if (_effectPlayer == null)
            Debug.LogWarning("DevourHandler: No DevourEffectPlayer on Main Camera.");
    }

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnInput_Space += TryHandleDevourInput;
            PlayerInputReader.Instance.OnSpit += TrySpitOutHeldObject;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnInput_Space -= TryHandleDevourInput;
            PlayerInputReader.Instance.OnSpit -= TrySpitOutHeldObject;
        }

        foreach (MonoBehaviour mb in _devourablesInRange)
        {
            if (mb is IDevourable d)
                MockEventCenter.TriggerDevourableExitRange(d);
        }
        _devourablesInRange.Clear();

        StopAllCoroutines();
        SetPounceCollisionIgnore(false);
        _isPouncing = false;
        _devourSequenceRunning = false;
        _devourInitiatedSwitchPending = false;
        _currentTarget = null;
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
            if (devourable != null && devourable.CanBeDevoured(_playerController))
                _rangeCheckResults.Add((MonoBehaviour)devourable);
        }

        foreach (MonoBehaviour mb in _devourablesInRange)
        {
            if (!_rangeCheckResults.Contains(mb))
            {
                if (mb is IDevourable d)
                    MockEventCenter.TriggerDevourableExitRange(d);
            }
        }
        _devourablesInRange.RemoveWhere(mb => !_rangeCheckResults.Contains(mb));

        foreach (MonoBehaviour mb in _rangeCheckResults)
        {
            if (_devourablesInRange.Add(mb))
            {
                if (mb is IDevourable d)
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
        _rb.velocity = toTarget * pounceSpeed;
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
        if (_baseForm.CurrentState == ActionState.SpecialAction) return;
        if (_baseForm.CurrentState == ActionState.WallCling) return;
        if (_isPouncing) return;
        if (_devourSequenceRunning) return;
        if (_heldObject != null) return;
        if (Time.time < _cooldownEndTime) return;

        IDevourable target = FindNearestDevourable();
        if (target == null) return;

        _currentTarget = target;
        _currentTarget.IsTargeted = true;
        StartPounce();
    }

    public void CancelAll()
    {
        SpitOutHeldObject();
        SetPounceCollisionIgnore(false);

        StopAllCoroutines();
        _effectPlayer?.ResetAll();
        _isPouncing = false;
        _devourSequenceRunning = false;
        _devourInitiatedSwitchPending = false;
        if (_currentTarget != null)
        {
            _currentTarget.IsTargeted = false;
            if (_currentTarget is DevourableAnimal da)
                da.IsInDevourSequence = false;
            _currentTarget = null;
        }
        _rb.velocity = Vector2.zero;
        _baseForm.SetActionState(ActionState.Idle);
    }

    public void SpitOutHeldObject()
    {
        if (_heldObject == null) return;

        IHoldable holdable = _heldObject;
        _heldObject = null;

        Vector3 spitPos = transform.root.position + (Vector3)(_baseForm.FacingDirection * 1f);

        holdable.PlaceInWorld(spitPos);
        holdable.OnUnequip(_playerController);

        AudioManager.Instance?.PlaySfx(spitClip);

        if (holdable is MonoBehaviour)
            MockEventCenter.TriggerDevourableExitRange(holdable);
    }

    private void TrySpitOutHeldObject()
    {
        if (_heldObject == null) return;
        if (!_heldObject.CanVoluntarySpit) return;

        SpitOutHeldObject();
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
            if (!devourable.CanBeDevoured(_playerController)) continue;

            float dSq = ((Vector2)devourable.Transform.position - origin).sqrMagnitude;
            if (devourable.Priority > bestPriority ||
                (Mathf.Approximately(devourable.Priority, bestPriority) && dSq < bestDistSq))
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

        _pounceStartPos = transform.root.position;

        _isPouncing = true;
        _pounceEndTime = Time.fixedTime + pounceMaxDuration;
        _baseForm.SetActionState(ActionState.SpecialAction);
        _baseForm.SetAnimatorBool(IsSwoopingHash, true);

        Vector2 toTarget = (_currentTarget.Transform.position - transform.root.position).normalized;
        _rb.velocity = toTarget * pounceSpeed;

        SetPounceCollisionIgnore(true);

        AudioManager.Instance?.PlaySfx(pounceClip);
    }

    private void CancelPounce()
    {
        SetPounceCollisionIgnore(false);
        _isPouncing = false;
        if (_currentTarget != null)
        {
            _currentTarget.IsTargeted = false;
            _currentTarget = null;
        }
        _rb.velocity = Vector2.zero;
        _baseForm.SetActionState(ActionState.Idle);
        _baseForm.SetAnimatorBool(IsSwoopingHash, false);
        _cooldownEndTime = Time.time + cooldownSeconds;
    }

    /// <summary>
    /// Temporarily ignore "solid-solid" collision between the player and the target during
    /// pounce, so solid devourables (e.g. HeavyStone) can be swooped through to reach the
    /// dist&lt;0.5f success check. Trigger colliders are skipped, keeping the animal /
    /// FireCrystal OnTriggerEnter2D path intact.
    /// </summary>
    private void SetPounceCollisionIgnore(bool ignore)
    {
        if (ignore)
        {
            if (_currentTarget == null) return;
            Collider2D[] playerColliders = transform.root.GetComponentsInChildren<Collider2D>();
            Collider2D[] targetColliders = _currentTarget.Transform.GetComponentsInChildren<Collider2D>();

            _pounceIgnoredColliders.Clear();
            foreach (Collider2D playerCol in playerColliders)
            {
                if (playerCol == null || playerCol.isTrigger) continue;
                foreach (Collider2D targetCol in targetColliders)
                {
                    if (targetCol == null || targetCol.isTrigger) continue;
                    Physics2D.IgnoreCollision(playerCol, targetCol, true);
                    _pounceIgnoredColliders.Add((playerCol, targetCol));
                }
            }
        }
        else
        {
            foreach ((Collider2D player, Collider2D target) in _pounceIgnoredColliders)
            {
                if (player != null && target != null)
                    Physics2D.IgnoreCollision(player, target, false);
            }
            _pounceIgnoredColliders.Clear();
        }
    }

    private IEnumerator RunDevourSequence(IDevourable target)
    {
        _devourSequenceRunning = true;
        _isPouncing = false;
        _rb.velocity = Vector2.zero;
        SetPounceCollisionIgnore(false); // restore before target hides, else it would clip through the player when spat out

        Vector2 pointA = _pounceStartPos;
        Vector2 pointB = target.Transform.position;
        Vector2 spitDirection = (pointA - pointB).normalized;

        MonoBehaviour targetMb = target as MonoBehaviour;

        bool isHoldable = target is IHoldable;
        DevourableAnimal devAnimal = target as DevourableAnimal;
        bool isFirstDevour = !isHoldable && devAnimal != null
            && !_playerController.IsFormUnlocked(devAnimal.GrantedForm);

        yield return null;

        // ── Engulf: time freeze + DOTween ──
        target.OnBeingDevoured();

        if (_baseForm.Animator != null)
            _baseForm.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Time.timeScale = 0f;

        if (_effectPlayer != null)
            yield return _effectPlayer.PlayZoomIn(target.Transform.position);
        else
            yield return new WaitForSecondsRealtime(0.4f);

        AudioManager.Instance?.PlaySfx(devourClip);

        if (_effectPlayer != null)
            yield return _effectPlayer.PlayDevour(target);
        else
            yield return new WaitForSecondsRealtime(1.0f);

        // ── Outcome (during freeze, unlock only — no form switch) ──
        if (!isHoldable)
        {
            target.ExecuteDevourOutcome(_playerController);
            AudioManager.Instance?.PlaySfx(spitClip);
            target.OnBeingSpitOut(spitDirection);
        }
        else
        {
            IHoldable holdable = (IHoldable)target;
            holdable.OnEquip(_playerController);
            _heldObject = holdable;
            targetMb?.gameObject.SetActive(false);
        }

        if (_effectPlayer != null)
            yield return _effectPlayer.PlayZoomOut();
        else
            yield return new WaitForSecondsRealtime(0.4f);

        Time.timeScale = 1f;

        if (_baseForm.Animator != null)
            _baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;

        // ── Spit animal in BA direction then decelerate + stun (on animal, survives form switch) ──
        if (!isHoldable && devAnimal != null)
        {
            devAnimal.LaunchAndStun(spitDirection, animalFlightSpeed);
        }

        // ── Cleanup ──
        _baseForm.SetAnimatorBool(IsSwoopingHash, false);
        if (!isHoldable && target.DestroyAfterDevour && targetMb != null)
            Destroy(targetMb.gameObject);

        _baseForm.SetActionState(ActionState.Idle);
        target.IsTargeted = false;
        _currentTarget = null;
        _cooldownEndTime = Time.time + cooldownSeconds;
        _devourSequenceRunning = false;

        // ── Deferred form switch (first-time devour only) ──
        if (isFirstDevour && devAnimal != null)
        {
            _devourInitiatedSwitchPending = true;
            _deferredFormType = devAnimal.GrantedForm;
            _playerController.SwitchToFormByType(_deferredFormType);
            _devourInitiatedSwitchPending = false;
        }
    }

    private static IDevourable GetDevourable(Component c)
    {
        var animal = c.GetComponent<DevourableAnimal>();
        if (animal != null) return animal;
        return c.GetComponent<DevourableObject>();
    }
}
