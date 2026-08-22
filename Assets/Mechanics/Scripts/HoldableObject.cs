using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class HoldableObject : DevourableObject, IHoldable
{
    [Header("Holdable Config")]
    [SerializeField] private bool canVoluntarySpit = true;

    [Header("Hold Effects")]
    [SerializeField] private UnityEvent onEquipEffect;
    [SerializeField] private UnityEvent onUnequipEffect;

    [Header("Stat Modifiers")]
    [SerializeField] private HoldableModifier statModifier;

    private bool _isHeld;

    // ── IHoldable ──

    public bool CanVoluntarySpit => canVoluntarySpit;

    public void OnEquip(PlayerController playerController)
    {
        _isHeld = true;
        hasBeenDevoured = true;
        onEquipEffect?.Invoke();

        // 兜底：即使 onEquipEffect 的 UnityEvent 未触发（如 m_Mode 配置问题），
        // 持有 HeavyObject 的持有物（如 HeavyStone）也要让玩家变重
        GetComponent<HeavyObject>()?.SetPlayerHeavy();

        if (statModifier.HasAnyEffect && playerController?.ActiveForm != null)
            playerController.ActiveForm.ApplyHoldableModifier(statModifier);
    }

    public void OnUnequip(PlayerController playerController)
    {
        _isHeld = false;
        onUnequipEffect?.Invoke();

        GetComponent<HeavyObject>()?.RemovePlayerHeavy();

        if (statModifier.HasAnyEffect && playerController?.ActiveForm != null)
            playerController.ActiveForm.RemoveHoldableModifier(statModifier);
    }

    public void PlaceInWorld(Vector3 position)
    {
        transform.position = position;
        gameObject.SetActive(true);

        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = Color.white;
            DOTween.Kill(SpriteRenderer);
        }

        DOTween.Kill(transform);
        transform.localScale = Vector3.one;

        ResetDevoured();
        _isHeld = false;
    }

    // ── Override DevourableObject hooks ──

    protected override bool GetDestroyAfterDevour() => false;

    protected override bool CanBeDevouredOverride(PlayerController pc)
    {
        if (_isHeld) return false;
        return base.CanBeDevouredOverride(pc);
    }

    protected override void ExecuteDevourOutcomeOverride(PlayerController pc)
    {
        // Handled by DevourHandler via IHoldable.OnEquip
    }

    protected override void OnBeingSpitOutOverride(Vector2 direction)
    {
        // Handled by DevourHandler via IHoldable.PlaceInWorld + OnUnequip
    }
}
