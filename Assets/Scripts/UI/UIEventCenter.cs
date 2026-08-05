using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class UIEventCenter
{
    /// <summary>
    /// 场景切换事件，传递参数为：fromScene,toScene
    /// </summary>
    public static event Action<string, string> OnSceneChanged;

    public static void TriggerSceneChanged(string fromScene, string toScene)//场景切换时调用此方法触发事件
    {
        OnSceneChanged?.Invoke(fromScene, toScene);
    }

    /// <summary>
    /// 面板打开事件
    /// </summary>
    public static event Action OnMainMenuSettingPanelOpened;

    public static void TriggerMainMenuSettingPanelOpened()//主菜单设置面板打开时调用此方法触发事件
    {
        OnMainMenuSettingPanelOpened?.Invoke();
    }

    /// <summary>
    /// 面板关闭事件
    /// </summary>
    public static event Action OnMainMenuSettingPanelClosed;

    public static void TriggerMainMenuSettingPanelClosed()//主菜单设置面板关闭时调用此方法触发事件
    {
        OnMainMenuSettingPanelClosed?.Invoke();
    }
}
