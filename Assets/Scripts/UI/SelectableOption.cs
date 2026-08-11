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
/// tips面板为可选项
/// </summary>
public class SelectableOption : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject _left;
    [SerializeField] private GameObject _right;
    [SerializeField] private float _scaleAmount;
    [SerializeField] private GameObject _tips;
    [SerializeField] private bool _isWithSettingPanel;
    private Vector3 _originalScale;
    private RectTransform _optionRectTransform;
    private RectTransform _tipsRectTransform;
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _originalScale = transform.localScale;
        _optionRectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_tips != null)
        {
            _tipsRectTransform = _tips.GetComponent<RectTransform>();
        }
    }

    void OnEnable()
    {
        if (_isWithSettingPanel)
        {
            UIEventCenter.OnSettingPanelOpened += OnSettingPanelOpend;
            UIEventCenter.OnSettingPanelClosed += OnSettingPanelClosed;
        }
    }

    void OnDisable()
    {
        if (_isWithSettingPanel)
        {
            UIEventCenter.OnSettingPanelOpened -= OnSettingPanelOpend;
            UIEventCenter.OnSettingPanelClosed -= OnSettingPanelClosed;
        }
    }

    //实现接口成员
    public void OnPointerEnter(PointerEventData e) => Selected();
    public void OnSelect(BaseEventData e) => Selected();
    public void OnPointerExit(PointerEventData e) => UnSelected();
    public void OnDeselect(BaseEventData e) => UnSelected();

    private void Selected()
    {
        transform.DOKill(true);
        transform.DOScale(_originalScale * _scaleAmount, 0.05f).SetUpdate(true);
        _left.SetActive(true);
        _right.SetActive(true);
        if (_tips != null)
        {
            _tips.SetActive(true);
            _tipsRectTransform.position = new Vector2(_tipsRectTransform.position.x, _optionRectTransform.position.y);

            //适配tips位置，避免超出屏幕范围
            //x轴方向UI布局还有点想法，暂时不做适配.
            if (_tipsRectTransform.position.y + _tipsRectTransform.rect.height / 2f * _tipsRectTransform.lossyScale.y > Screen.height)
            {
                Debug.Log("UI:TipPanel位置重新适配");
                _tipsRectTransform.position = new Vector2(_tipsRectTransform.position.x, Screen.height - _tipsRectTransform.rect.height / 2f * _tipsRectTransform.lossyScale.y);
            }
            else if (_tipsRectTransform.position.y - _tipsRectTransform.rect.height / 2f * _tipsRectTransform.lossyScale.y < 0)
            {
                Debug.Log("UI:TipPanel位置重新适配");
                _tipsRectTransform.position = new Vector2(_tipsRectTransform.position.x, _tipsRectTransform.rect.height / 2f * _tipsRectTransform.lossyScale.y);
            }

        }
    }

    private void UnSelected()
    {
        transform.DOKill(true);
        transform.DOScale(_originalScale, 0.05f).SetUpdate(true);
        _left.SetActive(false);
        _right.SetActive(false);
        if (_tips != null)
        {
            _tips.SetActive(false);
        }
    }

    private void OnSettingPanelOpend()
    {
        _canvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void OnSettingPanelClosed()
    {
        _canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
    }
}
