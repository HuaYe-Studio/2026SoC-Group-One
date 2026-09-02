using System;

/// <summary>
/// [BT] 节点注册特性（阶段 3.1）：标注节点注册名，BTNodeFactory 启动时反射扫描收集。
/// 用法：[BTNode("Flee")] public class BTFleeAction : BTNode
/// 配合 IBTContext 构造器，新增节点 = 写类 + 一行特性 + 一行构造器，无需改工厂。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class BTNodeAttribute : Attribute
{
    /// <summary>节点注册名（数据驱动/JSON 树中引用的名字）。</summary>
    public string Name { get; }

    public BTNodeAttribute(string name)
    {
        Name = name;
    }
}
