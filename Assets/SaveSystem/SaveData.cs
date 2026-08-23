using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    // ---------- 0. 元数据 ----------
    public string sceneName;                // 存档时的场景名称
    public string saveTime;                 // 存档时间（ISO 格式）

    // ---------- 1. 存档点位置 ----------
    public float savePointX;
    public float savePointY;

    // ---------- 2. 形态数据 ----------
    public string currentForm;
    public List<string> unlockedForms;

    // ---------- 3. 元素能力 ----------
    public List<string> unlockedElements;

    // ---------- 4. 金币 ----------
    public int coinCount;
    public int coinAccumulatedCount;

    // ---------- 5. 记忆碎片 ----------
    public List<string> memoryFragments;

    // ---------- 辅助方法 ----------
    public void SetSavePointPosition(Vector2 pos)
    {
        savePointX = pos.x;
        savePointY = pos.y;
    }

    public Vector2 GetSavePointPosition()
    {
        return new Vector2(savePointX, savePointY);
    }
}