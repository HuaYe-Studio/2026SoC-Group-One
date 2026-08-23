using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    // ---------- 单例（全局唯一访问点） ----------
    public static SaveManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string PreviewPath => Path.Combine(Application.persistentDataPath, "save_preview.jpg");

    // ============================================================
    // 数据加密（XOR 异或加密）
    // ============================================================
    // 作用：让存档文件变成乱码，防止玩家用记事本修改数据
    // 原理：对每个字符与密钥进行按位异或（XOR），两次异或可还原原文
    // 注意：密钥硬编码在代码中，只能防君子不防小人
    // ============================================================
    private const byte XOR_KEY = 0xAA;  // 加密密钥（0xAA = 170，可改）

    // 缓存读档数据，用于场景加载完成后恢复
    private SaveData _pendingSaveData = null;
    private Coroutine _screenshotCoroutine;
    private float _savePointSuppressEndTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
          {
            SceneManager.sceneLoaded -= OnSceneLoaded;
          }
    }

    //场景加载完成后自动调用
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 如果有待恢复的存档数据，就应用它
        if (_pendingSaveData != null)
        {
            ApplySaveData(_pendingSaveData);
            Debug.Log($"场景重载完成，玩家已恢复至存档点位置");
            // ===== 新增：通知 UI 读档完成 =====
            UIEventCenter.TriggerLoadCompleted();
            _pendingSaveData = null; // 清空缓存
        }
    }

    // ========== 核心方法1：存档 ==========

    // 由存档点调用，保存当前游戏状态
    public void SaveGame(Vector2 savePointPosition)
    {
        // 1. 抓取玩家当前状态
        SaveData data = CapturePlayerState();
        if (data == null)
            return;

        // 2. 存入存档点的坐标（复活位置）
        data.SetSavePointPosition(savePointPosition);

        // 3. 转成 JSON 并写入硬盘
        string json = JsonUtility.ToJson(data, true); 
        string encrypted = EncryptDecrypt(json);
        try
        {
             File.WriteAllText(SavePath, encrypted);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存存档失败：{e.Message}");
            return;
        }

        // 4. 控制台提示（方便调试）
        Debug.Log($"游戏已存档！位置：{savePointPosition}  文件路径：{SavePath}");
        UIEventCenter.TriggerSaveCompleted();
        // 5. JSON已成功写入，开始生成截图预览
        StartScreenshotCapture();
    }

    // ========== 核心方法2：读档并应用 ==========

    // 读取存档并恢复到游戏中（用于死亡复活 / 继续游戏）
    public bool LoadAndApplySave()
    {
        // 清掉上一次读档转场中断可能残留的待应用存档，避免误应用到下次场景加载
        _pendingSaveData = null;

        // ===== 新增：通知 UI 读档开始 =====
        UIEventCenter.TriggerLoadStarted();

        SaveData data = LoadRawData();

        if (data == null)
        {
            Debug.LogWarning("没有找到存档文件，将玩家传送至起点");

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // 传送到起点 (0,0)
                player.transform.position = Vector3.zero;

                // 重置刚体速度
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                // 重置形态为史莱姆
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.SwitchToFormByType(FormType.Slime);
                }

                // 恢复生命值到3颗心
                PlayerHP hp = player.GetComponent<PlayerHP>();
                if (hp != null)
                {
                    hp.Heal(3);
                    hp.SetInvincible(1f);
                }

                Debug.Log("玩家已重置到起点 (0, 0)，形态恢复为史莱姆，生命值恢复至3颗心");
            }
            else
            {
                Debug.LogError("找不到玩家，无法重置到起点！");
            }
            // ===== 新增：通知 UI 回到起点 =====
            UIEventCenter.TriggerRespawnAtStart();
            return false ;
        }

        // 有存档的正常读档流程：加载存档里的场景（而非当前所在场景，
        // 否则从主菜单读档会错误地重载主菜单场景）
        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogWarning($"存档场景无效：{data.sceneName}");
            return false;
        }

        _pendingSaveData = data;
        Time.timeScale = 1f; // 读档面板把游戏暂停了，加载存档场景前恢复时间

        // 优先通过场景转场加载（播放开门/加载动画），与主菜单开始游戏一致
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

        // 无转场回退
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
            // 读取文件内容（如果文件是空的，直接返回）
            string encrypted = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(encrypted))
            {
                Debug.LogWarning("存档文件为空，视为损坏");
                return null;
            }

            // ===== 解密后再解析 =====
            string json = EncryptDecrypt(encrypted);
            SaveData data = null;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch { data = null; }

            // 旧版未加密存档兼容：若解密后解析失败，再尝试直接按明文 JSON 解析
            if (data == null || string.IsNullOrEmpty(data.currentForm))
            {
                try { data = JsonUtility.FromJson<SaveData>(encrypted); }
                catch { data = null; }
            }

            // 检查解析结果是否有效
            if (data == null || string.IsNullOrEmpty(data.currentForm))
            {
                Debug.LogWarning("存档数据不完整，视为损坏");
                return null;
            }

            return data;
        }
        catch (IOException ex)
        {
            Debug.LogError($"读取存档时发生IO错误：{ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"读取存档时发生未知错误：{ex.Message}，存档可能已损坏");
            // 备份损坏文件供调试
            try
            {
                string backupPath = SavePath + ".corrupted";
                if (File.Exists(SavePath))
                {
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(SavePath, backupPath);
                }
                Debug.Log($"已备份损坏存档至：{backupPath}");
            }
            catch { }
            return null;
        }
    }

    // 检查是否有存档文件
    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    //检查存档是否有效（文件存在 + 数据完整 + 场景可加载）
    public bool HasValidSave()
    {
        if (!HasSave()) return false;

        SaveData data = LoadRawData();
        if (data == null) return false;

        if (string.IsNullOrEmpty(data.sceneName)) return false;

        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogWarning($"存档场景 {data.sceneName} 无法加载");
            return false;
        }

        return true;
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

        // 删档同时清空记忆碎片，保持单一存档一致性
        if (MemoryCollectionManager.Instance != null)
            MemoryCollectionManager.Instance.Reset();

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
        SaveData data = new SaveData();

        // ===== 0. 元数据 =====
        data.sceneName = SceneManager.GetActiveScene().name;
        data.saveTime = System.DateTimeOffset.Now.ToString("O");

        // ===== 1. 获取形态数据 =====
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                // 当前形态
                data.currentForm = pc.GetCurrentForm().ToString();

                // 已解锁形态列表
                List<FormType> forms = pc.GetUnlockedForms();
                data.unlockedForms = new List<string>();
                foreach (FormType ft in forms)
                {
                    data.unlockedForms.Add(ft.ToString());
                }
            }
            else
            {
                // 降级：找不到 PlayerController 时用默认值
                data.currentForm = "Slime";
                data.unlockedForms = new List<string> { "Slime" };
                Debug.LogWarning("未找到 PlayerController，使用默认形态数据");
            }
        }
        else
        {
            data.currentForm = "Slime";
            data.unlockedForms = new List<string> { "Slime" };
            Debug.LogWarning("未找到 Player 对象，使用默认形态数据");
        }

        // ===== 2. 获取元素能力 =====
        if (ElementAbilityManager.Instance != null)
        {
            List<ElementType> elements = ElementAbilityManager.Instance.GetUnlockedElements();
            data.unlockedElements = new List<string>();
            foreach (ElementType et in elements)
            {
                data.unlockedElements.Add(et.ToString());
            }
        }
        else
        {
            data.unlockedElements = new List<string>();
            Debug.LogWarning("未找到 ElementAbilityManager，元素能力使用空列表");
        }

        // ===== 3. 获取金币 =====
        if (CoinManager.Instance != null)
        {
            data.coinCount = CoinManager.Instance.GetCoinCount();
            data.coinAccumulatedCount = CoinManager.Instance.GetCoinAccumulatedCount();
        }
        else
        {
            data.coinCount = 0;
            data.coinAccumulatedCount = 0;
            Debug.LogWarning("未找到 CoinManager，金币使用默认值");
        }

        // ===== 4. 获取记忆碎片 =====
        data.memoryFragments = MemoryCollectionManager.Instance != null
            ? MemoryCollectionManager.Instance.GetCollectedList()
            : new List<string>();

        return data;
    }

    // 把存档数据恢复到游戏中
    private void ApplySaveData(SaveData data)
    {
        // ===== 1. 找到玩家 =====
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("场景中没有找到 Tag 为 'Player' 的游戏对象！");
            return;
        }

        // ===== 2. 恢复玩家位置 =====
        player.transform.position = data.GetSavePointPosition();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        Debug.Log($"玩家已移动到：{data.GetSavePointPosition()}");

        // ===== 3. 恢复形态 =====
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            FormType current = (FormType)System.Enum.Parse(typeof(FormType), data.currentForm);
            List<FormType> unlocked = new List<FormType>();
            foreach (string s in data.unlockedForms)
            {
                unlocked.Add((FormType)System.Enum.Parse(typeof(FormType), s));
            }
            pc.RestoreForms(current, unlocked);
            Debug.Log($"恢复形态：当前={current}，已解锁={string.Join(", ", unlocked)}");
        }
        else
        {
            Debug.LogWarning("玩家身上没有 PlayerController，形态恢复失败");
        }

        // ===== 4. 恢复元素能力 =====
        if (ElementAbilityManager.Instance != null)
        {
            List<ElementType> elements = new List<ElementType>();
            foreach (string s in data.unlockedElements)
            {
                elements.Add((ElementType)System.Enum.Parse(typeof(ElementType), s));
            }
            ElementAbilityManager.Instance.RestoreElements(elements);
            Debug.Log($"恢复元素：{string.Join(", ", elements)}");
        }
        else
        {
            Debug.LogWarning("未找到 ElementAbilityManager，元素恢复失败");
        }

        // ===== 5. 恢复金币 =====
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.RestoreCoinData(data.coinCount, data.coinAccumulatedCount);
            Debug.Log($"恢复金币：当前={data.coinCount}，累计={data.coinAccumulatedCount}");
        }
        else
        {
            Debug.LogWarning("未找到 CoinManager，金币恢复失败");
        }

        // ===== 6. 恢复生命值（固定为3颗心） =====
        PlayerHP hp = player.GetComponent<PlayerHP>();
        if (hp != null)
        {
            hp.Heal(3);
            hp.SetInvincible(1f);
            Debug.Log("生命值已恢复至3颗心，给予1秒无敌");
        }

        // ===== 7. 恢复记忆碎片 =====
        if (MemoryCollectionManager.Instance != null)
        {
            MemoryCollectionManager.Instance.RestoreCollected(data.memoryFragments);
            Debug.Log($"恢复记忆碎片：{data.memoryFragments?.Count ?? 0} 个");
        }
    }

    //退出自动保存
    private void OnApplicationQuit()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && HasSave())
        {
            SaveData data = LoadRawData();
            if (data != null)
            {
                Vector2 pos = player.transform.position;
                data.SetSavePointPosition(pos);
                string json = JsonUtility.ToJson(data, true);
                // ===== 改为加密写入 =====
                string encrypted = EncryptDecrypt(json);
                File.WriteAllText(SavePath, encrypted);
                Debug.Log($"退出存档已保存：位置 {pos}");
            }
        }
    }

    //继续游戏
    public bool ContinueFromMainMenu()
    {
        SaveData data = LoadRawData();
        if (data == null || !HasValidSave())
        {
            Debug.LogWarning("没有有效的存档可供继续");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogError($"场景 {data.sceneName} 不存在或无法加载");
            return false;
        }

        _pendingSaveData = data;
        SceneManager.LoadScene(data.sceneName);
        Debug.Log($"继续游戏：加载场景 {data.sceneName}");
        return true;
    }

    // ============================================================
    // 加密/解密工具方法
    // ============================================================
    private string EncryptDecrypt(string input)
    {
        // 空字符串或 null 直接返回
        if (string.IsNullOrEmpty(input))
            return input;

        // 将字符串转为字符数组，逐个字符与密钥异或
        char[] chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            // char 与 byte 异或，结果隐式转为 int，再转回 char
            chars[i] = (char)(chars[i] ^ XOR_KEY);
        }
        return new string(chars);
    }
}
