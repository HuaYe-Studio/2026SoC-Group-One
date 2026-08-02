using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

public class PausePageController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _panelCanvasGroup;//遮罩面板的canvasGroup组件
    [SerializeField] private GameObject _continueButton;
    [SerializeField] private GameObject _backButton;
    [SerializeField] private GameObject _settingButton;

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
        _panelCanvasGroup.alpha = 0.5f;
        _continueButton.SetActive(true);
        _backButton.SetActive(true);
        _settingButton.SetActive(true);
    }

    private void HidePanel()
    {
        Time.timeScale = 1f;
        _panelCanvasGroup.alpha = 0f;

        EventSystem.current.SetSelectedGameObject(null);
        _continueButton.SetActive(false);
        _backButton.SetActive(false);
        _settingButton.SetActive(false);
    }

    public void ContinueButtonClicked()
    {
        PlayerInputReader.Instance.OnUICancelTrigger();
    }

    public void BackButtonClicked()
    {
        SceneTransition.Instance.GoToScene("Scene_MainMenu");
    }

    public void SettingButtonClicked()
    {

    }

}
