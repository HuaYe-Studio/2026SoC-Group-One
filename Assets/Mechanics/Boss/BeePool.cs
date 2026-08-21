using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 蜜蜂对象池：复用蜜蜂 GameObject，避免大量 Instantiate/Destroy（蜂巢被毁后集中回收、飞散销毁）造成的 GC 与卡顿。
/// 按 prefab 分池；Get 优先取回收实例（无则 Instantiate），Release 清理状态后 SetActive(false) 入池。
/// 与 FlockManager 同为静态池，依赖 Unity 进入播放模式时重置静态状态（默认域重载）。
/// </summary>
public static class BeePool
{
    // prefab -> 空闲实例栈
    private static readonly Dictionary<GameObject, Stack<GameObject>> _available =
        new Dictionary<GameObject, Stack<GameObject>>();

    // 实例 -> 其 prefab（Release 时反查应入哪个池）
    private static readonly Dictionary<GameObject, GameObject> _prefabOf =
        new Dictionary<GameObject, GameObject>();

    // 每个 prefab 的最大空闲实例数，超过直接 Destroy，避免无限堆积
    private const int MaxIdlePerPrefab = 200;

    /// <summary>取一只蜜蜂：优先复用回收实例，池空则 Instantiate。</summary>
    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[BeePool] prefab 为空，无法取蜜蜂");
            return null;
        }

        GameObject go = null;
        if (_available.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0 && go == null)
                go = stack.Pop();
        }

        if (go == null)
        {
            go = Object.Instantiate(prefab, position, rotation);
        }
        else
        {
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
        }

        _prefabOf[go] = prefab;
        return go;
    }

    /// <summary>回收一只蜜蜂：清理其状态后 SetActive(false) 入池，超过上限则直接销毁。</summary>
    public static void Release(GameObject go)
    {
        if (go == null) return;

        // 先清理蜜蜂自身状态（退订目标事件、停飞、释放巡游点认领），并重置行为树
        BeeAI bee = go.GetComponent<BeeAI>();
        if (bee != null) bee.OnPoolRelease();
        BeeBT bt = go.GetComponent<BeeBT>();
        if (bt != null) bt.ResetForReuse();

        _prefabOf.TryGetValue(go, out GameObject prefab);
        go.SetActive(false);

        if (prefab != null)
        {
            if (!_available.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _available[prefab] = stack;
            }

            if (stack.Count < MaxIdlePerPrefab)
            {
                stack.Push(go);
                return;
            }

            _prefabOf.Remove(go);
        }

        Object.Destroy(go);
    }
}
