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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // ===== 新增：订阅玩家死亡事件 =====
            MockEventCenter.OnPlayerDeath += HandlePlayerDeath;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== 新增：玩家死亡处理函数 =====
    private void HandlePlayerDeath()
    {
        Debug.Log("玩家死亡，触发读档");
        LoadAndApplySave();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MockEventCenter.OnPlayerDeath -= HandlePlayerDeath;
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

        // ===== 新增：通知 UI 存档完成 =====
        UIEventCenter.TriggerSaveCompleted();
    }

    // ========== 核心方法2：读档并应用 ==========

    // 读取存档并恢复到游戏中（用于死亡复活 / 继续游戏）
    public bool LoadAndApplySave()
    {
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

        // 有存档的正常读档流程
        _pendingSaveData = data;
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
        Debug.Log($"正在重载场景：{currentScene}，完成后将恢复存档数据");

        return true;
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

        // ===== 5. 恢复生命值（固定为3颗心） =====
        PlayerHP hp = player.GetComponent<PlayerHP>();
        if (hp != null)
        {
            hp.Heal(3);
            hp.SetInvincible(1f);
            Debug.Log("生命值已恢复至3颗心，给予1秒无敌");
        }
    }
}