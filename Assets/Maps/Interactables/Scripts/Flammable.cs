using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flammable : MonoBehaviour
{
    [Header("燃烧参数")]
    [Tooltip("被点燃后持续燃烧的时间（秒）")]
    public float burnDuration = 5f;
    [Tooltip("燃烧结束后是否销毁物体")]
    public bool destroyOnBurnOut = true;
    [Tooltip("燃烧结束后转换到的状态（预制体或游戏对象），留空则仅销毁或保持不变")]
    public GameObject burnOutStatePrefab;

    [Header("引燃参数")]
    [Tooltip("引燃其他可燃物的检测半径")]
    public float ignitionRadius = 1.0f;
    [Tooltip("引燃检测的间隔（秒）")]
    public float ignitionCheckInterval = 0.5f;
    [Tooltip("被引燃时，初始火焰粒子是否立即显示")]
    public bool showParticlesOnIgnite = true;

    [Header("烟雾参数")]
    [Tooltip("烟雾预制体（需包含SmokePuff脚本）")]
    public GameObject smokePuffPrefab;
    [Tooltip("烟雾产生间隔（秒）")]
    public float smokeEmitInterval = 0.3f;
    [Tooltip("烟雾团上升速度（单位/秒）")]
    public float smokeRiseSpeed = 1.5f;
    [Tooltip("烟雾柱宽度（约等于烟雾团直径）")]
    public float smokeColumnWidth = 1.0f;
    [Tooltip("烟雾达到此高度后自动消失")]
    public float smokeMaxHeight = 7f;
    [Tooltip("烟雾碰到墙体后持续存在的时间（秒）后消失")]
    public float smokeWallLife = 1f;

    [Header("粒子效果")]
    [Tooltip("火焰粒子系统（推荐使用ParticleSystem）")]
    public ParticleSystem fireParticles;

    [Header("层级检测")]
    [Tooltip("墙体所在的层级（Layer）")]
    public LayerMask wallLayerMask;

    // 内部状态
    private bool isBurning = false;
    private bool isExtinguished = false;
    private float burnTimer = 0f;
    private float ignitionTimer = 0f;
    private float smokeTimer = 0f;
    private Collider2D[] nearbyColliders = new Collider2D[20];
    private Collider2D myCollider;

    void Start()
    {
        // 获取自身的碰撞体
        myCollider = GetComponent<Collider2D>();

        if (fireParticles != null)
        {
            fireParticles.Stop();
            fireParticles.Clear();
        }
    }

    void Update()
    {
        if (!isBurning || isExtinguished)
            return;

        burnTimer += Time.deltaTime;
        if (burnTimer >= burnDuration)
        {
            OnBurnOut();
            return;
        }

        ignitionTimer += Time.deltaTime;
        if (ignitionTimer >= ignitionCheckInterval)
        {
            ignitionTimer = 0f;
            TryIgniteNearby();
        }

        if (smokePuffPrefab != null)
        {
            smokeTimer += Time.deltaTime;
            if (smokeTimer >= smokeEmitInterval)
            {
                smokeTimer = 0f;
                EmitSmokePuff();
            }
        }
    }

    /// <summary>
    /// 外部点火方法（如火焰触发器、玩家攻击等）
    /// </summary>
    public void Ignite()
    {
        if (isBurning || isExtinguished)
            return;

        isBurning = true;
        burnTimer = 0f;
        ignitionTimer = 0f;
        smokeTimer = 0f;

        if (fireParticles != null)
        {
            fireParticles.Play();
        }

        //Debug.Log(gameObject.name + " 被点燃！");
    }

    /// <summary>
    /// 燃烧结束处理
    /// </summary>
    private void OnBurnOut()
    {
        if (isExtinguished)
            return;

        isExtinguished = true;
        isBurning = false;

        if (fireParticles != null)
        {
            fireParticles.Stop();
            fireParticles.Clear();
        }

        if (burnOutStatePrefab != null)
        {
            GameObject newState = Instantiate(burnOutStatePrefab, transform.position, transform.rotation);
            newState.transform.SetParent(transform.parent);
        }

        if (destroyOnBurnOut)
        {
            Destroy(gameObject);
        }
        else
        {
            // 禁用碰撞体，防止继续交互
            if (myCollider != null)
            {
                myCollider.enabled = false;
            }
            enabled = false;
        }

        //Debug.Log(gameObject.name + " 燃烧结束。");
    }

    /// <summary>
    /// 检测并引燃附近的可燃物
    /// </summary>
    private void TryIgniteNearby()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            ignitionRadius,
            nearbyColliders
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = nearbyColliders[i];
            if (col.gameObject == gameObject)
                continue;

            Flammable otherFlammable = col.GetComponent<Flammable>();
            if (otherFlammable != null && !otherFlammable.isBurning && !otherFlammable.isExtinguished)
            {
                otherFlammable.Ignite();
                //Debug.Log(gameObject.name + " 引燃了 " + otherFlammable.gameObject.name);
            }
        }
    }

    /// <summary>
    /// 产生一个烟雾团
    /// </summary>
    private void EmitSmokePuff()
    {
        if (smokePuffPrefab == null)
            return;

        Vector3 spawnPos = transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0, 0);
        GameObject smoke = Instantiate(smokePuffPrefab, spawnPos, Quaternion.identity);

        SmokePuff smokeScript = smoke.GetComponent<SmokePuff>();
        if (smokeScript == null)
        {
            smokeScript = smoke.AddComponent<SmokePuff>();
        }

        smokeScript.riseSpeed = smokeRiseSpeed;
        smokeScript.maxHeight = smokeMaxHeight;
        smokeScript.wallLife = smokeWallLife;
        smokeScript.wallLayerMask = wallLayerMask;

        // 调整烟雾大小
        SpriteRenderer sr = smoke.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float originalWidth = sr.sprite.bounds.size.x;
            if (originalWidth > 0)
            {
                float scale = smokeColumnWidth / originalWidth;
                smoke.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        smokeScript.StartRising();
    }

    /// <summary>
    /// 碰撞检测 - 被火焰碰撞体点燃
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBurning || isExtinguished)
            return;

        // 检测碰撞对象是否为火焰
        if (collision.gameObject.CompareTag("Fire"))
        {
            Ignite();
        }
    }

    /// <summary>
    /// 持续碰撞检测 - 持续接触火焰
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!isBurning && !isExtinguished && collision.gameObject.CompareTag("Fire"))
        {
            Ignite();
        }
    }

    /// <summary>
    /// 触发器检测（兼容旧版触发器火焰）
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBurning || isExtinguished)
            return;

        if (other.CompareTag("Fire"))
        {
            Ignite();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isBurning && !isExtinguished && other.CompareTag("Fire"))
        {
            Ignite();
        }
    }

    /// <summary>
    /// 可视化检测半径
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, ignitionRadius);
    }
}