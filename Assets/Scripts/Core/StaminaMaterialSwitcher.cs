using UnityEngine;

public class StaminaMaterialSwitcher : MonoBehaviour
{
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material tiredMaterial;
    [SerializeField, Range(0f, 1f)] private float tiredThreshold = 0.3f;

    private PlayerController _playerController;
    private PlayerStamina _stamina;
    private bool _isTired;

    private void OnEnable()
    {
        _playerController = GetComponent<PlayerController>();
        _stamina = GetComponent<PlayerStamina>();

        MockEventCenter.OnStaminaChanged += OnStaminaChanged;
        MockEventCenter.OnFormChanged += OnFormChanged;
        Refresh();
    }

    private void OnDisable()
    {
        MockEventCenter.OnStaminaChanged -= OnStaminaChanged;
        MockEventCenter.OnFormChanged -= OnFormChanged;
    }

    private void OnStaminaChanged(float current, float max)
    {
        bool tired = max > 0f ? current / max <= tiredThreshold : false;
        SetTired(tired);
    }

    private void OnFormChanged(FormType formType) => Refresh();

    private void Refresh()
    {
        _isTired = _stamina != null && _stamina.Ratio <= tiredThreshold;
        ApplyMaterial();
    }

    private void SetTired(bool tired)
    {
        if (tired == _isTired) return;
        _isTired = tired;
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        var form = _playerController != null ? _playerController.ActiveForm : null;
        var sr = form != null ? form.GetComponent<SpriteRenderer>() : null;
        if (sr != null)
            sr.sharedMaterial = _isTired ? tiredMaterial : normalMaterial;
    }
}
