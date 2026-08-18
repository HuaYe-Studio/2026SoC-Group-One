using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryCollectionManager : MonoBehaviour
{
    public static MemoryCollectionManager Instance; 
    public List<int> collectedIDList = new List<int>();
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

    public void AddMemoryFragment(int id)
    {
        if (collectedIDList.Contains(id))
        {
            return;
        }
        collectedIDList.Add(id);
        Debug.Log($" 收集到: ID: {id}");
    }
}
