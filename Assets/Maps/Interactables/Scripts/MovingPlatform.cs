using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform PosA, PosB;
    public Vector2 PosStart, PosEnd;
    [SerializeField] float movespeed;
    [SerializeField] float pausetime=0f;
    private bool movingToEnd = true;
    private float pauseTimer = 0f;

    private Rigidbody2D rb;
    private Vector2 targetPosition;
    private void Start()
    {
        if(PosA != null)PosStart=PosA.position;
        if (PosB != null)PosEnd=PosB.position;
        rb = GetComponent<Rigidbody2D>();
        rb.position = PosStart;
        targetPosition = PosEnd;
        movingToEnd = true;
    }
    private void FixedUpdate()
    {
        if(rb==null) return;
        float distance=Vector2.Distance(rb.position, targetPosition);
        if(distance<0.05f)
        {
            rb.velocity=Vector2.zero;
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
            Vector2 direction=(targetPosition - rb.position).normalized;
            rb.velocity=direction*movespeed;
        }
    }
    private static bool IsRideableLayer(int layer)
    {
        return layer == LayerMask.NameToLayer("Animal") || layer == LayerMask.NameToLayer("Player");
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsRideableLayer(collision.gameObject.layer))
        {
            // 挂到根节点：动物/玩家都随平台移动（玩家碰撞体在形态子物体上，取其 root）
            collision.transform.root.SetParent(transform);
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        Transform root = collision.transform.root;
        if (IsRideableLayer(collision.gameObject.layer) && root.parent == transform)
        {
            root.SetParent(null);
        }
    }
}