using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;


/// <summary>
/// 控制形态轮盘的显示与隐藏，以及选中形态的逻辑。
/// 挂载在 PF_UI_FormWheel 根节点上。
/// </summary>
public class FormWheelController : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private GameObject _wheelPanel; // 轮盘面板的引用
    [SerializeField] private KeyCode _activateKey = KeyCode.Tab; // 激活轮盘面板的按键
    [SerializeField] private float _centerRound = 0f; // 中心区域的半径
    [SerializeField] private float _selectionRadius = 1000f; // 选项区域的半径
    [SerializeField] private float _wheelPanelRadius = 1f; // 轮盘面板的以屏幕中心为圆心的的半径比例(以150为基准)
    private int _currentSelection, _previousSelection = -1; // 当前和上一个选中的选项索引
    [SerializeField] private GameObject[] _optionsPositions; // 选项位置的数组，按顺时针顺序排列
    [SerializeField] private GameObject[] _wheelOptions; // 轮盘面板的选项数组[按FormType顺序排列,但第0个为取消区域]
    [SerializeField] private GameObject _borader; // 选项的边框
    [SerializeField] private float _duration = 0.2f; // 动效的持续时间
    [SerializeField] private float _scaleFactor = 1.6f; // 选中选项的缩放因子
    [SerializeField] private TMPro.TMP_Text _selectedOptionText; // 显示选中选项的文本
    private int _optionCount; // 选项数量
    private Vector2 _screenCenter; // 屏幕中心点坐标
    private List<GameObject> _rankedOptions = new List<GameObject>(); // 排序后的选项数组 
    private PlayerController _playerController; // 玩家控制器的引用

    public static List<FormType> unlockedForms = new List<FormType>(); // 已解锁的形态列表

    void OnEnable()
    {
        MockEventCenter.OnFormUnlocked += AddUnlockedForm;
    }

    void Awake()
    {
        //初始化已解锁形态列表和排序后的选项数组

        _rankedOptions.Add(_wheelOptions[0]);//确保存在取消区域，序号为0
        if (unlockedForms.Count == 0)// 如果解锁列表为空，默认解锁Slime形态
        {
            unlockedForms.Add(FormType.Slime); // 默认解锁Slime形态
            _rankedOptions.Add(_wheelOptions[(int)FormType.Slime + 1]); // 将Slime形态对应的选项添加到排序后的选项数组中
        }

        //此处需要和存档模块联动，以读取目前已解锁动物列表
    }

    void Start()
    {
        _optionCount = _wheelOptions.Length; // 获取轮盘面板的选项数量
        _screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        _playerController = FindObjectOfType<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(_activateKey))
        {
            ShowWheelPanel();
        }

        if (Input.GetKey(_activateKey))
        {
            WheelSelect();
        }

        if (Input.GetKeyUp(_activateKey))
        {
            HideWheelPanel();
        }
    }

    void OnDisable()
    {
        MockEventCenter.OnFormUnlocked -= AddUnlockedForm;
    }

    private void ShowWheelPanel()
    {
        _currentSelection = -1; // 重置当前选中的选项索引
        _previousSelection = -1; // 重置上一个选中的选项索引
        _wheelPanel.SetActive(true);
        Time.timeScale = 0f; // 暂停游戏

        for (int i = 0; i < _rankedOptions.Count; i++)//轮盘展开的效果实现
        {
            _rankedOptions[i].SetActive(true);
            _rankedOptions[i].transform.position = _screenCenter; // 将选项移动到屏幕中心
            Vector2 directionToTarget = ((Vector2)_optionsPositions[i].transform.position - (Vector2)_screenCenter) * _wheelPanelRadius; // 计算从屏幕中心到目标位置的方向向量
            _rankedOptions[i].transform
                .DOMove(_screenCenter + directionToTarget, _duration)
                .SetEase(Ease.OutBack) // 使用DoTween平滑移动选项到目标位置
                .SetUpdate(true); // 设置为忽略时间缩放，确保在暂停游戏时仍然可以执行动画
        }

        _borader.SetActive(true);
        _borader.transform.position = _screenCenter; // 将边框移动到屏幕中心
        _borader.transform.localScale = Vector2.one * _scaleFactor; // 放大边框
    }

    private void HideWheelPanel()
    {

        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.DOKill(); // 停止选项的移动动画
        }
        // 选中有效的选项，执行相应的操作
        Debug.Log("UI:Selected Option: " + _currentSelection);
        if (_currentSelection > 0)
        {
            _playerController.SwitchToFormByType((FormType)(_currentSelection - 1)); // 切换到选中的形态，注意索引需要减1，因为取消区域占据了第0个位置
        }

        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.position = _screenCenter; // 重置选项位置到屏幕中心
        }
        _wheelPanel.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏

    }

    private void WheelSelect()
    {
        Vector2 mousePosition = (Vector2)Input.mousePosition;
        Vector2 direction = mousePosition - _screenCenter;
        float distance = direction.magnitude;
        _previousSelection = _currentSelection; // 保存上一个选中的选项索引

        if (distance < _centerRound || distance > _selectionRadius)
        {
            _currentSelection = -1; // 鼠标在中心区域内或超出选项区域，不选择任何选项
            return;
        }
        else
        {
            int tempCount;
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f; // 将角度转换为0到360度之间
            }

            float optionAngle = 360f / _optionCount; // 每个选项的角度范围
            if (Mathf.RoundToInt(angle / optionAngle) >= _optionCount)
            {
                tempCount = 0; // 选择第一个选项
            }
            else
            {
                tempCount = Mathf.RoundToInt(angle / optionAngle); // 根据角度计算选中的选项索引
            }
            if (tempCount >= 0 && tempCount >= _rankedOptions.Count)
            {
                _currentSelection = _optionCount - tempCount > tempCount - _rankedOptions.Count + 1 ? _rankedOptions.Count - 1 : 0; // 如果选中的索引超出排序后的选项数组范围，选择最接近的有效选项
            }
            else
            {
                _currentSelection = tempCount; // 更新当前选中的选项索引
            }
        }

        if (_currentSelection >= 0 && _previousSelection >= 0 && _currentSelection < _rankedOptions.Count && _previousSelection < _rankedOptions.Count)
        {
            _rankedOptions[_currentSelection].transform.DOScale(Vector3.one * _scaleFactor, 0.05f).SetUpdate(true); // 放大当前选中的选项
            _rankedOptions[_previousSelection].transform.DOScale(Vector3.one, 0.05f).SetUpdate(true); // 恢复
            _borader.transform.position = _rankedOptions[_currentSelection].transform.position; // 将边框移动到当前选中的选项位置
            if (_currentSelection > 0)
            {
                _selectedOptionText.text = ((FormType)(_currentSelection - 1)).ToString(); // 更新显示的选中选项文本
            }
            else
            {
                _selectedOptionText.text = "Cancel"; // 如果选择的是取消区域，显示"取消"
            }
        }
    }

    private void AddUnlockedForm(FormType form)
    {
        if (!unlockedForms.Contains(form))
        {
            unlockedForms.Add(form);
            _rankedOptions.Add(_wheelOptions[(int)form + 1]); // 将解锁的形态对应的选项添加到排序后的选项数组中
        }
    }

}
