using System.ComponentModel;
using UnityEngine;

public class HeavyObject : MonoBehaviour, IHeavy
{
    [SerializeField] private bool isHeavy = true;

    public bool IsHeavy => isHeavy ;
    public void SetHeavyState(bool heavy)
    {
        isHeavy = heavy;
    }

    public void ToggleHeavy()
    {
        isHeavy = !isHeavy;
    }
    public void SetPlayerHeavy()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<HeavyObject>() == null)
            player.AddComponent<HeavyObject>();
    }
    public void RemovePlayerHeavy()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        HeavyObject heavy = player.GetComponent<HeavyObject>();
        if (heavy != null)
            Destroy(heavy);
    }
}