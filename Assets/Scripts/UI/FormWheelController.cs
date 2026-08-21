using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 控制形态轮盘的显示与隐藏，以及选中形态的逻辑。
/// 挂载在 PF_UI_FormWheel 根节点上。
/// </summary>
public class FormWheelController : MonoBehaviour
{
    [SerializeField] private bool _isKeyBoard;//是否切换到键盘输入，这里以后还要和全局设置做适配
    private int _currentSelection, _previousSelection = -1;
    private Vector2 _screenCenter;//屏幕中心坐标
    public static List<FormType> unlockedFormTypes = new List<FormType>();
    private FormWheelView _formWheelView;
    private PlayerController _playerController;
    private bool _isWheelOpen;
    float _angle = 0f;
    
    // ---------- Unity 生命周期 ----------
    void Awake()
    {
        _formWheelView = GetComponent<FormWheelView>();
    }

    void Start()
    {
        _playerController = FindObjectOfType<PlayerController>();

        // 用玩家真实解锁列表覆盖静态列表，保持接口返回顺序并去重
        unlockedFormTypes.Clear();
        if (_playerController != null)
        {
            List<FormType> playerUnlockedForms = _playerController.GetUnlockedForms();
            foreach (FormType form in playerUnlockedForms)
            {
                if (!unlockedFormTypes.Contains(form))
                    unlockedFormTypes.Add(form);
            }
        }
        if (unlockedFormTypes.Count == 0)
            unlockedFormTypes.Add(FormType.Slime);

        _formWheelView.RebuildOptions(unlockedFormTypes);

        _screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
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
        }

        _screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);//更新屏幕中心的位置
        _currentSelection = -1;
        _previousSelection = -1;
        _isWheelOpen = true;
        Time.timeScale = 0f; // 暂停游戏

        _formWheelView.ShowWheelPanel(_isKeyBoard);
    }

    private void HideWheelPanel()
    {
        _isWheelOpen = false;

        // 取得本次选择结果
        bool hasSelectedForm = _formWheelView.TryGetFormByIndex(_currentSelection, out FormType selectedForm);

        _formWheelView.HideWheelPanel();

        // 执行选中逻辑
        if (hasSelectedForm)
        {
            Debug.Log($"UI: Selected Form: {selectedForm}");
            _playerController.SwitchToFormByType(selectedForm);
        }
        else
        {
            Debug.Log("UI: Cancel selection");
        }

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
        int totalCount = _formWheelView.GetTotalSlotCount();
        int visibleOptionCount = _formWheelView.GetVisibleOptionCount();
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
            if (tempCount >= visibleOptionCount)
            {
                _currentSelection = (totalCount - tempCount > tempCount - visibleOptionCount + 1)
                    ? visibleOptionCount - 1
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
                _angle = (visibleOptionCount - 1) * optionAngle;
            }

            tempCount = Mathf.RoundToInt(_angle / optionAngle);
            if (tempCount >= totalCount) tempCount = 0;

            if (tempCount >= visibleOptionCount)
            {
                tempCount = 0;
                _angle = 0;
            }
            _currentSelection = tempCount;

        }

        _formWheelView.UpdateSelectionVisual(_previousSelection, _currentSelection, _angle, _isKeyBoard);
    }

    // ---------- 事件监听：解锁新形态 ----------
    private void AddUnlockedForm(FormType form)
    {
        if (!unlockedFormTypes.Contains(form))
        {
            unlockedFormTypes.Add(form);
            _formWheelView.AddUnlockedForm(form);
        }
    }

    //清空相关数组，一遍下次存档读取覆盖
    private void ReSetWheelOptions(string fromSceneName, string toScene)
    {
        _formWheelView.ClearOptions();
        unlockedFormTypes.Clear();
    }
}
