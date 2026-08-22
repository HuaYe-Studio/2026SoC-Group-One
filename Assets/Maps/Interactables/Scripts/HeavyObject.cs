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
        // Player 根节点是所有形态碰撞体的共同祖先；用 PlayerController 定位根节点，
        // 避免 FindGameObjectWithTag("Player") 因多个 Player 标签对象返回非根节点，
        // 导致 SignalSource.GetComponentInParent 检测不到
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.GetComponent<HeavyObject>() == null)
        {
            pc.gameObject.AddComponent<HeavyObject>();
            Debug.Log($"[HeavyObject] SetPlayerHeavy: added to {pc.name}, IsHeavy={pc.GetComponent<HeavyObject>().IsHeavy}");
        }
        else
        {
            Debug.Log($"[HeavyObject] SetPlayerHeavy: pc={pc?.name}, already={pc?.GetComponent<HeavyObject>()?.IsHeavy}");
        }
    }
    public void RemovePlayerHeavy()
    {
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc == null) return;
        HeavyObject heavy = pc.GetComponent<HeavyObject>();
        if (heavy != null)
            Destroy(heavy);
    }
}