using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

public class SceneTransition : UISingleton<SceneTransition>
{
    [SerializeField] private RectTransform _doorLeft;
    [SerializeField] private RectTransform _doorRight;
    [SerializeField] private Ease _ease = Ease.InOutQuad;
    [SerializeField] private float _duration = 0.5f;
    [SerializeField] private String _mainMenuName;
    float _doorWidth;

    protected override void Awake()
    {
        base.Awake();
        _doorWidth = Screen.width / 2f;
        _doorLeft.sizeDelta = new Vector2(Screen.width/2f, Screen.height);
        _doorRight.sizeDelta = new Vector2(Screen.width/2f, Screen.height);
    }

    // Start is called before the first frame update
    void Start()
    {
        OpenInstant();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnRectTransformDimensionsChange()
    {
        _doorWidth = Screen.width / 2;
        _doorLeft.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
        _doorRight.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
    }

    public void GoToScene(string sceneName)
    {
        OnRectTransformDimensionsChange();
        
        PlayerInputReader.Instance.OnUICancelTrigger();
        StartCoroutine(Co_Transition(sceneName));
        if(sceneName == _mainMenuName)
        {
            PlayerInputReader.Instance.OnMenuTrigger();
        }
    }

    private IEnumerator Co_Transition(string sceneName)
    {
        //关
        Sequence closeDoor = DOTween.Sequence().SetUpdate(true);
        closeDoor.Join(_doorLeft.DOAnchorPosX(0, _duration).SetEase(_ease));
        closeDoor.Join(_doorRight.DOAnchorPosX(0, _duration).SetEase(_ease));
        yield return closeDoor.WaitForCompletion();

        //加载中循环动画

        //加载场景
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        //停止循环动画

        //开
        Sequence openDoor = DOTween.Sequence().SetUpdate(true);
        openDoor.Join(_doorLeft.DOAnchorPosX(-_doorWidth, _duration).SetEase(_ease));
        openDoor.Join(_doorRight.DOAnchorPosX(_doorWidth, _duration).SetEase(_ease));
        yield return openDoor.WaitForCompletion();

    }

    private void OpenInstant()
    {
        _doorLeft.anchoredPosition = new Vector2(-_doorWidth, 0);
        _doorRight.anchoredPosition = new Vector2(_doorWidth, 0);
    }
}
