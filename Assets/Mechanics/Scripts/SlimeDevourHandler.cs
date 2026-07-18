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

    private SlimeForm slimeForm;
    private BaseForm baseForm;
    private Rigidbody2D rb;
    private PlayerController playerController;
    private DevourEffectPlayer effectPlayer;

    private DevourableAnimal currentTarget;
    private bool isPouncing;
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
        // 订阅吞噬输入事件（空格键）
        if (PlayerInputReader.Instance != null)
            PlayerInputReader.Instance.OnEatSpit += TryHandleDevourInput;
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        if (PlayerInputReader.Instance != null)
            PlayerInputReader.Instance.OnEatSpit -= TryHandleDevourInput;

        // 清理范围内动物
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

        Vector2 toTarget = (currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isPouncing || currentTarget == null) return;

        DevourableAnimal animal = other.GetComponent<DevourableAnimal>();
        if (animal != currentTarget) return;

        StartCoroutine(RunDevourSequence(animal));
    }

    /// <summary>
    /// 由输入事件驱动调用（空格键按下）
    /// </summary>
    private void TryHandleDevourInput()
    {
        // 原有的检测逻辑（去掉 Input.GetKeyDown 检查）
        if (baseForm.CurrentState == ActionState.SpecialAction) return;
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
        baseForm.SetAnimatorBool("IsDevouring", true);

        Vector2 toTarget = (currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void CancelPounce()
    {
        isPouncing = false;
        currentTarget = null;
        rb.velocity = Vector2.zero;
        baseForm.SetActionState(ActionState.Idle);
        baseForm.SetAnimatorBool("IsDevouring", false);
        cooldownEndTime = Time.time + cooldownSeconds;
    }

    private IEnumerator RunDevourSequence(DevourableAnimal animal)
    {
        isPouncing = false;
        rb.velocity = Vector2.zero;

        yield return null;

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Time.timeScale = 0f;

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomIn(animal.transform.position);
        else
            yield return new WaitForSecondsRealtime(0.4f);

        if (effectPlayer != null)
            yield return effectPlayer.PlayDevour(animal);
        else
            yield return new WaitForSecondsRealtime(1.0f);

        MockEventCenter.TriggerFormUnlock(animal.GrantedForm);
        playerController.SwitchToFormByType(animal.GrantedForm);

        if (effectPlayer != null)
            yield return effectPlayer.PlayZoomOut();
        else
            yield return new WaitForSecondsRealtime(0.4f);

        Time.timeScale = 1f;

        if (baseForm.Animator != null)
            baseForm.Animator.updateMode = AnimatorUpdateMode.Normal;

        baseForm.SetAnimatorBool("IsDevouring", false);
        Destroy(animal.gameObject);
        baseForm.SetActionState(ActionState.Idle);
        currentTarget = null;
        cooldownEndTime = Time.time + cooldownSeconds;
    }
}