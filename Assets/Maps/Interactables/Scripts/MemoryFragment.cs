using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static MemoryFragment;

public class MemoryFragment : MonoBehaviour
{
    [Header("ID")]
    public string itemID;

    [Header("replace")]
    public GameObject coinPrefab;
    private void Start()
    {
        if (MemoryCollectionManager.Instance.IsMemoryCollected(itemID))
        {
            ReplaceWithCoins();
            return;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CollectFragment();
        }
    }
     public void ReplaceWithCoins()
    {
        GameObject coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
    void CollectFragment()
    {
        MemoryCollectionManager.Instance.AddMemoryFragment(itemID);
        Destroy(gameObject);
    }
}
