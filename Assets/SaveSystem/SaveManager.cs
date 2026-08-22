using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    // ---------- 单例（全局唯一访问点） ----------
    public static SaveManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string PreviewPath => Path.Combine(Application.persistentDataPath, "save_preview.jpg");

    // 缓存读档数据，用于场景加载完成后恢复
    private SaveData _pendingSaveData = null;
    private Coroutine _screenshotCoroutine;
    private float _savePointSuppressEndTime;

    private void Awake()
    {
        // 单例初始化：如果不存在就创建，存在就销毁多余的
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 切换场景时不销毁
            SceneManager.sceneLoaded += OnSceneLoaded;// 监听场景加载完成事件
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // 只有真正的单例销毁时才退订并清空Instance
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    //场景加载完成后自动调用
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 没有待恢复的存档数据，直接返回
        if (_pendingSaveData == null)
            return;

        // 待应用存档的目标场景必须与当前加载场景一致，
        // 否则说明转场被中断/偏离，丢弃残留数据，避免误应用到错误场景
        if (scene.name != _pendingSaveData.sceneName)
        {
            Debug.LogWarning($"待应用存档目标场景 {_pendingSaveData.sceneName} 与当前场景 {scene.name} 不符，已丢弃残留存档数据");
            _pendingSaveData = null;
            return;
        }

        // 有匹配的待恢复存档数据，应用它
        ApplySaveData(_pendingSaveData);
        Debug.Log($"场景重载完成，玩家已恢复至存档点位置");
        _pendingSaveData = null; // 清空缓存
    }

    /*private void Update()
    {
        // 临时测试：按 L 键模拟死亡读档
        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadAndApplySave();
            Debug.Log("手动触发读档（模拟死亡）");
        }
    }*/

    // ========== 核心方法1：存档 ==========

    // 由存档点调用，保存当前游戏状态
    public void SaveGame(Vector2 savePointPosition)
    {
        // 1. 抓取玩家当前状态（组装数据盒子）
        SaveData data = CapturePlayerState();
        if (data == null)
            return;

        // 2. 存入存档点的坐标（复活位置）
        data.SetSavePointPosition(savePointPosition);

        // 3. 转成 JSON 并写入硬盘
        string json = JsonUtility.ToJson(data, true); 
        try
        {
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存存档失败：{e.Message}");
            return;
        }

        // 4. 控制台提示（方便调试）
        Debug.Log($"游戏已存档！位置：{savePointPosition}  文件路径：{SavePath}");

        // 5. JSON已成功写入，开始生成截图预览
        StartScreenshotCapture();
    }

    // ========== 核心方法2：读档并应用 ==========

    // 读取存档并恢复到游戏中（用于死亡复活 / 继续游戏）
    public bool LoadAndApplySave()
    {
        // 0. 清掉上一次转场中断可能残留的待应用存档
        _pendingSaveData = null;

        // 1. 从硬盘读取文件
        SaveData data = LoadRawData();

        // 2. 如果没有存档，返回失败
        if (data == null)
        {
            Debug.LogWarning("没有找到存档文件！请检查是否已触碰存档点。");
            // TODO: 这里以后接入 "回到关卡起点" 的逻辑
            return false;
        }

        // 3. 校验目标场景
        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogWarning($"存档场景无效：{data.sceneName}");
            return false;
        }

        // 4. 把数据存起来，然后加载存档场景
        _pendingSaveData = data;
        Time.timeScale = 1f; // 恢复游戏

        // 5. 优先使用场景转场，保证输入状态和转场UI
        if (SceneTransition.Instance != null && PlayerInputReader.HasInstance)
        {
            try
            {
                SceneTransition.Instance.GoToScene(data.sceneName);
                Debug.Log($"正在通过场景转场加载存档场景：{data.sceneName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"场景转场加载存档失败：{e.Message}，改用无转场回退");
            }
        }

        // 6. SceneTransition缺失或转场失败时使用无转场回退
        Debug.LogWarning("SceneTransition 缺失或不可用，使用无转场回退加载存档场景");
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnUICancelTrigger();
            PlayerInputReader.Instance.SwitchToGameplay();
        }
        SceneManager.LoadScene(data.sceneName);

        Debug.Log($"正在加载存档场景：{data.sceneName}，完成后将恢复存档数据");
        return true;
    }

    // ========== 内部工具方法 ==========


    // 仅从硬盘读取文件，不应用到游戏（供主菜单检查“继续游戏”按钮状态）
    public SaveData LoadRawData()
    {
        if (!File.Exists(SavePath))
            return null;

        try
        {
            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return null;

            // 旧存档没有saveTime时，用文件最后修改时间作为本次显示回退值
            if (string.IsNullOrEmpty(data.saveTime))
            {
                try
                {
                    data.saveTime = new System.DateTimeOffset(File.GetLastWriteTime(SavePath)).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"读取存档时间失败：{e.Message}");
                    data.saveTime = "";
                }
            }

            NormalizeSaveData(data);
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取存档失败：{e.Message}");
            return null;
        }
    }

    // 检查是否有存档文件
    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    // 检查存档是否有效：文件可读且目标场景可加载
    public bool HasValidSave()
    {
        SaveData data = LoadRawData();
        if (data == null)
            return false;

        return Application.CanStreamedLevelBeLoaded(data.sceneName);
    }

    // 读取存档截图；没有存档、没有截图、空截图或读取异常时返回false
    public bool TryLoadPreview(out byte[] imageBytes)
    {
        imageBytes = null;

        if (!HasSave() || !File.Exists(PreviewPath))
            return false;

        try
        {
            imageBytes = File.ReadAllBytes(PreviewPath);
            return imageBytes != null && imageBytes.Length > 0;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"读取存档截图失败：{e.Message}");
            imageBytes = null;
            return false;
        }
    }

    // 删除唯一存档文件
    public bool DeleteSave()
    {
        _pendingSaveData = null;

        // 停止可能仍在等待的截图协程，防止删除后截图文件重新生成
        if (_screenshotCoroutine != null)
        {
            StopCoroutine(_screenshotCoroutine);
            _screenshotCoroutine = null;
        }

        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
            if (File.Exists(PreviewPath))
                File.Delete(PreviewPath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除存档失败：{e.Message}");
            return false;
        }
    }

    // 复活传送期间短暂抑制存档点自动保存
    public void SuppressSavePointForRespawn()
    {
        _savePointSuppressEndTime = Time.unscaledTime + 0.5f;
    }

    // 当前是否处于复活传送抑制存档点保存的状态
    public bool IsSavePointSaveSuppressed => Time.unscaledTime < _savePointSuppressEndTime;

    // ========== 截图预览 ==========

    // 启动截图协程；重复保存前先停止旧协程，避免并行写入
    private void StartScreenshotCapture()
    {
        if (_screenshotCoroutine != null)
        {
            StopCoroutine(_screenshotCoroutine);
            _screenshotCoroutine = null;
        }

        _screenshotCoroutine = StartCoroutine(CaptureScreenshotCoroutine());
    }

    private IEnumerator CaptureScreenshotCoroutine()
    {
        // 新截图开始前清除旧截图，避免截图失败后展示旧预览
        try
        {
            if (File.Exists(PreviewPath))
                File.Delete(PreviewPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"清除旧存档截图失败：{e.Message}");
        }

        yield return new WaitForEndOfFrame();

        RenderTexture previousActive = RenderTexture.active;
        Texture2D sourceTexture = null;
        RenderTexture rt = null;
        Texture2D scaledTexture = null;
        try
        {
            sourceTexture = ScreenCapture.CaptureScreenshotAsTexture();
            if (sourceTexture == null)
            {
                Debug.LogWarning("截图失败：CaptureScreenshotAsTexture 返回 null");
                yield break;
            }

            rt = RenderTexture.GetTemporary(640, 360, 0);
            Graphics.Blit(sourceTexture, rt);

            RenderTexture.active = rt;
            scaledTexture = new Texture2D(640, 360, TextureFormat.RGB24, false);
            scaledTexture.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            scaledTexture.Apply();

            byte[] bytes = scaledTexture.EncodeToJPG(80);
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogWarning("截图编码失败：EncodeToJPG 返回空数据");
                yield break;
            }

            File.WriteAllBytes(PreviewPath, bytes);
            Debug.Log($"存档截图已保存：{PreviewPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"保存存档截图失败：{e.Message}");
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (rt != null)
                RenderTexture.ReleaseTemporary(rt);
            if (sourceTexture != null)
                Destroy(sourceTexture);
            if (scaledTexture != null)
                Destroy(scaledTexture);
            _screenshotCoroutine = null;
        }
    }

    // ========== 数据抓取与恢复 ==========

    // 从游戏中抓取当前玩家的所有状态
    private SaveData CapturePlayerState()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("保存存档失败：场景中没有 PlayerController");
            return null;
        }

        CoinManager coin = FindObjectOfType<CoinManager>();
        if (coin == null)
        {
            Debug.LogError("保存存档失败：场景中没有 CoinManager");
            return null;
        }

        ElementAbilityManager element = FindObjectOfType<ElementAbilityManager>();
        if (element == null)
        {
            Debug.LogError("保存存档失败：场景中没有 ElementAbilityManager");
            return null;
        }

        SaveData data = new SaveData();
        data.sceneName = SceneManager.GetActiveScene().name;
        data.saveTime = System.DateTimeOffset.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        data.currentForm = player.GetCurrentForm().ToString();
        data.unlockedForms = player.GetUnlockedForms().ConvertAll(f => f.ToString());
        data.coinCount = coin.GetCoinCount();
        data.coinAccumulatedCount = coin.GetCoinAccumulatedCount();
        data.unlockedElements = element.GetUnlockedElements().ConvertAll(e => e.ToString());

        // 捕获后同样保证 Slime 解锁不变量
        NormalizeSaveData(data);

        // 注意：玩家的坐标我们不需要在这里抓取，因为存档点的位置会由 SavePoint 传进来
        return data;
    }

    // 把存档数据恢复到游戏中
    private void ApplySaveData(SaveData data)
    {
        // 1. 恢复玩家位置（最重要！）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // 把玩家移动到存档点位置
            player.transform.position = data.GetSavePointPosition();

            // 重置刚体速度，防止复活后乱飞
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            Debug.Log($"玩家已移动到：{data.GetSavePointPosition()}");

            // 2. 恢复形态（PlayerController 挂在玩家根节点，复用已找到的 player）
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                List<FormType> unlockedForms = ParseFormTypes(data.unlockedForms);
                FormType current = ParseCurrentForm(data.currentForm, unlockedForms);
                playerController.RestoreForms(current, unlockedForms);
                Debug.Log($"恢复形态：当前={current}，已解锁={string.Join(", ", unlockedForms)}");
            }
        }
        else
        {
            Debug.LogError("场景中没有找到 Tag 为 'Player' 的游戏对象！");
        }

        // 3. 恢复收集数据（变更）
        CoinManager coinManager = FindObjectOfType<CoinManager>();
        if (coinManager != null)
        {
            coinManager.RestoreCoinData(data.coinCount, data.coinAccumulatedCount);
            Debug.Log($"恢复金币：当前={data.coinCount}，累计={data.coinAccumulatedCount}");
        }

        // 4. 恢复元素能力（新增）
        ElementAbilityManager elementManager = FindObjectOfType<ElementAbilityManager>();
        if (elementManager != null)
        {
            List<ElementType> elements = ParseElementTypes(data.unlockedElements);
            elementManager.RestoreElements(elements);
            Debug.Log($"恢复元素：{string.Join(", ", elements)}");
        }
    }

    // 读取存档时做兼容归一化
    private void NormalizeSaveData(SaveData data)
    {
        if (string.IsNullOrEmpty(data.sceneName))
            data.sceneName = "WetLand_Main";

        if (data.unlockedForms == null)
            data.unlockedForms = new List<string>();
        if (data.unlockedElements == null)
            data.unlockedElements = new List<string>();

        List<FormType> unlockedForms = ParseFormTypes(data.unlockedForms);
        if (!unlockedForms.Contains(FormType.Slime))
            unlockedForms.Insert(0, FormType.Slime);

        FormType current = ParseCurrentForm(data.currentForm, unlockedForms);

        data.currentForm = current.ToString();
        data.unlockedForms = unlockedForms.ConvertAll(f => f.ToString());
        data.unlockedElements = ParseElementTypes(data.unlockedElements).ConvertAll(e => e.ToString());
    }

    // 解析已解锁形态列表，非法值直接跳过
    private List<FormType> ParseFormTypes(List<string> rawList)
    {
        List<FormType> result = new List<FormType>();
        if (rawList == null)
            return result;

        foreach (string raw in rawList)
        {
            if (TryParseFormType(raw, out FormType form) && !result.Contains(form))
                result.Add(form);
        }
        return result;
    }

    // 解析当前形态，非法或未解锁时回退Slime
    private FormType ParseCurrentForm(string raw, List<FormType> unlockedForms)
    {
        if (TryParseFormType(raw, out FormType form) && unlockedForms.Contains(form))
            return form;
        return FormType.Slime;
    }

    // 解析元素列表，非法值直接跳过
    private List<ElementType> ParseElementTypes(List<string> rawList)
    {
        List<ElementType> result = new List<ElementType>();
        if (rawList == null)
            return result;

        foreach (string raw in rawList)
        {
            if (TryParseElementType(raw, out ElementType element) && !result.Contains(element))
                result.Add(element);
        }
        return result;
    }

    // 兼容旧FORM_占位值
    private bool TryParseFormType(string raw, out FormType result)
    {
        result = default;
        if (string.IsNullOrEmpty(raw))
            return false;

        if (raw == "FORM_SLIME")
        {
            result = FormType.Slime;
            return true;
        }
        if (raw == "FORM_FROG")
        {
            result = FormType.Frog;
            return true;
        }

        return System.Enum.TryParse<FormType>(raw, true, out result) && System.Enum.IsDefined(typeof(FormType), result);
    }

    private bool TryParseElementType(string raw, out ElementType result)
    {
        result = default;
        if (string.IsNullOrEmpty(raw))
            return false;

        return System.Enum.TryParse<ElementType>(raw, true, out result) && System.Enum.IsDefined(typeof(ElementType), result);
    }
}
