using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// UI用单例模式父类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class UISingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this as T;
        }
        DontDestroyOnLoad(gameObject);
    }
}
