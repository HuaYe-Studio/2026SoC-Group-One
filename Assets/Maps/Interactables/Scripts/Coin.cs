using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 1;
   private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            Collect();
        }
    }
    void Collect()
    {
        CoinManager.Instance.AddCoin(value);
        Destroy(gameObject);
    }
}
