using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform PosA, PosB;
    public Vector2 PosStart, PosEnd;
    [SerializeField] float movespeed;
    [SerializeField] float pausetime = 0f;
    private bool movingToEnd = true;
    private float pauseTimer = 0f;

    private Rigidbody2D rb;
    private Vector2 targetPosition;

    // 当前踩在平台上、随平台水平移动的刚体（玩家/动物）。由顶部 trigger 检测进出。
    private readonly HashSet<Rigidbody2D> _riders = new HashSet<Rigidbody2D>();
    private Vector2 _lastPos;

    private void Start()
    {
        if (PosA != null) PosStart = PosA.position;
        if (PosB != null) PosEnd = PosB.position;
        rb = GetComponent<Rigidbody2D>();
        rb.position = PosStart;
        _lastPos = rb.position;
        targetPosition = PosEnd;
        movingToEnd = true;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // 上一物理步的水平位移：solid 碰撞体已承载垂直方向，玩家无摩擦材质，
        // 水平方向只能靠这里直接搬刚体位置跟上平台。
        float deltaX = rb.position.x - _lastPos.x;
        _lastPos = rb.position;

        MovePlatform();

        if (deltaX != 0f)
        {
            foreach (Rigidbody2D rider in _riders)
            {
                if (rider != null)
                    rider.position += new Vector2(deltaX, 0f);
            }
        }
    }

    private void MovePlatform()
    {
        float distance = Vector2.Distance(rb.position, targetPosition);
        if (distance < 0.05f)
        {
            rb.velocity = Vector2.zero;
            if (pausetime > 0f)
            {
                pauseTimer += Time.fixedDeltaTime;
                if (pauseTimer < pausetime)
                    return;
                pauseTimer = 0f;
            }
            movingToEnd = !movingToEnd;
            targetPosition = movingToEnd ? PosEnd : PosStart;
        }
        else
        {
            Vector2 direction = (targetPosition - rb.position).normalized;
            rb.velocity = direction * movespeed;
        }
    }

    private static bool IsRideableLayer(int layer)
    {
        return layer == LayerMask.NameToLayer("Animal") || layer == LayerMask.NameToLayer("Player");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsRideableLayer(other.gameObject.layer)) return;
        if (other.attachedRigidbody != null)
            _riders.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody != null)
            _riders.Remove(other.attachedRigidbody);
    }
}
