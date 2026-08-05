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

    private DevourableAnimal currentTarget;
    private bool isPouncing;
    public bool IsPouncing => isPouncing;
    private bool _devourSequenceRunning;
    private float pounceEndTime;
    private float cooldownEndTime;

    private HashSet<DevourableAnimal> animalsInRange = new HashSet<DevourableAnimal>();
    private List<DevourableAnimal> rangeCheckResults = new List<DevourableAnimal>();
    private Collider2D[] _overlapBuffer = new Collider2D[32];

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
        // 订阅吞噬按键事件（Space）
        // 使用属性访问器确保实例存在
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space += TryHandleDevourInput;
    }

    private void OnDisable()
    {
        // ⚠️ 直接检查私有静态字段，避免触发懒加载
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInput_Space -= TryHandleDevourInput;

        // 清理范围检测
        foreach (DevourableAnimal animal in animalsInRange)
            MockEventCenter.TriggerAnimalExitRange(animal);
        animalsInRange.Clear();
    }

    private void Update()
    {
        UpdateAnimalsInRange();
    }

    private void UpdateAnimalsInRange()
    {
        Vector2 origin = transform.root.position;
        int count = Physics2D.OverlapCircleNonAlloc(origin, detectionRadius, _overlapBuffer, animalLayer);

        rangeCheckResults.Clear();
        for (int i = 0; i < count; i++)
        {
            DevourableAnimal animal = _overlapBuffer[i].GetComponent<DevourableAnimal>();
            if (animal != null)
                rangeCheckResults.Add(animal);
        }

        foreach (DevourableAnimal animal in animalsInRange)
        {
            if (!rangeCheckResults.Contains(animal))
                MockEventCenter.TriggerAnimalExitRange(animal);
        }
        animalsInRange.RemoveWhere(a => !rangeCheckResults.Contains(a));

        foreach (DevourableAnimal animal in rangeCheckResults)
        {
            if (animalsInRange.Add(animal))
                MockEventCenter.TriggerAnimalEnterRange(animal);
        }
    }

    private void FixedUpdate()
    {
        if (!isPouncing) return;

        if (Time.fixedTime >= pounceEndTime || currentTarget == null)
        {
            CancelPounce();
            return;
        }

        float dist = Vector2.Distance(transform.root.position, currentTarget.transform.position);
        if (dist < 0.5f)
        {
            StartCoroutine(RunDevourSequence(currentTarget));
            return;
        }

        Vector2 toTarget = (currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isPouncing || currentTarget == null) return;

        DevourableAnimal animal = other.GetComponent<DevourableAnimal>();
        if (animal != currentTarget) return;

        if (_devourSequenceRunning) return;

        StartCoroutine(RunDevourSequence(animal));
    }

    private void TryHandleDevourInput()
    {
        if (baseForm.CurrentState == ActionState.SpecialAction) return;
        if (baseForm.CurrentState == ActionState.WallCling) return;
        if (isPouncing) return;
        if (Time.time < cooldownEndTime) return;

        DevourableAnimal target = FindNearestDevourable();
        if (target == null) return;

        currentTarget = target;
        currentTarget.IsTargeted = true;
        StartPounce();
    }

    public void CancelAll()
    {
        StopAllCoroutines();
        isPouncing = false;
        _devourSequenceRunning = false;
        currentTarget = null;
        rb.velocity = Vector2.zero;
        Time.timeScale = 1f;
        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;
        baseForm.SetActionState(ActionState.Idle);
    }

    private DevourableAnimal FindNearestDevourable()
    {
        Vector2 origin = transform.root.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, detectionRadius, animalLayer);
        if (hits.Length == 0) return null;

        DevourableAnimal best = null;
        float bestDistSq = float.MaxValue;

        foreach (Collider2D col in hits)
        {
            DevourableAnimal animal = col.GetComponent<DevourableAnimal>();
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
        isPouncing = true;
        pounceEndTime = Time.fixedTime + pounceMaxDuration;
        baseForm.SetActionState(ActionState.SpecialAction);
        baseForm.SetAnimatorBool("IsSwooping", true);

        Vector2 toTarget = (currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;

        AudioManager.Instance?.PlaySFX(pounceClip);
    }

    private void CancelPounce()
    {
        isPouncing = false;
        currentTarget = null;
        rb.velocity = Vector2.zero;
        baseForm.SetActionState(ActionState.Idle);
        baseForm.SetAnimatorBool("IsSwooping", false);
        cooldownEndTime = Time.time + cooldownSeconds;
    }

    private IEnumerator RunDevourSequence(DevourableAnimal animal)
    {
        _devourSequenceRunning = true;
        isPouncing = false;
        rb.velocity = Vector2.zero;

        // 记录吐出方向：B→A（飞扑反方向）
        Vector2 spitDirection = (transform.root.position - animal.transform.position).normalized;

        yield return null;

        // 动物进入被吞噬状态
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

        // 吐出动物
        AudioManager.Instance?.PlaySFX(spitClip);
        animal.PlayBeingSpitOut(spitDirection);

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomOut();
        else
            yield return new WaitForSecondsRealtime(0.4f);

        Time.timeScale = 1f;

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;

        baseForm.SetAnimatorBool("IsSwooping", false);
        baseForm.SetActionState(ActionState.Idle);
        currentTarget = null;
        cooldownEndTime = Time.time + cooldownSeconds;
        _devourSequenceRunning = false;
    }
}