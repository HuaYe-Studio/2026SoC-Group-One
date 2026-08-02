using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 自定义的可选选项按钮效果
/// 挂载在button上
/// </summary>
public class SelectableOption : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    [SerializeField] private GameObject _left;
    [SerializeField] private GameObject _right;
    [SerializeField] private float _scaleAmount;
    private Vector3 _originalScale;

    void Awake()
    {
        _originalScale = transform.localScale;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    //实现接口成员
    public void OnPointerEnter(PointerEventData e) => Selected();
    public void OnSelect(BaseEventData e) => Selected();
    public void OnPointerExit(PointerEventData e) => UnSelected();
    public void OnDeselect(BaseEventData e) => UnSelected();

    private void Selected()
    {
        transform.DOKill(true);
        transform.DOScale(_originalScale * _scaleAmount,0.05f).SetUpdate(true);
        _left.SetActive(true);
        _right.SetActive(true);
    }

    private void UnSelected()
    {
        transform.DOKill(true);
        transform.DOScale(_originalScale,0.05f).SetUpdate(true);
        _left.SetActive(false);
        _right.SetActive(false);
    }
}
