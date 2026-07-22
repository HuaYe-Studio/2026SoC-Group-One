using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FormProgressController : MonoBehaviour
{
    [SerializeField] private Image _formImage;//形态图标
    [SerializeField] private Image _roundBorader;//圆形边框
    [SerializeField] private Sprite[] _formIcons;//按照FormType枚举的顺序拖入图标
    [SerializeField] private float _scaleFactor;//缩放因子
    [SerializeField] private float _duration = 0.3f;//动效持续时间
    [SerializeField] private float _elasticity = 0.5f;//弹性

    void OnEnable()
    {
        MockEventCenter.OnFormChanged += FormChanged;
    }

    void Awake()
    {
        //这里要和存档系统联动，读取开始的形态
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
        MockEventCenter.OnFormChanged -=FormChanged;       
    }

    private void FormChanged(FormType formType)
    {
        _formImage.sprite = _formIcons[(int)formType];
        _formImage.transform.DOPunchScale(Vector3.one*(_scaleFactor-1),_duration,1,_elasticity).SetUpdate(true);
    }
}
