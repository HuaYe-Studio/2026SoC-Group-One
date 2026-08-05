using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 主界面控制器
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject _startButton;
    [SerializeField] private GameObject _loadButton;
    [SerializeField] private GameObject _settingButton;
    [SerializeField] private GameObject _exitButton;
    [SerializeField] private string _startSceneName;

    private CanvasGroup _startButtonCanvasGroup, _loadButtonCanvasGroup, _settingButtonCanvasGroup, _exitButtonCanvasGroup;

    void OnEnable()
    {
        UIEventCenter.OnMainMenuSettingPanelOpened += OnSettingPanelOpened;
        UIEventCenter.OnMainMenuSettingPanelClosed += OnSettingPanelClosed;
    }

    // Start is called before the first frame update
    void Start()
    {
        _startButtonCanvasGroup = _startButton.GetComponent<CanvasGroup>();
        _loadButtonCanvasGroup = _loadButton.GetComponent<CanvasGroup>();
        _settingButtonCanvasGroup = _settingButton.GetComponent<CanvasGroup>();
        _exitButtonCanvasGroup = _exitButton.GetComponent<CanvasGroup>();

        if(SceneTransition.Instance != null)
        {
            _startButton.GetComponent<Button>().onClick.AddListener(() => SceneTransition.Instance.GoToScene(_startSceneName));
        }
        if(UIManager.Instance != null)
        {
            _exitButton.GetComponent<Button>().onClick.AddListener(() => UIManager.Instance.ExitGame());
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDisable()
    {
        UIEventCenter.OnMainMenuSettingPanelOpened -= OnSettingPanelOpened;
        UIEventCenter.OnMainMenuSettingPanelClosed -= OnSettingPanelClosed;
    }

    //设置面版显示时隐藏主界面的按钮
    private void OnSettingPanelOpened()
    {
        _startButtonCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _loadButtonCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _settingButtonCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _exitButtonCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);

        _startButtonCanvasGroup.blocksRaycasts = false;
        _loadButtonCanvasGroup.blocksRaycasts = false;
        _settingButtonCanvasGroup.blocksRaycasts = false;   
        _exitButtonCanvasGroup.blocksRaycasts = false;

        _startButtonCanvasGroup.interactable = false;
        _loadButtonCanvasGroup.interactable = false;
        _settingButtonCanvasGroup.interactable = false;
        _exitButtonCanvasGroup.interactable = false;
    }

    //设置面板关闭时显示主界面按钮
    private void OnSettingPanelClosed()
    {
        _startButtonCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _loadButtonCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _settingButtonCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _exitButtonCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);

        _startButtonCanvasGroup.blocksRaycasts = true;
        _loadButtonCanvasGroup.blocksRaycasts = true;
        _settingButtonCanvasGroup.blocksRaycasts = true;
        _exitButtonCanvasGroup.blocksRaycasts = true;

        _startButtonCanvasGroup.interactable = true;
        _loadButtonCanvasGroup.interactable = true;
        _settingButtonCanvasGroup.interactable = true;
        _exitButtonCanvasGroup.interactable = true;
    }
}
