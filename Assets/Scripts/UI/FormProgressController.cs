using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Linq;
using System;
using System.Threading;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求


/// <summary>
/// 控制屏幕左上角的当前形态图标以及解锁进度图标的脚本
/// 挂载在PF_UI_FormProgress根节点上
/// 将该预制体拖拽到场景中时，务必保证场景中同时存在PF_UI_FormWheel
/// </summary>
public class FormProgressController : MonoBehaviour
{
    [SerializeField] private Image _formImage;//形态图标
    [SerializeField] private Image _roundBorader;//圆形边框
    [SerializeField] private Sprite[] _formIcons;//按照FormType枚举的顺序拖入图标
    [SerializeField] private Image[] _progressImage;//progress图像，按渐进顺序拖入
    [SerializeField] private Sprite _offImage;//off状态下的progress图标
    [SerializeField] private Sprite _onImage;//on状态下的progress图标
    [SerializeField] private float _scaleFactor;//缩放因子
    [SerializeField] private float _duration = 0.3f;//动效持续时间
    [SerializeField] private float _elasticity = 0.5f;//弹性
    [SerializeField] private float _radius = 90f;//progress排布半径

    void OnEnable()
    {
        MockEventCenter.OnFormChanged += FormChanged;
        MockEventCenter.OnFormUnlocked += ProgressUpdate;
    }

    void Awake()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        RadialArrangement(50,210,_radius,_roundBorader.transform.position,_progressImage);
        ProgressUpdate(FormType.Slime);//初始更新

        //这里要和存档系统联动，读取开始的形态，目前默认slime
        _formImage.sprite = _formIcons[(int)FormType.Slime];
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnDisable()
    {
        MockEventCenter.OnFormChanged -= FormChanged;
        MockEventCenter.OnFormUnlocked -= ProgressUpdate;
    }

    private void FormChanged(FormType formType)
    {
        _formImage.transform.DOKill(true);
        _formImage.sprite = _formIcons[(int)formType];

        //日后切换形态过程的动画做好后这里有可能，可能，要做适配
        _formImage.transform.DOPunchScale(Vector3.one * (_scaleFactor - 1), _duration, 1, _elasticity).SetUpdate(true);
    }

    private void ProgressUpdate(FormType formType)
    {
        int count = FormWheelController.unlockedFormTypes.Count;
        for (int i = 0; i < _progressImage.Length; i++)
        {
            _progressImage[i].sprite = i<count? _onImage:_offImage;
        }
    }

    //扇形排布方法，以+Y为0点，顺时针为正，传入起始角度，终点角度，半径，中心，以及需要排布的Object数组
    //如有需要，可单独写在一个脚本里作公共方法
    private void RadialArrangement(float startAngle,float endAngle,float radius,Vector2 center,GameObject[] gameObjects)
    {
        int count = gameObjects.Length;
        float angleStep = (endAngle - startAngle)/(count - 1);

        float angle;
        float rad;
        for(int i = 0;i<count;i++)
        {
            angle = startAngle + angleStep * i;
            rad = angle *Mathf.Deg2Rad;

            gameObjects[i].transform.position = center + new Vector2(Mathf.Sin(rad),Mathf.Cos(rad))*(radius/1440*Screen.height);
            gameObjects[i].transform.rotation = Quaternion.Euler(0,0,-angle);
        }
    }

    //针对组件对象的重载
    private void RadialArrangement(float startAngle,float endAngle,float radius,Vector2 center,Component[] Components)
    {
        int count = Components.Length;
        float angleStep = (endAngle - startAngle)/(count - 1);

        float angle;
        float rad;
        for(int i = 0;i<count;i++)
        {
            angle = startAngle + angleStep * i;
            rad = angle *Mathf.Deg2Rad;

            Components[i].transform.position = center + new Vector2(Mathf.Sin(rad),Mathf.Cos(rad))*(radius/1440*Screen.height);
            Components[i].transform.rotation = Quaternion.Euler(0,0,-angle);
        }
    }
}
