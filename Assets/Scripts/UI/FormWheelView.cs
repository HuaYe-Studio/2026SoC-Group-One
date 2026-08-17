using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 形态轮盘UI视图，负责轮盘的显示、展开、关闭和选中视觉。
/// 挂载在 PF_UI_FormWheel 根节点上。
/// </summary>
public class FormWheelView : MonoBehaviour
{
    [SerializeField] private GameObject _wheelPanel;                // 轮盘面板的引用
    [SerializeField] private float _wheelPanelRadius = 1f;          // 轮盘面板的以屏幕中心为圆心的半径比例
    [SerializeField] private GameObject[] _wheelOptions;            // 轮盘面板的选项数组[按FormType顺序排列,但第0个为取消区域]
    [SerializeField] private GameObject _borader;                   // 选项的边框
    [SerializeField] private float _duration = 0.2f;                // 动效的持续时间
    [SerializeField] private float _scaleFactor = 1.6f;             // 选中选项的缩放因子
    [SerializeField] private TMPro.TMP_Text _selectedOptionText;    // 显示选中选项的文本
    [SerializeField] private GameObject _arrow;
    private List<GameObject> _rankedOptions = new List<GameObject>();
    private List<FormType> _rankedFormTypes = new List<FormType>();
    private Dictionary<FormType, GameObject> _formTypeToWheelOption = new Dictionary<FormType, GameObject>();
    private Dictionary<Transform, Tween> _optionScaleTweens = new Dictionary<Transform, Tween>();

    // ---------- Unity 生命周期 ----------
    void Awake()
    {
        // 按 FormType 枚举顺序构建映射：索引0=取消，索引1起对应 (int)FormType + 1
        foreach (FormType ft in System.Enum.GetValues(typeof(FormType)))
        {
            int idx = (int)ft + 1;
            if (idx < _wheelOptions.Length)
                _formTypeToWheelOption[ft] = _wheelOptions[idx];
        }
    }

    // ---------- 公开接口 ----------
    // 总槽位数量
    public int GetTotalSlotCount()
    {
        return _wheelOptions.Length;
    }

    // 当前可见选项数量
    public int GetVisibleOptionCount()
    {
        return _rankedOptions.Count;
    }

    // 根据传入的有序形态列表重建可见选项
    public void RebuildOptions(List<FormType> forms)
    {
        _rankedOptions.Clear();
        _rankedFormTypes.Clear();

        _rankedOptions.Add(_wheelOptions[0]); // 取消区域，序号0

        foreach (FormType form in forms)
        {
            AddUnlockedForm(form);
        }
    }

    // 按解锁事件追加一个尚未存在的形态
    public void AddUnlockedForm(FormType form)
    {
        if (_rankedOptions.Count == 0)
            _rankedOptions.Add(_wheelOptions[0]); // 取消区域，序号0

        if (!_rankedFormTypes.Contains(form) && _formTypeToWheelOption.TryGetValue(form, out var option))
        {
            _rankedFormTypes.Add(form);
            _rankedOptions.Add(option);
        }
    }

    // 清空当前可见选项快照
    public void ClearOptions()
    {
        _rankedOptions.Clear();
        _rankedFormTypes.Clear();
    }

    // 根据选择索引取得对应FormType；Cancel和非法索引返回失败
    public bool TryGetFormByIndex(int index, out FormType form)
    {
        form = default;
        if (index <= 0 || index > _rankedFormTypes.Count)
            return false;

        form = _rankedFormTypes[index - 1];
        return true;
    }

    // ---------- 核心方法 ----------
    // 按现有算法展开轮盘
    public void ShowWheelPanel(bool isKeyBoard)
    {
        _arrow.SetActive(isKeyBoard);

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);//更新屏幕中心的位置
        _wheelPanel.SetActive(true);
        float radius = _wheelPanelRadius * (150f / 1440f) * Screen.height;//计算轮盘半径
        float anglePerOption = 360f / _wheelOptions.Length;

        // 展开动画：每个选项从中心移动到目标位置
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            float angle = anglePerOption * i * Mathf.Deg2Rad;
            Vector2 targetPosition = screenCenter + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;//获得选项的目标位置，这里的（x，y）三角函数坐标没问题

            _rankedOptions[i].SetActive(true);
            _rankedOptions[i].transform.position = screenCenter;
            _rankedOptions[i].transform
                .DOMove(targetPosition, _duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true); // 忽略时间缩放
        }

        _borader.SetActive(true);
        _borader.transform.position = screenCenter;
        _borader.transform.localScale = Vector2.one * _scaleFactor;
    }

    // 按现有算法关闭、清理Tween、位置和缩放
    public void HideWheelPanel()
    {
        // 停止所有移动动画并清理缩放残留
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.DOKill();
            _rankedOptions[i].transform.localScale = Vector3.one;
        }
        _optionScaleTweens.Clear();

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);//更新屏幕中心的位置
        // 重置选项位置到中心（隐藏）
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.position = screenCenter;
        }

        _wheelPanel.SetActive(false);
    }

    // 按当前已验收算法更新选择视觉
    public void UpdateSelectionVisual(int previousSelection, int currentSelection, float angle, bool isKeyBoard)
    {
        // 更新 UI 缩放和文字；仅当候选索引变化时更新，避免每帧重复创建Tween
        if (currentSelection != previousSelection)
        {
            // 恢复上一个选中项
            if (previousSelection >= 0 && previousSelection < _rankedOptions.Count)
            {
                Transform previousTransform = _rankedOptions[previousSelection].transform;
                KillOptionScaleTween(previousTransform);
                _optionScaleTweens[previousTransform] = previousTransform.DOScale(Vector3.one, 0.05f).SetUpdate(true);
            }

            // 放大当前选中项
            if (currentSelection >= 0 && currentSelection < _rankedOptions.Count)
            {
                Transform currentTransform = _rankedOptions[currentSelection].transform;
                KillOptionScaleTween(currentTransform);
                _optionScaleTweens[currentTransform] = currentTransform.DOScale(Vector3.one * _scaleFactor, 0.05f).SetUpdate(true);

                // 更新文字
                if (currentSelection > 0)
                    _selectedOptionText.text = _rankedFormTypes[currentSelection - 1].ToString();
                else
                    _selectedOptionText.text = "Cancel";
            }
        }

        // 每帧同步边框位置和箭头方向；不依赖索引是否变化
        if (currentSelection >= 0 && currentSelection < _rankedOptions.Count)
        {
            _borader.transform.position = _rankedOptions[currentSelection].transform.position;

            if (isKeyBoard)
            {
                _arrow.transform.rotation = Quaternion.Euler(0, 0, -angle);
            }
        }
    }

    // 仅终止某个选项的缩放Tween，不影响同一Transform上的移动Tween
    private void KillOptionScaleTween(Transform target)
    {
        if (_optionScaleTweens.TryGetValue(target, out Tween scaleTween) && scaleTween != null && scaleTween.IsActive())
        {
            scaleTween.Kill();
        }
        _optionScaleTweens.Remove(target);
    }
}
