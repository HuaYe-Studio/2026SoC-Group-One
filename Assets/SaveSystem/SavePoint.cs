using UnityEngine;

public class SavePoint : MonoBehaviour
{
    // 存档点的精确复活位置
    [SerializeField] private Transform spawnPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只有玩家触碰时才触发存档
        if (other.CompareTag("Player"))
        {
            // 场景转场中不触发存档，避免读档落地立即重新保存
            if (SceneTransition.Instance != null && SceneTransition.Instance.IsTransitioning)
                return;

            // SaveManager未初始化时给出明确错误并返回
            if (SaveManager.Instance == null)
            {
                Debug.LogError("SaveManager.Instance 为空，无法保存存档！");
                return;
            }

            // 复活传送抑制期间不触发存档
            if (SaveManager.Instance.IsSavePointSaveSuppressed)
                return;

            // 获取存档坐标：如果有指定spawnPoint就用它的位置，否则用自身位置
            Vector2 pos = spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)transform.position;

            // 调用存档管理器保存
            SaveManager.Instance.SaveGame(pos);

            // 简单的调试提示（以后替换成UI“已存档”字样）
            Debug.Log("💾 存档点已触发！");
        }
    }

    // 在场景中画一个小图标方便调试（可选）
    private void OnDrawGizmos()
    {
        Vector2 pos = spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, 0.3f);
    }
}