using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 加载界面控制器，负责粗=存读档界面相关
/// 目前存档系统暂未完成，这里只负责部分UI逻辑，其余等待将来继续完成
/// </summary>
public class LoadPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _panelCanvasGroup; // 遮罩面板的 CanvasGroup 组件
    [SerializeField] private CanvasGroup _confirmDeletePanel;//确认删除存档面板的CanvasGroup组件
    [SerializeField] private Button _loadButton;//读取存档按钮
    [SerializeField] private Button _deleteButton;//删除存档按钮
    [SerializeField] private TMPro.TMP_Text _loadButtonText;//读取存档按钮上的TMP文字
    [SerializeField] private Image _previewImage;//存档截图预览Image
    [SerializeField] private TMPro.TMP_Text _dateText;//存档日期文本
    [SerializeField] private TMPro.TMP_Text _formText;//形态进度文本
    [SerializeField] private TMPro.TMP_Text _infoText;//存档信息文本

    private const int TotalFormCount = 4;
    private Texture2D _previewTexture;
    private Sprite _previewSprite;

    public void ShowConfirmDeletePanel()
    {
        _confirmDeletePanel.DOFade(1f, 0.2f).SetUpdate(true);
        _confirmDeletePanel.blocksRaycasts = true;
        _confirmDeletePanel.interactable = true;

        _panelCanvasGroup.DOFade(0.4f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = false;
        _panelCanvasGroup.interactable = false;
    }

    public void HideConfirmDeletePanel()
    {
        _confirmDeletePanel.DOFade(0f, 0.2f).SetUpdate(true);
        _confirmDeletePanel.blocksRaycasts = false;
        _confirmDeletePanel.interactable = false;

        _panelCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = true;
        _panelCanvasGroup.interactable = true;
    }

    public void ShowLoadPanel()
    {
        RefreshSaveState();
        UIEventCenter.TriggerPanelOpened();
        Time.timeScale = 0f;
        _panelCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = true;
        _panelCanvasGroup.interactable = true;
    }

    public void HideLoadPanel()
    {
        _panelCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
        _panelCanvasGroup.blocksRaycasts = false;
        _panelCanvasGroup.interactable = false;
        Time.timeScale = 1f;
        UIEventCenter.TriggerPanelClosed();
    }

    public void LoadSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager.Instance 为空，无法读取存档");
            RefreshSaveState();
            return;
        }

        if (SaveManager.Instance.LoadAndApplySave())
        {
            // SaveManager 已负责恢复 Time.timeScale 并加载存档场景
            return;
        }

        RefreshSaveState();
    }

    public void ConfirmDeleteSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager.Instance 为空，无法删除存档");
            return;
        }

        if (SaveManager.Instance.DeleteSave())
        {
            HideConfirmDeletePanel();
            RefreshSaveState();
        }
        // 删除失败时保持确认层打开，方便用户重试
    }

    // 刷新单槽存档状态：读取/删除按钮可用性和读取按钮文字
    private void RefreshSaveState()
    {
        if (SaveManager.Instance == null)
        {
            _loadButton.interactable = false;
            _deleteButton.interactable = false;
            _loadButtonText.text = "存档系统未就绪";
            ClearPreview();
            ClearSaveDetails();
            Debug.LogError("SaveManager.Instance 为空，无法刷新读档面板状态");
            return;
        }

        if (!SaveManager.Instance.HasSave())
        {
            _loadButton.interactable = false;
            _deleteButton.interactable = false;
            _loadButtonText.text = "暂无存档";
            ClearPreview();
            ClearSaveDetails();
            return;
        }

        if (!SaveManager.Instance.HasValidSave())
        {
            _loadButton.interactable = false;
            _deleteButton.interactable = true;
            _loadButtonText.text = "存档不可用";
            ClearPreview();
            ClearSaveDetails();
            return;
        }

        _loadButton.interactable = true;
        _deleteButton.interactable = true;
        _loadButtonText.text = "读取存档";
        RefreshPreview();

        SaveData data = SaveManager.Instance.LoadRawData();
        if (data != null)
            RefreshSaveDetails(data);
        else
            ClearSaveDetails();
    }

    // 读取并显示存档截图；失败时清空预览
    private void RefreshPreview()
    {
        ClearPreview();

        if (SaveManager.Instance == null || !SaveManager.Instance.HasSave() || !SaveManager.Instance.HasValidSave())
            return;

        if (!SaveManager.Instance.TryLoadPreview(out byte[] imageBytes))
            return;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!texture.LoadImage(imageBytes))
        {
            Destroy(texture);
            return;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        _previewTexture = texture;
        _previewSprite = sprite;
        _previewImage.sprite = sprite;
        _previewImage.gameObject.SetActive(true);
    }

    // 清空并释放预览相关运行时资源
    private void ClearPreview()
    {
        if (_previewSprite != null)
        {
            Destroy(_previewSprite);
            _previewSprite = null;
        }
        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
            _previewTexture = null;
        }
        if (_previewImage != null)
        {
            _previewImage.sprite = null;
            _previewImage.gameObject.SetActive(false);
        }
    }

    // 刷新存档详情文本：日期、形态进度、关卡名称
    private void RefreshSaveDetails(SaveData data)
    {
        if (data == null)
        {
            ClearSaveDetails();
            return;
        }

        _dateText.text = FormatSaveTime(data.saveTime);
        int formCount = Mathf.Clamp(data.unlockedForms != null ? data.unlockedForms.Count : 0, 0, TotalFormCount);
        _formText.text = $"{formCount}/{TotalFormCount}";
        _infoText.text = FormatSceneName(data.sceneName);
    }

    // 清空并恢复存档详情默认文本
    private void ClearSaveDetails()
    {
        if (_dateText != null)
            _dateText.text = "0000/00/00\n00:00";
        if (_formText != null)
            _formText.text = $"0/{TotalFormCount}";
        if (_infoText != null)
            _infoText.text = "暂无";
    }

    // 将ISO时间字符串格式化为两行日期时间
    private string FormatSaveTime(string saveTime)
    {
        if (string.IsNullOrEmpty(saveTime))
            return "0000/00/00\n00:00";

        if (System.DateTimeOffset.TryParse(saveTime, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTimeOffset time))
            return time.ToString("yyyy/MM/dd\nHH:mm", System.Globalization.CultureInfo.InvariantCulture);

        return "0000/00/00\n00:00";
    }

    // 仅在显示层转换关卡名称
    private string FormatSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return "暂无";
        if (sceneName == "WetLand_Main")
            return "湿地";
        return sceneName;
    }

    private void OnDestroy()
    {
        ClearPreview();
    }
}
