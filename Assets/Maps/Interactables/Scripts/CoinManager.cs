using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance;
    public static int coinCount = 0;
    public static int accumulatedCount = 0;
    public const int TRIGGER_THRESHOLD = 30;
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
        accumulatedCount += amount;
        if(accumulatedCount >= TRIGGER_THRESHOLD)
        {
            TriggerCoinEffect();
            accumulatedCount = accumulatedCount - TRIGGER_THRESHOLD;
        }
        UpdateUI();
    }
    private void TriggerCoinEffect()
    {
        Debug.Log("30");
    }
    void UpdateUI()
    {
        UIEventCenter.TriggerGetCoin();//UI:更新UI的触发器
    }
    public int GetCoinCount()
    {
        return coinCount;
    }
    public int GetCoinAccumulatedCount()
    {
        return accumulatedCount;
    }
    public void ResetCoins()
    {
        coinCount = 0;
        UpdateUI();
    }
    public void RestoreCoinData(int coinCount, int accumulatedCount)
    {
        CoinManager.coinCount= coinCount;
        CoinManager.accumulatedCount= accumulatedCount;
        UpdateUI();
    }
}