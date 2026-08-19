using UnityEngine;

public class SmokePuff : MonoBehaviour
{
    [Header("烟雾参数")]
    [Tooltip("上升速度（单位/秒）")]
    public float riseSpeed = 1.5f;
    [Tooltip("最大上升高度（单位）")]
    public float maxHeight = 7f;
    [Tooltip("触碰墙体后存活时间（秒）")]
    public float wallLife = 1f;

    [Header("墙体检测")]
    [Tooltip("墙体所在的层级")]
    public LayerMask wallLayerMask;

    private float startY;
    private bool isRising = false;
    private bool isTouchingWall = false;
    private float wallTimer = 0f;
    private Rigidbody2D rb;

    void Start()
    {
        // 如果没有手动设置，默认检测"Wall"层级
        if (wallLayerMask == 0)
        {
            wallLayerMask = LayerMask.GetMask("Wall");
        }

        // 获取刚体组件（如果有）
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!isRising) return;

        // 如果碰到墙体，停止上升并计时
        if (isTouchingWall)
        {
            wallTimer += Time.deltaTime;
            if (wallTimer >= wallLife)
            {
                Destroy(gameObject);
            }
            return; // 不执行上升逻辑
        }

        // 正常上升
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // 检查是否达到最大高度
        if (transform.position.y - startY >= maxHeight)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 开始上升（由Flammable脚本调用）
    /// </summary>
    public void StartRising()
    {
        startY = transform.position.y;
        isRising = true;

        // 如果有刚体，设置为运动学模式防止物理干扰
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// 碰撞检测（墙体）
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 检测是否为墙体
        if (wallLayerMask == (wallLayerMask | (1 << collision.gameObject.layer)))
        {
            // 停止上升
            isTouchingWall = true;
            wallTimer = 0f;

            // 清除刚体速度（如果有）
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            Debug.Log("烟雾碰到墙体，停止上升，将在 " + wallLife + " 秒后消失");
        }
    }

    /// <summary>
    /// 持续碰撞检测（防止卡墙）
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (wallLayerMask == (wallLayerMask | (1 << collision.gameObject.layer)))
        {
            // 确保仍然标记为触碰墙体状态
            if (!isTouchingWall)
            {
                isTouchingWall = true;
                wallTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 触发器检测（如果墙体使用Trigger）
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (wallLayerMask == (wallLayerMask | (1 << other.gameObject.layer)))
        {
            isTouchingWall = true;
            wallTimer = 0f;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            Debug.Log("烟雾碰到墙体（Trigger），停止上升");
        }
    }

    /// <summary>
    /// 可视化检测
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 topPoint = transform.position + Vector3.up * maxHeight;
        Gizmos.DrawLine(transform.position, topPoint);
        Gizmos.DrawWireSphere(topPoint, 0.3f);
    }
}