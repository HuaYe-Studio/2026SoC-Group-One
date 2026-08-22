using UnityEngine;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 控制“已存档”提示的显示与隐藏。
/// 挂载在 PF_UI_SaveTip 根节点上。
/// </summary>
public class SaveTipController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;//提示整体的CanvasGroup

    private Sequence _saveSequence;

    void Awake()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    //订阅存档完成事件，并确保初始为隐藏状态
    void OnEnable()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        UIEventCenter.OnSaveCompleted += HandleSaveCompleted;
    }

    //退订存档完成事件，并清理动画
    void OnDisable()
    {
        UIEventCenter.OnSaveCompleted -= HandleSaveCompleted;

        if (_saveSequence != null)
        {
            _saveSequence.Kill();
            _saveSequence = null;
        }
    }

    //存档完成后播放提示：淡入0.15s → 完整显示1.0s → 淡出0.15s
    private void HandleSaveCompleted()
    {
        if (_canvasGroup == null) return;

        //连续触发时清理旧动画，从当前透明度重新进入本次显示流程
        if (_saveSequence != null)
        {
            _saveSequence.Kill();
            _saveSequence = null;
        }

        _saveSequence = DOTween.Sequence()
            .SetUpdate(true)//不受Time.timeScale影响
            .Append(_canvasGroup.DOFade(1f, 0.15f))
            .AppendInterval(1f)
            .Append(_canvasGroup.DOFade(0f, 0.15f))
            .OnComplete(() => _saveSequence = null);
    }
}
