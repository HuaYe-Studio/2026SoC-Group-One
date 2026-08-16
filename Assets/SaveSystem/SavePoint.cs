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