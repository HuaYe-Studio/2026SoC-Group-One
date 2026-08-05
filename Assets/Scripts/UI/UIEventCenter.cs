using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// UI事件中心
/// </summary>
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
