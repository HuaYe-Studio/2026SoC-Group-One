using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    // ---------- 单例（全局唯一访问点） ----------
    public static SaveManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    // 缓存读档数据，用于场景加载完成后恢复
    private SaveData _pendingSaveData = null;

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

    //场景加载完成后自动调用
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 如果有待恢复的存档数据，就应用它
        if (_pendingSaveData != null)
        {
            ApplySaveData(_pendingSaveData);
            Debug.Log($"场景重载完成，玩家已恢复至存档点位置");
            _pendingSaveData = null; // 清空缓存
        }
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

        // 2. 存入存档点的坐标（复活位置）
        data.SetSavePointPosition(savePointPosition);

        // 3. 转成 JSON 并写入硬盘
        string json = JsonUtility.ToJson(data, true); 
        File.WriteAllText(SavePath, json);

        // 4. 控制台提示（方便调试）
        Debug.Log($"游戏已存档！位置：{savePointPosition}  文件路径：{SavePath}");
    }

    // ========== 核心方法2：读档并应用 ==========

    // 读取存档并恢复到游戏中（用于死亡复活 / 继续游戏）
    public void LoadAndApplySave()
    {
        // 1. 从硬盘读取文件
        SaveData data = LoadRawData();

        // 2. 如果没有存档，回到起点（先打印警告）
        if (data == null)
        {
            Debug.LogWarning("没有找到存档文件！请检查是否已触碰存档点。");
            // TODO: 这里以后接入 "回到关卡起点" 的逻辑
            return;
        }

        // ===== 改动点：把数据存起来，然后重载场景 =====
        _pendingSaveData = data;

        // 获取当前场景名称并重新加载
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);

        Debug.Log($"正在重载场景：{currentScene}，完成后将恢复存档数据");
    }

    // ========== 内部工具方法 ==========


    // 仅从硬盘读取文件，不应用到游戏（供主菜单检查“继续游戏”按钮状态）
    public SaveData LoadRawData()
    {
        if (!File.Exists(SavePath))
            return null;

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        return data;
    }

    // 检查是否有存档文件
    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    // ========== 数据抓取与恢复（占位区） ==========


    // 从游戏中抓取当前玩家的所有状态
    private SaveData CapturePlayerState()
    {
        SaveData data = new SaveData();

        // 【目前用假数据占位】等形态系统写好了，把下面这几行替换成真正的调用即可
        data.currentForm = "FORM_SLIME";
        data.unlockedForms = new List<string> { "FORM_SLIME", "FORM_FROG" };

        // ----- 收集数据（变更）-----
        data.coinCount = 0;                 // 当前金币
        data.coinAccumulatedCount = 0;      // 累计计数

        // ----- 元素能力（新增）-----
        data.unlockedElements = new List<string>(); // 暂时为空

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
        }
        else
        {
            Debug.LogError("场景中没有找到 Tag 为 'Player' 的游戏对象！");
        }

        // 2. 恢复形态（暂时只打印，等形态系统接入）
        Debug.Log($"恢复形态：当前={data.currentForm}，已解锁={string.Join(", ", data.unlockedForms)}");

        // 3. 恢复收集数据（变更）
        Debug.Log($"恢复金币：当前={data.coinCount}，累计={data.coinAccumulatedCount}");

        // 4. 恢复元素能力（新增）
        Debug.Log($"恢复元素：{string.Join(", ", data.unlockedElements)}");
    }
}