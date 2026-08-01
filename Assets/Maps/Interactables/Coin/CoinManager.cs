using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance;
    private int coinCount = 0;
    public Text coinText;
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
        if (coinText != null)
            coinText.text = "Coins: " + coinCount;
    }
    public void ResetCoins()
    {
        coinCount = 0;
        PlayerPrefs.DeleteKey("CoinCount");
        UpdateUI();
    }
}
