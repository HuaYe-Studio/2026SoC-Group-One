using UnityEngine;

public class FlameProjectile : MonoBehaviour
{
    [Header("火焰参数")]
    [Tooltip("移动速度")]
    public float speed = 5f;
    [Tooltip("生命周期（秒）")]
    public float lifeTime = 2f;
    [Tooltip("最远飞行距离（单位），超过此距离自动销毁，0表示无限")]
    public float maxDistance = 0f;
    [Tooltip("是否受重力影响")]
    public bool useGravity = false;
    [Tooltip("重力系数")]
    public float gravityScale = 0.5f;

    [Header("火焰尺寸")]
    [Tooltip("火焰宽度（单位）")]
    public float flameWidth = 1f;
    [Tooltip("火焰高度（单位）")]
    public float flameHeight = 1f;

    private Vector2 velocity;
    private float lifeTimer = 0f;
    private float distanceTraveled = 0f;
    private bool isInitialized = false;
    private Rigidbody2D rb;
    private Vector3 startPosition;
    private SpriteRenderer sr;
    private Color originalColor;

    void Start()
    {
        if (!isInitialized)
        {
            velocity = transform.right * speed;
        }

        // 保存起始位置
        startPosition = transform.position;

        // 确保尺寸为1x1
        transform.localScale = new Vector3(flameWidth, flameHeight, 1f);

        // 保存原始颜色
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalColor = sr.color;
        }

        if (useGravity)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            rb.velocity = velocity;
        }
    }

    void Update()
    {
        if (!isInitialized)
            return;

        // 生命周期计时
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // 移动
        Vector3 movement = (Vector3)velocity * Time.deltaTime;
        transform.position += movement;

        // 累计飞行距离
        distanceTraveled += movement.magnitude;

        // 最远距离检测
        if (maxDistance > 0 && distanceTraveled >= maxDistance)
        {
            Destroy(gameObject);
            return;
        }

        // 逐渐缩小消散
        float progress = lifeTimer / lifeTime;
        float scale = Mathf.Lerp(1f, 0f, progress);
        transform.localScale = new Vector3(flameWidth * scale, flameHeight * scale, 1f);
    }

    /// <summary>
    /// 初始化火焰（由FlameThrower调用）
    /// </summary>
    public void Initialize(Vector2 initialVelocity, float lifetime)
    {
        velocity = initialVelocity;
        lifeTime = lifetime;
        isInitialized = true;
        lifeTimer = 0f;
        distanceTraveled = 0f;
        startPosition = transform.position;

        // 确保尺寸为1x1
        transform.localScale = new Vector3(flameWidth, flameHeight, 1f);

        // 恢复原始颜色
        if (sr != null)
        {
            sr.color = originalColor;
        }

        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }

    /// <summary>
    /// 设置方向
    /// </summary>
    public void SetDirection(Vector2 direction)
    {
        velocity = direction.normalized * speed;
        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }

    /// <summary>
    /// 设置速度
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        velocity = velocity.normalized * speed;
        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }

    /// <summary>
    /// 可视化调试
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(flameWidth, flameHeight, 0));

        // 绘制最远距离范围
        if (maxDistance > 0 && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(startPosition, maxDistance);
        }
    }
}