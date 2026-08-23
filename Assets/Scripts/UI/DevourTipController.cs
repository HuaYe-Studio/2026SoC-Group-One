using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 控制吞噬提示 UI（space_ui）与吐出提示 UI（InorganicTip）的显示与隐藏。
/// 挂载在 PF_UI_DevourTip 根节点上。
/// </summary>
public class DevourTipController : MonoBehaviour
{
    [Header("吞噬提示 UI")]
    [SerializeField] private GameObject _devourTipUI;//对应显示的UI图像（暂时只有空格space）

    [Header("吐出提示 UI")]
    [SerializeField] private GameObject _inorganicTipUI;//对应显示的吐出提示UI（F键）

    private CanvasGroup _canvasGroup;
    private bool _canVoluntarySpit;
    private readonly HashSet<IDevourable> _devourablesInRange = new HashSet<IDevourable>();

    void Awake()
    {
        if (_devourTipUI != null)
        {
            _canvasGroup = _devourTipUI.GetComponent<CanvasGroup>();
        }
    }

    //订阅吞噬检测范围事件与持有状态事件
    void OnEnable()
    {
        MockEventCenter.OnDevourableEnterRange += HandleDevourableEnterRange;
        MockEventCenter.OnDevourableExitRange += HandleDevourableExitRange;
        MockEventCenter.OnHeldObjectChanged += HandleHeldObjectChanged;
    }

    // Start is called before the first frame update
    void Start()
    {
        RefreshTips();//默认隐藏两个提示UI
    }

    //退订吞噬检测范围事件与持有状态事件
    void OnDisable()
    {
        MockEventCenter.OnDevourableEnterRange -= HandleDevourableEnterRange;
        MockEventCenter.OnDevourableExitRange -= HandleDevourableExitRange;
        MockEventCenter.OnHeldObjectChanged -= HandleHeldObjectChanged;

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
    }

    //处理可吞噬目标进入检测范围，记录后统一刷新提示
    private void HandleDevourableEnterRange(IDevourable target)
    {
        _devourablesInRange.Add(target);
        RefreshTips();
    }

    //处理可吞噬目标离开检测范围，记录后统一刷新提示
    private void HandleDevourableExitRange(IDevourable target)
    {
        _devourablesInRange.Remove(target);
        RefreshTips();
    }

    //处理持有状态变化，仅保存状态，不直接显隐，统一刷新提示
    private void HandleHeldObjectChanged(bool canVoluntarySpit)
    {
        _canVoluntarySpit = canVoluntarySpit;
        RefreshTips();
    }

    //统一刷新两类提示：持有可吐出物体只显示吐出，否则附近有目标才显示吞噬
    private void RefreshTips()
    {
        bool showSpit = _canVoluntarySpit;
        bool showDevour = !showSpit && _devourablesInRange.Count > 0;

        SetSpitTipActive(showSpit);
        SetDevourTipActive(showDevour);
    }

    private void SetSpitTipActive(bool show)
    {
        if (_inorganicTipUI == null) return;

        if (_inorganicTipUI.activeSelf != show)
            _inorganicTipUI.SetActive(show);
    }

    private void SetDevourTipActive(bool show)
    {
        if (_devourTipUI == null) return;

        if (show)
        {
            if (!_devourTipUI.activeSelf)
            {
                _devourTipUI.SetActive(true);
                if (_canvasGroup != null)
                {
                    _canvasGroup.DOFade(0f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                }
            }
        }
        else
        {
            if (_devourTipUI.activeSelf)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.DOKill();
                    _canvasGroup.alpha = 1f;
                }
                _devourTipUI.SetActive(false);
            }
        }
    }
}
