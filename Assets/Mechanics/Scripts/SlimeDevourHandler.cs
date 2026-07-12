using System.Collections;
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

    public bool TryHandleDevourInput()
    {
        if (baseForm.CurrentState == ActionState.SpecialAction) return false;
        if (isPouncing) return false;
        if (Time.time < cooldownEndTime) return false;
        if (!Input.GetKeyDown(KeyCode.E)) return false;

        DevourableAnimal target = FindNearestDevourable();
        if (target == null) return false;

        currentTarget = target;
        currentTarget.IsTargeted = true;
        StartPounce();
        return true;
    }

    public void CancelAll()
    {
        StopAllCoroutines();
        isPouncing = false;
        currentTarget = null;
        rb.velocity = Vector2.zero;
        Time.timeScale = 1f;
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

        Vector2 toTarget = (currentTarget.transform.position - transform.root.position).normalized;
        rb.velocity = toTarget * pounceSpeed;
    }

    private void CancelPounce()
    {
        isPouncing = false;
        currentTarget = null;
        rb.velocity = Vector2.zero;
        baseForm.SetActionState(ActionState.Idle);
        cooldownEndTime = Time.time + cooldownSeconds;
    }

    private IEnumerator RunDevourSequence(DevourableAnimal animal)
    {
        isPouncing = false;
        rb.velocity = Vector2.zero;

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

        Destroy(animal.gameObject);
        baseForm.SetActionState(ActionState.Idle);
        currentTarget = null;
        cooldownEndTime = Time.time + cooldownSeconds;
    }
}
