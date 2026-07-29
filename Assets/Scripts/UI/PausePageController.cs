using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
public class PausePageController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _panelCanvasGroup;//遮罩面板的canvasGroup组件

    void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnMenu += ShowPanel;
            PlayerInputReader.Instance.OnUICancel += HidePanel;
        }
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnMenu -= ShowPanel;
            PlayerInputReader.Instance.OnUICancel -= HidePanel;
        }
    }

    private void ShowPanel()
    {
        Time.timeScale = 0f;
        _panelCanvasGroup.alpha = 1f;
    }

    private void HidePanel()
    {
        Time.timeScale = 1f;
        _panelCanvasGroup.alpha = 0f;
    }
}
