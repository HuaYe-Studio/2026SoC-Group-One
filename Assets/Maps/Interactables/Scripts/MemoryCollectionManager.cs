using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryCollectionManager : MonoBehaviour
{
    public static MemoryCollectionManager Instance;
    private readonly HashSet<string> CollectedIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
    }
    public bool IsMemoryCollected(string memoryID)
    {
        return CollectedIDs.Contains(memoryID);
    }
    public void Reset()
    {
        CollectedIDs.Clear();
    }
    public List<string> GetCollectedList()
    {
        return new List<string>(CollectedIDs);
    }
    // 从存档恢复已收集的记忆碎片（读档时由 SaveManager.ApplySaveData 调用）
    public void RestoreCollected(IEnumerable<string> ids)
    {
        CollectedIDs.Clear();
        if (ids == null) return;
        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id))
                CollectedIDs.Add(id);
        }
    }
}
