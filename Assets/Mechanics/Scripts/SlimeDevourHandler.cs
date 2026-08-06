using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SlimeForm))]
public class SlimeDevourHandler : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 4f;
    [SerializeField] private LayerMask animalLayer;

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

    private DevourableAnimal _currentTarget;
    private bool _isPouncing;
    public bool IsPouncing => _isPouncing;
    private bool _devourSequenceRunning;
    private float _pounceEndTime;
    private float _cooldownEndTime;

    private HashSet<DevourableAnimal> _animalsInRange = new HashSet<DevourableAnimal>();
    private List<DevourableAnimal> _rangeCheckResults = new List<DevourableAnimal>();
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

        foreach (DevourableAnimal animal in _animalsInRange)
            MockEventCenter.TriggerAnimalExitRange(animal);
        _animalsInRange.Clear();
    }

    private void Update()
    {
        UpdateAnimalsInRange();
    }

    private void UpdateAnimalsInRange()
    {
        Vector2 origin = transform.root.position;
        int count = Physics2D.OverlapCircleNonAlloc(origin, detectionRadius, _overlapBuffer, animalLayer);

        _rangeCheckResults.Clear();
        for (int i = 0; i < count; i++)
        {
            DevourableAnimal animal = _overlapBuffer[i].GetComponent<DevourableAnimal>();
            if (animal != null)
                _rangeCheckResults.Add(animal);
        }

        foreach (DevourableAnimal animal in _animalsInRange)
        {
            if (!_rangeCheckResults.Contains(animal))
                MockEventCenter.TriggerAnimalExitRange(animal);
        }
        _animalsInRange.RemoveWhere(a => !_rangeCheckResults.Contains(a));

        foreach (DevourableAnimal animal in _rangeCheckResults)
        {
            if (_animalsInRange.Add(animal))
                MockEventCenter.TriggerAnimalEnterRange(animal);
        }
    }

    private void FixedUpdate()
    {
        if (!_isPouncing) return;

        if (Time.fixedTime >= _pounceEndTime || _currentTarget == null)
        {
            CancelPounce();
            return;
        }

        float dist = Vector2.Distance(transform.root.position, _currentTarget.transform.position);
        if (dist < 0.5f)
        {
            StartCoroutine(RunDevourSequence(_currentTarget));
            return;
        }

        Vector2 toTarget = (_currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isPouncing || _currentTarget == null) return;

        DevourableAnimal animal = other.GetComponent<DevourableAnimal>();
        if (animal != _currentTarget) return;

        if (_devourSequenceRunning) return;

        StartCoroutine(RunDevourSequence(animal));
    }

    private void TryHandleDevourInput()
    {
        if (baseForm.CurrentState == ActionState.SpecialAction) return;
        if (baseForm.CurrentState == ActionState.WallCling) return;
        if (_isPouncing) return;
        if (Time.time < _cooldownEndTime) return;

        DevourableAnimal target = FindNearestDevourable();
        if (target == null) return;

        _currentTarget = target;
        _currentTarget.IsTargeted = true;
        StartPounce();
    }

    public void CancelAll()
    {
        StopAllCoroutines();
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

    private DevourableAnimal FindNearestDevourable()
    {
        Vector2 origin = transform.root.position;
        int count = Physics2D.OverlapCircleNonAlloc(origin, detectionRadius, _overlapBuffer, animalLayer);
        if (count == 0) return null;

        DevourableAnimal best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            DevourableAnimal animal = _overlapBuffer[i].GetComponent<DevourableAnimal>();
            if (animal == null) continue;
            if (playerController.IsFormUnlocked(animal.GrantedForm)) continue;

            float dSq = ((Vector2)animal.transform.position - origin).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = animal;
            }
        }

        return best;
    }

    private void StartPounce()
    {
        _isPouncing = true;
        _pounceEndTime = Time.fixedTime + pounceMaxDuration;
        baseForm.SetActionState(ActionState.SpecialAction);
        baseForm.SetAnimatorBool(IsSwoopingHash, true);

        Vector2 toTarget = (_currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;

        AudioManager.Instance?.PlaySFX(pounceClip);
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

    private IEnumerator RunDevourSequence(DevourableAnimal animal)
    {
        _devourSequenceRunning = true;
        _isPouncing = false;
        rb.velocity = Vector2.zero;

        Vector2 spitDirection = (transform.root.position - animal.transform.position).normalized;

        yield return null;

        animal.PlayBeingDevoured();

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Time.timeScale = 0f;

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomIn(animal.transform.position);
        else
            yield return new WaitForSecondsRealtime(0.4f);

        AudioManager.Instance?.PlaySFX(devourClip);

        if (effectPlayer != null)
            yield return effectPlayer.PlayDevour(animal);
        else
            yield return new WaitForSecondsRealtime(1.0f);

        bool isNewForm = !playerController.IsFormUnlocked(animal.GrantedForm);
        if (isNewForm)
        {
            MockEventCenter.TriggerFormUnlock(animal.GrantedForm);
            playerController.SwitchToFormByType(animal.GrantedForm);
        }

        AudioManager.Instance?.PlaySFX(spitClip);
        animal.PlayBeingSpitOut(spitDirection);

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomOut();
        else
            yield return new WaitForSecondsRealtime(0.4f);

        Time.timeScale = 1f;

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;

        baseForm.SetAnimatorBool(IsSwoopingHash, false);
        baseForm.SetActionState(ActionState.Idle);
        _currentTarget = null;
        _cooldownEndTime = Time.time + cooldownSeconds;
        _devourSequenceRunning = false;
    }
}
