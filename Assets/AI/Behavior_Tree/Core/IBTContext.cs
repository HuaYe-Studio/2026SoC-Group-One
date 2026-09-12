using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 统一节点构造上下文（阶段 3.2）：工厂创建节点时注入的依赖集合，
/// 替代各节点散落的构造函数参数（FrogAI/AnimalBase/Blackboard…）。
/// 节点声明 IBTContext 构造器即可被 BTNodeFactory.Create 统一实例化，并读取参数表做数据驱动。
/// </summary>
public interface IBTContext
{
    /// <summary>所属对象（动物根物体 / 挂载 *BT 的组件）。</summary>
    Component Owner { get; }

    /// <summary>所属动物的 AnimalBase（可能是具体子类如 FrogAI/SpiderAI）。</summary>
    AnimalBase Animal { get; }

    /// <summary>所属动物的认知黑板。</summary>
    Blackboard Board { get; }

    /// <summary>节点参数表（数据驱动：JSON/代码传入的键值对）。</summary>
    IReadOnlyDictionary<string, string> Params { get; }

    /// <summary>从所属对象取组件（含 Owner 自身），取不到返回 null。</summary>
    T GetComponent<T>() where T : class;

    /// <summary>读取参数（带默认值）。</summary>
    string GetParam(string key, string defaultValue = null);

    /// <summary>读取整数参数（带默认值）。</summary>
    int GetParamInt(string key, int defaultValue = 0);

    /// <summary>读取浮点参数（带默认值）。</summary>
    float GetParamFloat(string key, float defaultValue = 0f);
}

/// <summary>IBTContext 默认实现：包装动物组件 + 参数表。</summary>
public class BTContext : IBTContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyParams = new Dictionary<string, string>();

    private readonly Component _owner;
    private readonly AnimalBase _animal;
    private readonly Blackboard _board;
    private readonly IReadOnlyDictionary<string, string> _params;

    public Component Owner => _owner;
    public AnimalBase Animal => _animal;
    public Blackboard Board => _board;
    public IReadOnlyDictionary<string, string> Params => _params;

    /// <param name="owner">动物根物体（Anim/根节点，含 AnimalBase 或可 GetComponent 到）。</param>
    /// <param name="params">节点参数表（可选）。</param>
    public BTContext(Component owner, IReadOnlyDictionary<string, string> @params = null)
    {
        _owner = owner;
        _animal = owner as AnimalBase ?? owner?.GetComponent<AnimalBase>();
        _board = _animal != null ? _animal.Board : null;
        _params = @params ?? EmptyParams;
    }

    public T GetComponent<T>() where T : class
    {
        if (_owner == null) return null;
        if (_owner is T direct) return direct;
        return _owner.GetComponent<T>();
    }

    public string GetParam(string key, string defaultValue = null)
        => _params.TryGetValue(key, out var value) ? value : defaultValue;

    public int GetParamInt(string key, int defaultValue = 0)
        => int.TryParse(GetParam(key), out var value) ? value : defaultValue;

    public float GetParamFloat(string key, float defaultValue = 0f)
        => float.TryParse(GetParam(key), out var value) ? value : defaultValue;
}
