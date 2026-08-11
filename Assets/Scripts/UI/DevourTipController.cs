using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 控制吞噬提示 UI（space_ui）的显示与隐藏。
/// 挂载在 PF_UI_DevourTip 根节点上。
/// </summary>
public class DevourTipController : MonoBehaviour
{
    [Header("吞噬提示 UI")]
    [SerializeField] private GameObject _devourTipUI;//对应显示的UI图像（暂时只有空格space）


    private CanvasGroup _canvasGroup;

    void Awake()
    {
        if (_devourTipUI != null)
        {
            _canvasGroup = _devourTipUI.GetComponent<CanvasGroup>();
        }
    }

    //订阅吞噬检测范围事件
    void OnEnable()
    {
        MockEventCenter.OnDevourableEnterRange += HandleDevourableEnterRange;
        MockEventCenter.OnDevourableExitRange += HandleDevourableExitRange;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_devourTipUI != null)
        {
            _devourTipUI.SetActive(false);//默认隐藏吞噬提示UI
        }
    }

    //退订吞噬检测范围事件
    void OnDisable()
    {
        MockEventCenter.OnDevourableEnterRange -= HandleDevourableEnterRange;
        MockEventCenter.OnDevourableExitRange -= HandleDevourableExitRange;

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
    }

    //处理可吞噬目标进入检测范围，UI显现的逻辑
    private void HandleDevourableEnterRange(IDevourable target)
    {
        if (_devourTipUI != null && !_devourTipUI.activeSelf)
        {
            _devourTipUI.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(0f, 0.5f).SetLoops(-1, LoopType.Yoyo);
            }
        }
    }

    //处理可吞噬目标离开检测范围，UI隐藏的逻辑
    private void HandleDevourableExitRange(IDevourable target)
    {
        if (_devourTipUI != null && _devourTipUI.activeSelf)
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
