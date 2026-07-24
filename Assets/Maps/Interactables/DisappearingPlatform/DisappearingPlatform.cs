using System.Collections;
using UnityEngine;

public class DisappearingPlatform : MonoBehaviour
{
    [Header("时间设置")]
    [Tooltip("接触后多久消失（秒）")]
    public float delayBeforeDisappear = 0.5f;

    [Tooltip("消失后多久重新出现（秒）")]
    public float delayBeforeReappear = 0.5f;

    [Header("组件引用")]
    public SpriteRenderer spriteRenderer;
    public Collider2D platformCollider; 

    private bool isTriggered = false; // 防止在倒计时期间被重复触发

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (platformCollider == null) platformCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isTriggered && collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(DisappearAndReappearRoutine());
        }
    }

    private IEnumerator DisappearAndReappearRoutine()
    {
        isTriggered = true;

        // 1. 等待指定的接触延迟时间
        yield return new WaitForSeconds(delayBeforeDisappear);

        // 2. 隐藏物体
        SetPlatformState(false);

        // 3. 等待指定的消失持续时间
        yield return new WaitForSeconds(delayBeforeReappear);

        // 4. 重新显示物体
        SetPlatformState(true);

        isTriggered = false; // 重置触发状态，允许下一次踩踏
    }

    // 统一控制物体的显示/隐藏和碰撞状态
    private void SetPlatformState(bool active)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = active;
        }

        if (platformCollider != null)
        {
            platformCollider.enabled = active;
        }
    }
}