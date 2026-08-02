using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
