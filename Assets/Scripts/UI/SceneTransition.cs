using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 场景切换UI，包含开关门动画和加载中UI
/// 挂载在PF_UI_SceneTransition预制体上
/// </summary>
public class SceneTransition : UISingleton<SceneTransition>
{
    [SerializeField] private RectTransform _doorLeft;//左半边遮罩
    [SerializeField] private RectTransform _doorRight;//右半边遮罩
    [SerializeField] private Ease _ease = Ease.InOutQuad;
    [SerializeField] private float _duration = 0.5f;//开关门动画持续时间
    [SerializeField] private String _mainMenuName;//主菜单场景名
    [SerializeField] private CanvasGroup _leftLodingCanvasGroup;//左半边加载中UI
    [SerializeField] private CanvasGroup _rightLodingCanvasGroup;//右半边加载中UI
    [SerializeField] private GameObject _loadingImage;//加载中UI的完整图片
    private float _doorWidth;//门的宽度
    private Vector2 _desiredSize;//加载中UI的完整图片的大小

    protected override void Awake()
    {
        base.Awake();
        //初始化门的宽度和位置
        _doorWidth = Screen.width / 2f;
        _doorLeft.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
        _doorRight.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
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

    //当屏幕尺寸发生变化时，更新门的宽度和位置
    void OnRectTransformDimensionsChange()
    {
        _doorWidth = Screen.width / 2;
        _doorLeft.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
        _doorRight.sizeDelta = new Vector2(Screen.width / 2f, Screen.height);
        _desiredSize = _leftLodingCanvasGroup.GetComponent<RectTransform>().rect.size;
    }

    public void GoToScene(string sceneName)
    {
        OnRectTransformDimensionsChange();

        //触发UI取消事件，防止在切换场景时UI还处于选中状态
        PlayerInputReader.Instance.OnUICancelTrigger();

        StartCoroutine(Co_Transition(sceneName));

        //如果切换到主菜单场景，触发菜单事件
        if (sceneName == _mainMenuName)
        {
            PlayerInputReader.Instance.OnMenuTrigger();
        }
    }

    private IEnumerator Co_Transition(string sceneName)
    {
        bool canContinue = false;//是否可以继续执行开门动画

        //关
        Sequence closeDoor = DOTween.Sequence().SetUpdate(true);
        closeDoor.Join(_doorLeft.DOAnchorPosX(0, _duration).SetEase(_ease));
        closeDoor.Join(_doorRight.DOAnchorPosX(0, _duration).SetEase(_ease));
        yield return closeDoor.WaitForCompletion();

        //加载中循环动画
        yield return new WaitForSecondsRealtime(0.3f);

        _leftLodingCanvasGroup.alpha = 0f;
        _rightLodingCanvasGroup.alpha = 0f;
        _loadingImage.SetActive(true);
        _loadingImage.transform.rotation = Quaternion.identity;  // 归零

        //设置加载中UI的完整图片的大小为半边门宽度的两倍和屏幕高度
        RectTransform loadingImageRect = _loadingImage.GetComponent<RectTransform>();
        loadingImageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _desiredSize.x*2);
        loadingImageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _desiredSize.y);

        //设置加载中UI的完整图片的旋转动画
        //这里初始会有一定的旋转角度，无伤大雅，暂且搁置
        Sequence loadingSequence = DOTween.Sequence().SetUpdate(true);
        loadingSequence.Append(_loadingImage.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360).SetEase(Ease.Linear));
        loadingSequence.AppendInterval(0.3f);
        loadingSequence.SetLoops(-1);

        //加载场景
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        canContinue = true;

        //加载动画的回调，确保转完一周
        loadingSequence.OnStepComplete(() =>
        {
            if (canContinue)
            {
                loadingSequence.Kill();
                _loadingImage.SetActive(false);

                //开
                _leftLodingCanvasGroup.alpha = 1f;
                _rightLodingCanvasGroup.alpha = 1f;
                Sequence openDoor = DOTween.Sequence().SetUpdate(true);
                openDoor.Join(_doorLeft.DOAnchorPosX(-_doorWidth, _duration).SetEase(_ease));
                openDoor.Join(_doorRight.DOAnchorPosX(_doorWidth, _duration).SetEase(_ease));
                
            }
        });

    }

    //立即打开门的动画
    private void OpenInstant()
    {
        _doorLeft.anchoredPosition = new Vector2(-_doorWidth, 0);
        _doorRight.anchoredPosition = new Vector2(_doorWidth, 0);
    }
}
