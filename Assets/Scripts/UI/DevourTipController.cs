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
        MockEventCenter.OnAnimalEnterRange += HandleAnimalEnterRange;
        MockEventCenter.OnAnimalExitRange += HandleAnimalExitRange;
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
        MockEventCenter.OnAnimalEnterRange -= HandleAnimalEnterRange;
        MockEventCenter.OnAnimalExitRange -= HandleAnimalExitRange;
    }

    //处理生物进入检测范围，UI显现的逻辑
    private void HandleAnimalEnterRange(DevourableAnimal animal)
    {
        if (_devourTipUI != null && !_devourTipUI.activeSelf)
        {
            _devourTipUI.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(0f, 0.5f).SetLoops(-1, LoopType.Yoyo);//反复淡入淡出效果
            }
        }
    }

    //处理生物离开检测范围，UI隐藏的逻辑
    private void HandleAnimalExitRange(DevourableAnimal animal)
    {
        if (_devourTipUI != null && _devourTipUI.activeSelf)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill(); // 停止淡入淡出动画
                _canvasGroup.alpha = 1f; // 重置透明度为完全不透明
            }
            _devourTipUI.SetActive(false);
        }
    }
}
