using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static MemoryFragment;

public class MemoryFragment : MonoBehaviour
{
    [Header("ID")]
    public int itemID;

    [Header("replace")]
    public GameObject coinPrefab;
   
    private bool isCollected = false;
    private void Start()
    {
        if (isCollected)
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
        isCollected = true;
        MemoryCollectionManager.Instance.AddMemoryFragment(itemID);
        Destroy(gameObject);
    }
}
