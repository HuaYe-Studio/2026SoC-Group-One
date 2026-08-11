using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance;
    public static int coinCount = 0;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void AddCoin(int amount = 1)
    {
        coinCount += amount;
        PlayerPrefs.SetInt("CoinCount", coinCount);
        UpdateUI();
    }
    void UpdateUI()
    {
        UIEventCenter.TriggerGetCoin();//UI:更新UI的触发器
    }
    public void ResetCoins()
    {
        coinCount = 0;
        PlayerPrefs.DeleteKey("CoinCount");
        UpdateUI();
    }
}
