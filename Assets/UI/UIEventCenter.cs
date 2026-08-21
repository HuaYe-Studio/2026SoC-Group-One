using System;

public static class UIEventCenter
{
    // 存档系统 UI 事件
    public static event Action OnSaveCompleted;
    public static event Action OnLoadStarted;
    public static event Action OnLoadCompleted;
    public static event Action OnRespawnAtStart;

    // 触发方法
    public static void TriggerSaveCompleted() => OnSaveCompleted?.Invoke();
    public static void TriggerLoadStarted() => OnLoadStarted?.Invoke();
    public static void TriggerLoadCompleted() => OnLoadCompleted?.Invoke();
    public static void TriggerRespawnAtStart() => OnRespawnAtStart?.Invoke();
}