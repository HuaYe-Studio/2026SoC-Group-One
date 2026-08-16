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
}
