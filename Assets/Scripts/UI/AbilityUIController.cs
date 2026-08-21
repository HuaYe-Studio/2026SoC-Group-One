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
    private bool _subscribedToFormChanged;//是否已订阅形态变化事件
    private bool _subscribedToChargeModeChanged;//是否已订阅跳跃模式变化事件
    private bool _warned;//是否已输出缺少引用警告

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureReferences();
        SubscribeEvents();
    }

    void Start()
    {
        EnsureReferences();
        SubscribeEvents();

        if (!TryInitialize())
            return;

        RefreshAbilityUI();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (!_subscribedToFormChanged)
        {
            MockEventCenter.OnFormChanged += OnFormChanged;
            _subscribedToFormChanged = true;
        }

        if (_frogForm != null && !_subscribedToChargeModeChanged)
        {
            _frogForm.OnChargeModeChanged += OnChargeModeChanged;
            _subscribedToChargeModeChanged = true;
        }
    }

    private void UnsubscribeEvents()
    {
        if (_subscribedToFormChanged)
        {
            MockEventCenter.OnFormChanged -= OnFormChanged;
            _subscribedToFormChanged = false;
        }

        if (_subscribedToChargeModeChanged)
        {
            if (_frogForm != null)
                _frogForm.OnChargeModeChanged -= OnChargeModeChanged;
            _subscribedToChargeModeChanged = false;
        }
    }

    private void EnsureReferences()
    {
        if (_playerController == null)
        {
            PlayerController[] playerControllers = FindObjectsOfType<PlayerController>(true);
            if (playerControllers != null && playerControllers.Length > 0)
                _playerController = playerControllers[0];
        }

        if (_playerController != null && _frogForm == null)
            _frogForm = _playerController.GetForm(FormType.Frog) as FrogForm;

        if (_frogForm == null)
        {
            FrogForm[] frogForms = FindObjectsOfType<FrogForm>(true);
            if (frogForms != null && frogForms.Length > 0)
                _frogForm = frogForms[0];
        }
    }

    private bool TryInitialize()
    {
        if (_playerController == null || _frogForm == null ||
            _abilityCanvas == null || _abilityImage == null ||
            _simpleJumpIcon == null || _poiseJumpIcon == null)
        {
            WarnOnceAndHide();
            return false;
        }

        return true;
    }

    private void WarnOnceAndHide()
    {
        if (_warned)
            return;

        _warned = true;
        if (_abilityCanvas != null)
            _abilityCanvas.gameObject.SetActive(false);
        Debug.LogWarning("AbilityUIController: 缺少 Player/Frog/序列化引用，能力图标已隐藏。");
    }

    private void OnFormChanged(FormType formType)
    {
        EnsureReferences();
        SubscribeEvents();

        if (!TryInitialize())
            return;

        if (formType == FormType.Frog)
        {
            _abilityCanvas.gameObject.SetActive(true);
            RefreshIcon();
        }
        else
        {
            _abilityCanvas.gameObject.SetActive(false);
        }
    }

    private void OnChargeModeChanged(bool chargeModeEnabled)
    {
        EnsureReferences();

        if (!TryInitialize())
            return;

        RefreshIcon();
    }

    private void RefreshAbilityUI()
    {
        if (_playerController.GetCurrentForm() == FormType.Frog)
        {
            _abilityCanvas.gameObject.SetActive(true);
            RefreshIcon();
        }
        else
        {
            _abilityCanvas.gameObject.SetActive(false);
        }
    }

    private void RefreshIcon()
    {
        _abilityImage.sprite = _frogForm.IsChargeMode ? _poiseJumpIcon : _simpleJumpIcon;
    }
}
