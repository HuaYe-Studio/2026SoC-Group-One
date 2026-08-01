using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
   private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            Collect();
        }
    }
    void Collect()
    {
        CoinManager.Instance.AddCoin(1);
        Destroy(gameObject);
    }
}
