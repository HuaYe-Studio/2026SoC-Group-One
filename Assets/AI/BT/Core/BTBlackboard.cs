using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 行为树黑板：存储节点间共享的数据（键值对）。
/// 挂载到同一 GameObject 上，由各节点通过 BehaviorTreeRunner 访问。
/// </summary>
public class BTBlackboard : MonoBehaviour
{
    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

    public void Set(string key, object value)
    {
        _data[key] = value;
    }

    public T Get<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out object value) && value is T typedValue)
            return typedValue;

        return defaultValue;
    }

    public bool Has(string key)
    {
        return _data.ContainsKey(key);
    }

    public void Remove(string key)
    {
        _data.Remove(key);
    }

    public void Clear()
    {
        _data.Clear();
    }
}
