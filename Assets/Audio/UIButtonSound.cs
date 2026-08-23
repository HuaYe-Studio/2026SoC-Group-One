using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 按钮音效：挂在 Button 上，悬停/点击时播放 UI 音效。
/// 与 SelectableOption 的 hover 效果并存，不冲突。
/// </summary>
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISelectHandler
{
    [Header("UI 音效 key（对应 AudioLibrary 的 uiEntries）")]
    [Tooltip("悬停音效（鼠标移入或键盘选中）")]
    [SerializeField] private string hoverSfxKey = "hover";

    [Tooltip("点击音效（鼠标点击或确认）")]
    [SerializeField] private string clickSfxKey = "click";

    [Tooltip("是否播放悬停音效")]
    [SerializeField] private bool playHover = true;

    [Tooltip("是否播放点击音效")]
    [SerializeField] private bool playClick = true;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playHover) PlayHover();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (playHover) PlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playClick) PlayClick();
    }

    public void PlayHover()
    {
        if (AudioManager.HasInstance && !string.IsNullOrEmpty(hoverSfxKey))
            AudioManager.Instance.PlayUiSfxByKey(hoverSfxKey);
    }

    public void PlayClick()
    {
        if (AudioManager.HasInstance && !string.IsNullOrEmpty(clickSfxKey))
            AudioManager.Instance.PlayUiSfxByKey(clickSfxKey);
    }
}
