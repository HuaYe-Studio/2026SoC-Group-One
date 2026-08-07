using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 控制形态轮盘的显示与隐藏，以及选中形态的逻辑。
/// 挂载在 PF_UI_FormWheel 根节点上。
/// </summary>
public class FormWheelController : MonoBehaviour
{
    [SerializeField] private GameObject _wheelPanel;                // 轮盘面板的引用
    [SerializeField] private float _wheelPanelRadius = 1f;          // 轮盘面板的以屏幕中心为圆心的半径比例
    [SerializeField] private GameObject[] _wheelOptions;            // 轮盘面板的选项数组[按FormType顺序排列,但第0个为取消区域]
    [SerializeField] private GameObject _borader;                   // 选项的边框
    [SerializeField] private float _duration = 0.2f;                // 动效的持续时间
    [SerializeField] private float _scaleFactor = 1.6f;             // 选中选项的缩放因子
    [SerializeField] private TMPro.TMP_Text _selectedOptionText;    // 显示选中选项的文本
    [SerializeField] private bool _isKeyBoard;//是否切换到键盘输入，这里以后还要和全局设置做适配
    [SerializeField] private GameObject _arrow;
    private int _currentSelection, _previousSelection = -1;
    private Vector2 _screenCenter;//屏幕中心坐标
    private List<GameObject> _rankedOptions = new List<GameObject>();
    public static List<FormType> unlockedFormTypes = new List<FormType>();
    private Dictionary<FormType, GameObject> _formTypeToWheelOption = new Dictionary<FormType, GameObject>();
    private PlayerController _playerController;
    private bool _isWheelOpen;
    float _angle = 0f;
    
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

        // 初始化已解锁形态列表和排序后的选项数组
        // 此处需要和存档模块联动，以读取目前已解锁动物列表，暂时简略处理
        Debug.Log("UI:存档系统暂时未接入，暂时使用默认解锁形态列表");
        _rankedOptions.Add(_wheelOptions[0]); // 取消区域，序号0

