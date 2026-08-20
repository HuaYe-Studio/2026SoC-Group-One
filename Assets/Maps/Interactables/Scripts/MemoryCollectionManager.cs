using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryCollectionManager : MonoBehaviour
{
    public static MemoryCollectionManager Instance;
    private HashSet<string> CollectedIDs = new HashSet<string>();
    private const string SAVE_KEY = "CollectedMemoryIDs";
    void Awake()
    {
        //Reset();
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();
        }
        else
            Destroy(gameObject);
    }

    public void AddMemoryFragment(string id)
    {
        if (CollectedIDs.Contains(id))
        {
            return;
        }
        CollectedIDs.Add(id);
        Debug.Log($" 收集到: ID: {id}");
        SaveData();
    }
    public bool IsMemoryCollected(string memoryID)
    {
        return CollectedIDs.Contains(memoryID);
    }
    public void Reset()
    {
        CollectedIDs.Clear();
        SaveData();
    }
    public List<string> GetCollectedList()
    {
        return new List<string>(CollectedIDs);
    }
    private void SaveData()
    {
        string data = string.Join(",", CollectedIDs);
        PlayerPrefs.SetString(SAVE_KEY, data);
        PlayerPrefs.Save();
        // Debug.Log("数据已保存");
    }
    private void LoadData()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string data = PlayerPrefs.GetString(SAVE_KEY);
            if (!string.IsNullOrEmpty(data))
            {
                string[] ids = data.Split(',');
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id))
                        CollectedIDs.Add(id);
                }
                //Debug.Log($"加载了 {CollectedIDs.Count} 个收集项");
            }
        }
    }
}
