using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 加载界面控制器
/// </summary>
public class LoadPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _panelCanvasGroup; // 遮罩面板的 CanvasGroup 组件
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowLoadPanel()
    {
        Time.timeScale = 0f;
        _panelCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = true;
        _panelCanvasGroup.interactable = true;
    }

    public void HideLoadPanel()
    {
        _panelCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = false;
        _panelCanvasGroup.interactable = false;
        Time.timeScale = 1f;
    }
}