        if (unlockedFormTypes.Count == 0)
        {
            if (_formTypeToWheelOption.TryGetValue(FormType.Slime, out var slimeOption))
            {
                unlockedFormTypes.Add(FormType.Slime);
                _rankedOptions.Add(slimeOption);
            }
        }

    }

    void Start()
    {
        _screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        _playerController = FindObjectOfType<PlayerController>();
    }

    void OnEnable()
    {
        MockEventCenter.OnFormUnlocked += AddUnlockedForm;
        UIEventCenter.OnSceneChanged += ReSetWheelOptions;

        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAnimalWheelStarted += ShowWheelPanel;
            PlayerInputReader.Instance.OnAnimalWheelCanceled += HideWheelPanel;
        }
    }

    void OnDisable()
    {
        MockEventCenter.OnFormUnlocked -= AddUnlockedForm;
        UIEventCenter.OnSceneChanged -= ReSetWheelOptions;

        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAnimalWheelStarted -= ShowWheelPanel;
            PlayerInputReader.Instance.OnAnimalWheelCanceled -= HideWheelPanel;
        }

        // 确保退出时恢复时间缩放
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
    }

    void Update()
    {
        if (_isWheelOpen)
            WheelSelect();
    }

    // ---------- 核心方法 ----------
    private void ShowWheelPanel()
    {
        if (!_isKeyBoard)
        {
            Cursor.lockState = CursorLockMode.None;//解锁鼠标
            Cursor.visible = true;//鼠标可视
            _arrow.SetActive(false);
        }
        else
        {
            _arrow.SetActive(true);
        }

        _screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);//更新屏幕中心的位置
        _currentSelection = -1;
        _previousSelection = -1;
        _isWheelOpen = true;
        _wheelPanel.SetActive(true);
        float radius = _wheelPanelRadius * (150f / 1440f) * Screen.height;//计算轮盘半径
        float anglePerOption = 360f / _wheelOptions.Length;
        Time.timeScale = 0f; // 暂停游戏

        // 展开动画：每个选项从中心移动到目标位置
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            float angle = anglePerOption * i * Mathf.Deg2Rad;
            Vector2 targetPosition = _screenCenter + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;//获得选项的目标位置，这里的（x，y）三角函数坐标没问题

            _rankedOptions[i].SetActive(true);
            _rankedOptions[i].transform.position = _screenCenter;
            _rankedOptions[i].transform
                .DOMove(targetPosition, _duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true); // 忽略 Time.timeScale
        }

        _borader.SetActive(true);
        _borader.transform.position = _screenCenter;
        _borader.transform.localScale = Vector2.one * _scaleFactor;
    }

    private void HideWheelPanel()
    {
        _isWheelOpen = false;

        // 停止所有移动动画
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.DOKill();
        }

        // 执行选中逻辑
        if (_currentSelection > 0 && _currentSelection < _rankedOptions.Count)
        {
            FormType selectedForm = unlockedFormTypes[_currentSelection - 1];
            Debug.Log($"UI: Selected Form: {selectedForm}");
            _playerController.SwitchToFormByType(selectedForm);
        }
        else
        {
            Debug.Log("UI: Cancel selection");
        }

        // 重置选项位置到中心（隐藏）
        for (int i = 0; i < _rankedOptions.Count; i++)
        {
            _rankedOptions[i].transform.position = _screenCenter;
        }

        _wheelPanel.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏

        if (!_isKeyBoard)
        {
            Cursor.lockState = CursorLockMode.Locked;//锁住鼠标
            Cursor.visible = false;//鼠标隐藏
        }
    }

    private void WheelSelect()
    {
        _previousSelection = _currentSelection;
        int totalCount = _wheelOptions.Length;
        float optionAngle = 360f / totalCount;

        int tempCount;

        if (!_isKeyBoard)//鼠标输入
        {
            Vector2 mousePosition = PlayerInputReader.Instance.MouseScreenPosition;
            Vector2 direction = mousePosition - _screenCenter;
            //float distance = direction.magnitude;//暂时为死代码

            // 计算鼠标角度（0~360）
            _angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            if (_angle < 0f) _angle += 360f;

            tempCount = Mathf.RoundToInt(_angle / optionAngle);
            if (tempCount >= totalCount) tempCount = 0;

            //选项夹紧逻辑
            if (tempCount >= _rankedOptions.Count)
            {
                _currentSelection = (totalCount - tempCount > tempCount - _rankedOptions.Count + 1)
                    ? _rankedOptions.Count - 1
                    : 0;
            }
            else
            {
                _currentSelection = tempCount;
            }
        }
        else //键盘输入
        {
            //此处还要和按键输入映射做适配，暂且先用旧版
            if (Input.GetKeyDown(KeyCode.L))
            {
                _angle += optionAngle;
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                _angle -= optionAngle;
            }
            _angle = _angle % 360f;
            if (_angle < 0)
            {
                _angle = (_rankedOptions.Count - 1) * optionAngle;
            }

            tempCount = Mathf.RoundToInt(_angle / optionAngle);
            if (tempCount >= totalCount) tempCount = 0;

            if (tempCount >= _rankedOptions.Count)
            {
                tempCount = 0;
                _angle = 0;
            }
            _currentSelection = tempCount;

        }

        // 更新 UI 缩放和边框位置
        if (_currentSelection >= 0 && _previousSelection >= 0 &&
            _currentSelection < _rankedOptions.Count && _previousSelection < _rankedOptions.Count)
        {
            // 放大当前选中，恢复上一个
            _rankedOptions[_currentSelection].transform.DOScale(Vector3.one * _scaleFactor, 0.05f).SetUpdate(true);
            _rankedOptions[_previousSelection].transform.DOScale(Vector3.one, 0.05f).SetUpdate(true);
            _borader.transform.position = _rankedOptions[_currentSelection].transform.position;

            if (_isKeyBoard)
            {
                _arrow.transform.rotation = Quaternion.Euler(0, 0, -_angle);
            }

            // 更新文字
            if (_currentSelection > 0)
                _selectedOptionText.text = unlockedFormTypes[_currentSelection - 1].ToString();
            else
                _selectedOptionText.text = "Cancel";
        }
    }

    // ---------- 事件监听：解锁新形态 ----------
    private void AddUnlockedForm(FormType form)
    {
        if (!unlockedFormTypes.Contains(form) && _formTypeToWheelOption.TryGetValue(form, out var option))
        {
            unlockedFormTypes.Add(form);
            _rankedOptions.Add(option);
        }
    }

    //清空相关数组，一遍下次存档读取覆盖
    private void ReSetWheelOptions(string fromSceneName, string toScene)
    {
        _rankedOptions.Clear();
        unlockedFormTypes.Clear();
    }
}
