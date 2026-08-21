using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    // ---------- 0. 场景/存档基本信息 ----------
    public string sceneName;
    public string saveTime;

    // ---------- 1. 存档点位置 ----------
    public float savePointX;
    public float savePointY;

    // ---------- 2. 形态数据 ----------
    public string currentForm;              // 当前使用的形态ID
    public List<string> unlockedForms;      // 已解锁的形态列表

    // ---------- 3. 收集数据（变更） ----------
    public int coinCount;                  // 当前金币数量
    public int coinAccumulatedCount;       // 金币累计计数（每30枚触发效果）

    // ---------- 4. 元素能力（新增） ----------
    public List<string> unlockedElements;  // 已解锁的元素能力列表

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