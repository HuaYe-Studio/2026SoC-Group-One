using UnityEngine;

/// <summary>
/// [BT] 行为树运行器：挂载到 GameObject 上，持有根节点和黑板，每帧 Tick 行为树。
/// 使用方式：SetRoot(BTNode)，行为树生命周期由 Update 驱动。
/// </summary>
[RequireComponent(typeof(BTBlackboard))]
public class BehaviorTreeRunner : MonoBehaviour
{
    [SerializeField] private bool _enableDebugLog;

    private BTNode _root;
    private BTBlackboard _blackboard;

    public BTBlackboard Blackboard => _blackboard;

    private void Awake()
    {
        _blackboard = GetComponent<BTBlackboard>();
    }

    /// <summary>
    /// 设置行为树的根节点。一般外部在 Start 或 BuildTree 中调用。
    /// </summary>
    public void SetRoot(BTNode root)
    {
        _root = root;
        _root?.OnEnter();
    }

    private void Update()
    {
        if (_root == null) return;

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            Debug.Log($"[BT] Tick result: {result}");
    }

    private void OnDisable()
    {
        _root?.OnExit();
    }
}
