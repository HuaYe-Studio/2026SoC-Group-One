using UnityEngine;
using UnityEngine.UI;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 能力图标 UI 控制器：显示 Frog 当前跳跃模式（普通跳 / 蓄力跳）
/// </summary>
public class AbilityUIController : MonoBehaviour
{
    [SerializeField] private Canvas _abilityCanvas;//能力图标画布
    [SerializeField] private Image _abilityImage;//能力图标
    [SerializeField] private Sprite _simpleJumpIcon;//普通跳图标
    [SerializeField] private Sprite _poiseJumpIcon;//蓄力跳图标

    private PlayerController _playerController;//玩家控制器
    private FrogForm _frogForm;//青蛙形态

    void Start()
    {
        _playerController = FindObjectOfType<PlayerController>(true);
        if (_playerController != null)
            _frogForm = _playerController.GetForm(FormType.Frog) as FrogForm;

        if (_playerController == null || _frogForm == null ||
            _abilityCanvas == null || _abilityImage == null ||
            _simpleJumpIcon == null || _poiseJumpIcon == null)
        {
            if (_abilityCanvas != null)
                _abilityCanvas.enabled = false;
            Debug.LogWarning("AbilityUIController: 缺少 Player/Frog/序列化引用，能力图标已隐藏。");
            return;
        }

        MockEventCenter.OnFormChanged += OnFormChanged;
        _frogForm.OnChargeModeChanged += OnChargeModeChanged;

        RefreshAbilityUI();
    }

    void OnDestroy()
    {
        MockEventCenter.OnFormChanged -= OnFormChanged;
        if (_frogForm != null)
            _frogForm.OnChargeModeChanged -= OnChargeModeChanged;
    }

    private void OnFormChanged(FormType formType)
    {
        if (formType == FormType.Frog)
        {
            _abilityCanvas.enabled = true;
            RefreshIcon();
        }
        else
        {
            _abilityCanvas.enabled = false;
        }
    }

    private void OnChargeModeChanged(bool chargeModeEnabled)
    {
        _abilityImage.sprite = chargeModeEnabled ? _poiseJumpIcon : _simpleJumpIcon;
    }

    private void RefreshAbilityUI()
    {
        if (_playerController.GetCurrentForm() == FormType.Frog)
        {
            _abilityCanvas.enabled = true;
            RefreshIcon();
        }
        else
        {
            _abilityCanvas.enabled = false;
        }
    }

    private void RefreshIcon()
    {
        _abilityImage.sprite = _frogForm.IsChargeMode ? _poiseJumpIcon : _simpleJumpIcon;
    }
}
