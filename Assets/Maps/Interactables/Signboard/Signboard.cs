using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Signboard : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("拖入告示牌头顶的 UI Canvas 或提示 Panel")]
    public GameObject uiHint;

    private bool isPlayerInside = false;
    private void Start()
    {
        // 游戏开始时确保提示 UI 是隐藏的
        if (uiHint != null)
        {
            uiHint.SetActive(false);
        }
    }

    // 玩家进入告示牌网格区域时触发
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("有物体踏入了告示牌区域：" + other.name); // 调试打印
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;

            // 踏入格子的瞬间直接显示 UI
            if (uiHint != null)
            {
                uiHint.SetActive(true);
            }
        }
    }

    // 玩家离开告示牌网格区域时触发
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;

            // 离开网格时自动隐藏 UI
            if (uiHint != null)
            {
                uiHint.SetActive(false);
            }
        }
    }
}