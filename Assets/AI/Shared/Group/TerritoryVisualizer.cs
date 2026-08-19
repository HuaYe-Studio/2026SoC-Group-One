using UnityEngine;

/// <summary>
/// [调试] 领地可视化：在 Scene 视图绘制所有已分配领地椭圆（水平扁椭圆，贴合 2D 横板），
/// 用于目视确认领地分布与重叠情况。
/// 用法：将本组件挂到场景任意常驻对象（建议一个空物体，如 "TerritoryDebug"）。
/// 领地由 TerritoryManager 在运行时统一分配，因此需进入 Play 模式后才能在 Scene 视图看到。
/// 颜色：个体领地=青色，共享群领地=品红，中心用白色十字标记。
/// </summary>
public class TerritoryVisualizer : MonoBehaviour
{
    [Tooltip("个体领地颜色")]
    [SerializeField] private Color _individualColor = new Color(0f, 1f, 1f, 0.6f);

    [Tooltip("共享群领地颜色（鱼群等）")]
    [SerializeField] private Color _sharedColor = new Color(1f, 0f, 1f, 0.6f);

    [Tooltip("进入 Play 首帧打印领地分配诊断到 Console")]
    [SerializeField] private bool _logDiagnostics = true;

    private float _diagnoseTimer;

    private void Update()
    {
        if (!_logDiagnostics || _diagnoseTimer != 0f) return;
        _diagnoseTimer = 1f; // 只打印一次，等首帧分配完成后

        NavGrid2D grid = NavGrid2D.Instance;
        Debug.Log($"[Territory] NavGrid2D.Instance={(grid != null ? "存在" : "null(分配会失败)")}");

        int count = 0;
        foreach (Territory t in TerritoryManager.All())
        {
            count++;
            Debug.Log($"[Territory] {t.OwnerKey} Center={t.Center} RadiusX={t.RadiusX} RadiusY={t.RadiusY} IsShared={t.IsShared}");
        }
        Debug.Log(count == 0
            ? "[Territory] ⚠ 无已分配领地：要么 EnsureAssigned 未执行，要么动物未注册"
            : $"[Territory] 共分配 {count} 个领地");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        foreach (Territory t in TerritoryManager.All())
        {
            Vector3 center = new Vector3(t.Center.x, t.Center.y, 0f);

            Gizmos.color = t.IsShared ? _sharedColor : _individualColor;
            DrawWireEllipse(center, t.RadiusX, t.RadiusY);

            // 中心十字标记，便于定位领地中心
            Gizmos.color = Color.white;
            const float cross = 0.4f;
            Gizmos.DrawLine(center + Vector3.left * cross, center + Vector3.right * cross);
            Gizmos.DrawLine(center + Vector3.down * cross, center + Vector3.up * cross);
        }
    }

    /// <summary>用分段折线近似画椭圆（Gizmos 无原生椭圆）。</summary>
    private static void DrawWireEllipse(Vector3 center, float radiusX, float radiusY)
    {
        const int segments = 64;
        Vector3 prev = center + new Vector3(radiusX, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
