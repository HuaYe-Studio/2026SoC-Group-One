using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class UIEventCenter
{
    /// <summary>
    /// 场景切换事件，传递参数为：fromScene,toScene
    /// </summary>
    public static event Action<string,string> OnSceneChanged;

    public static void TriggerSceneChanged(string fromScene, string toScene)
    {
        OnSceneChanged?.Invoke(fromScene, toScene);
    }
}
