using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 控制吞噬提示 UI（space_ui）的显示与隐藏。
/// 挂载在 PF_UI_DevourTip 根节点上。
/// </summary>
public class DevourTipController : MonoBehaviour
{
    [Header("吞噬提示 UI")]
    [SerializeField] private GameObject _devourTipUI;//对应显示的UI图像（暂时只有空格space）

    private bool _isAnimalInRange = false;//是否有可吞噬的动物在范围内,默认是false
    //吞噬检测代码不是UI模块职责，暂时留空等待
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        if (_devourTipUI != null)
        {
            _canvasGroup = _devourTipUI.GetComponent<CanvasGroup>();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_devourTipUI != null)
        {
            _devourTipUI.SetActive(false);//默认隐藏吞噬提示UI
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            _isAnimalInRange = !_isAnimalInRange;//简单的测试程序
        }

        if (_isAnimalInRange)
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
        else
        {
            if (_devourTipUI != null && _devourTipUI.activeSelf)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.DOKill();//停止淡入淡出效果
                }
                _devourTipUI.SetActive(false);
            }
        }
    }
}
