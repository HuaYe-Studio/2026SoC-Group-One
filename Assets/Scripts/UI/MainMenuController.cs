using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject _startButton;
    [SerializeField] private GameObject _loadButton;
    [SerializeField] private GameObject _settingButton;
    [SerializeField] private GameObject _exitButton;
    [SerializeField] private string _startSceneName;
    // Start is called before the first frame update
    void Start()
    {
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
