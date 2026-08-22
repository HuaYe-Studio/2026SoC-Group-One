using UnityEngine;

public class BossReward : MonoBehaviour
{
    [Header("门（要移除的）")]
    [SerializeField] private GameObject[] doorsToRemove;

    [Header("金币（要生成的）")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private Transform[] coinSpawnPoints;

    private void OnEnable()
    {
        MockEventCenter.OnBossDefeated += OnBossDefeated;
    }

    private void OnDisable()
    {
        MockEventCenter.OnBossDefeated -= OnBossDefeated;
    }

    private void OnBossDefeated()
    {
        Debug.Log("Boss已击败，移除门并生成金币");

        // 移除所有门
        RemoveDoors();

        // 在指定位置生成金币
        SpawnCoins();
    }

    private void RemoveDoors()
    {
        foreach (GameObject door in doorsToRemove)
        {
            if (door != null)
            {
                Destroy(door);
                Debug.Log($"移除门: {door.name}");
            }
        }
    }

    private void SpawnCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("金币预制体未设置！");
            return;
        }

        foreach (Transform spawnPoint in coinSpawnPoints)
        {
            if (spawnPoint != null)
            {
                Instantiate(coinPrefab, spawnPoint.position, Quaternion.identity);
            }
        }
    }
}