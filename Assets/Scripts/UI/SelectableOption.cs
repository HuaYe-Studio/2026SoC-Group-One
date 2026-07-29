using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
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
