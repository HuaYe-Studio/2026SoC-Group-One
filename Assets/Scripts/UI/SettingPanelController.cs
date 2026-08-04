using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 设置面板控制器，负责显示和隐藏设置面板，以及处理音量和画面设置的逻辑。
/// 挂载在 PF_UI_SettingPanel 根节点上。
/// </summary>
public class SettingPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _panelCanvasGroup; // 遮罩面板的 CanvasGroup 组件

    [Header("音量滑动条")]
    [SerializeField] private Slider _masterVolumeSlider;// 主音量滑动条
    [SerializeField] private TMP_Text _masterVolumeValueText;// 主音量数值文本

    [Header("画面设置")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;// 分辨率下拉框
    [SerializeField] private Toggle _fullscreenToggle;// 全屏切换开关

    private Resolution[] _availableResolutions;// 可用分辨率数组
    private List<string> _resolutionOptions;// 分辨率选项文本列表

    // Start is called before the first frame update
    void Start()
    {
        _masterVolumeSlider.value = AudioListener.volume;// 初始化主音量滑动条的值为当前音量
        _masterVolumeValueText.text = Mathf.RoundToInt(_masterVolumeSlider.value * 100).ToString();

        _availableResolutions = Screen.resolutions;// 获取可用分辨率列表
        _resolutionOptions = new List<string>();// 初始化分辨率选项列表
        foreach(var res in _availableResolutions)
        {
            string option = res.width + " x " + res.height;// 构建分辨率选项文本
            if (!_resolutionOptions.Contains(option)) // 避免重复分辨率
            {
                _resolutionOptions.Add(option);
            }
        }
        _resolutionDropdown.ClearOptions();// 清空下拉框选项
        _resolutionDropdown.AddOptions(_resolutionOptions);// 将分辨率选项添加到下拉框中

        int currentResolutionIndex = _resolutionOptions.IndexOf(Screen.currentResolution.width + " x " + Screen.currentResolution.height);// 获取当前分辨率在选项列表中的索引,若需要模糊匹配则可以改成FindIndex
        _resolutionDropdown.value = currentResolutionIndex >= 0 ? currentResolutionIndex : 0; // 如果当前分辨率不在列表中，则默认选择第一个选项

        _fullscreenToggle.isOn = Screen.fullScreen;// 初始化全屏切换开关的状态为当前全屏状态

        //事件绑定
        _masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);// 音量滑动条值变化时更新音量
        _resolutionDropdown.onValueChanged.AddListener(SetResolution);// 分辨率下拉框值变化时调用 SetResolution 方法
        _fullscreenToggle.onValueChanged.AddListener(f => Screen.fullScreen = f);// 全屏切换开关值变化时更新全屏状态
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowSettingPanel()
    {
        Time.timeScale = 0f;
        _panelCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = true;
        _panelCanvasGroup.interactable = true;
    }

    public void HideSettingPanel()
    {
        _panelCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = false;
        _panelCanvasGroup.interactable = false;
        Time.timeScale = 1f;
    }

    private void SetResolution(int index)
    {
        if (index >= 0 && index < _resolutionOptions.Count)
        {
            string[] resParts = _resolutionOptions[index].Split('x');
            int width = int.Parse(resParts[0].Trim());
            int height = int.Parse(resParts[1].Trim());
            Screen.SetResolution(width, height, Screen.fullScreen);
            Debug.Log($"UI:Resolution set to: {width} x {height}, Fullscreen: {Screen.fullScreen}");
        }
    }

    private void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        _masterVolumeValueText.text = Mathf.RoundToInt(value * 100).ToString();
    }
}
