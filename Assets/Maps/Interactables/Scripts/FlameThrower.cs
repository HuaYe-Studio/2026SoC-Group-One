using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlameThrower : MonoBehaviour
{
    public enum SprayMode
    {
        Continuous,     // 持续喷射
        Pulse           // 脉冲喷射（间隔喷射）
    }
    public SprayMode sprayMode = SprayMode.Continuous;

    [Header("喷射参数")]
    [Tooltip("火焰预制体")]
    public GameObject flamePrefab;
    [Tooltip("喷射点（火焰生成位置）")]
    public Transform firePoint;
    [Tooltip("中心点（火焰从喷射点向远离中心点的方向喷射）")]
    public Transform centerPoint;  // 改为中心点
    [Tooltip("火焰喷射速度（单位/秒）")]
    public float flameSpeed = 5f;
    [Tooltip("每个火焰团的生命周期（秒）")]
    public float flameLifeTime = 2f;
    [Tooltip("火焰最远飞行距离（单位），0表示无限")]
    public float flameMaxDistance = 0f;

    [Header("脉冲模式参数")]
    [Tooltip("脉冲间隔时间（秒）")]
    public float pulseInterval = 1f;
    [Tooltip("每次脉冲喷射的火焰团数量")]
    public int flamesPerPulse = 3;

    [Header("火焰团参数")]
    [Tooltip("每批喷射的火焰团数量")]
    public int flamesPerBatch = 5;
    [Tooltip("火焰团之间的角度偏移范围（度）")]
    public float spreadAngle = 30f;
    [Tooltip("火焰团随机速度偏移范围")]
    public float speedVariance = 1.5f;

    [Header("火焰尺寸（固定1x1）")]
    [Tooltip("火焰宽度（单位）")]
    public float flameWidth = 1f;
    [Tooltip("火焰高度（单位）")]
    public float flameHeight = 1f;

    [Header("喷射方向")]
    [Tooltip("喷射方向（如果设置了中心点，此值将被动态覆盖）")]
    public Vector2 sprayDirection = Vector2.right;

    [Header("自动启动")]
    [Tooltip("场景加载后自动开始喷射")]
    public bool autoStart = true;
    [Tooltip("启动延迟（秒）")]
    public float startDelay = 0f;

    // 内部状态
    private bool isSpraying = false;
    private Coroutine sprayCoroutine = null;

    void Start()
    {
        if (flamePrefab == null)
        {
            Debug.LogError("FlameThrower: 未设置火焰预制体！");
            return;
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 如果未设置中心点，自动查找
        if (centerPoint == null)
        {
            GameObject centerObj = GameObject.FindGameObjectWithTag("Center");
            if (centerObj != null)
            {
                centerPoint = centerObj.transform;
                Debug.Log("自动找到中心点: " + centerPoint.name);
            }
            else
            {
                Debug.LogWarning("未设置中心点，将使用sprayDirection的固定方向");
            }
        }

        if (autoStart)
        {
            if (startDelay > 0)
            {
                Invoke(nameof(StartSpraying), startDelay);
            }
            else
            {
                StartSpraying();
            }
        }
    }

    void Update()
    {
        // 动态更新喷射方向：从中心点指向喷射点（即远离中心点的方向）
        if (centerPoint != null && firePoint != null)
        {
            Vector3 direction = firePoint.position - centerPoint.position;
            // 如果喷射点就在中心点位置，使用默认方向
            if (direction.magnitude > 0.001f)
            {
                sprayDirection = direction.normalized;
            }
            else
            {
                // 如果重合，使用默认方向
                sprayDirection = Vector2.right;
            }
        }
    }

    public void StartSpraying()
    {
        if (isSpraying || flamePrefab == null)
            return;

        isSpraying = true;

        if (sprayCoroutine != null)
            StopCoroutine(sprayCoroutine);

        sprayCoroutine = StartCoroutine(SprayFlames());
        Debug.Log(gameObject.name + " 火焰喷射器开始喷射");
    }

    public void StopSpraying()
    {
        if (!isSpraying)
            return;

        isSpraying = false;
        if (sprayCoroutine != null)
        {
            StopCoroutine(sprayCoroutine);
            sprayCoroutine = null;
        }
        Debug.Log(gameObject.name + " 火焰喷射器停止喷射");
    }

    private IEnumerator SprayFlames()
    {
        while (isSpraying)
        {
            SpawnFlameBatch(flamesPerBatch);

            float interval = 0.1f;
            if (sprayMode == SprayMode.Pulse)
            {
                interval = pulseInterval;
                yield return new WaitForSeconds(interval);
            }
            else
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    private void SpawnFlameBatch(int count)
    {
        if (flamePrefab == null || firePoint == null)
            return;

        // 如果是脉冲模式，只生成指定数量的火焰团
        int actualCount = sprayMode == SprayMode.Pulse ? flamesPerPulse : count;

        for (int i = 0; i < actualCount; i++)
        {
            SpawnSingleFlame();
        }
    }

    private void SpawnSingleFlame()
    {
        // 动态获取当前方向（在Update中已更新）
        Vector2 baseDirection = sprayDirection.normalized;

        // 如果方向为零向量，使用默认方向
        if (baseDirection == Vector2.zero)
        {
            baseDirection = Vector2.right;
        }

        float angleOffset = Random.Range(-spreadAngle, spreadAngle) * 0.5f;
        Vector2 direction = Quaternion.Euler(0, 0, angleOffset) * baseDirection;

        float speed = flameSpeed + Random.Range(-speedVariance, speedVariance);
        speed = Mathf.Max(speed, 0.5f);

        GameObject flame = Instantiate(flamePrefab, firePoint.position, Quaternion.identity);

        flame.transform.localScale = new Vector3(flameWidth, flameHeight, 1f);

        FlameProjectile flameScript = flame.GetComponent<FlameProjectile>();
        if (flameScript == null)
        {
            flameScript = flame.AddComponent<FlameProjectile>();
        }

        // 传递参数：速度、生命周期、最远距离
        flameScript.speed = speed;
        flameScript.lifeTime = flameLifeTime;
        flameScript.maxDistance = flameMaxDistance;
        flameScript.flameWidth = flameWidth;
        flameScript.flameHeight = flameHeight;
        flameScript.Initialize(direction * speed, flameLifeTime);

        flame.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));

        ParticleSystem ps = flame.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
        }
    }

    public void SetDirection(Vector2 newDirection)
    {
        sprayDirection = newDirection.normalized;
    }

    public void SetCenter(Transform newCenter)
    {
        centerPoint = newCenter;
    }

    public void ToggleSpray()
    {
        if (isSpraying)
            StopSpraying();
        else
            StartSpraying();
    }

    public bool IsSpraying()
    {
        return isSpraying;
    }

    private void OnDrawGizmosSelected()
    {
        if (firePoint != null)
        {
            // 绘制当前喷射方向
            Gizmos.color = Color.red;
            Vector3 direction = sprayDirection.normalized;
            if (direction == Vector3.zero)
                direction = Vector3.right;

            float angleStep = spreadAngle / 10f;
            for (int i = 0; i <= 10; i++)
            {
                float angle = -spreadAngle / 2 + angleStep * i;
                Vector3 dir = Quaternion.Euler(0, 0, angle) * direction;
                Gizmos.DrawRay(firePoint.position, dir * 2f);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(firePoint.position, direction * 3f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePoint.position, 0.2f);

            // 绘制从中心点到喷射点的连线（表示喷射方向来源）
            if (centerPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(centerPoint.position, firePoint.position);
                Gizmos.DrawWireSphere(centerPoint.position, 0.3f);

                // 在中心点绘制箭头指向喷射点
                Vector3 dirToFire = (firePoint.position - centerPoint.position).normalized;
                if (dirToFire.magnitude > 0.1f)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(centerPoint.position, dirToFire * 0.5f);
                }
            }

            // 绘制最远距离
            if (flameMaxDistance > 0)
            {
                Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
                Gizmos.DrawWireSphere(firePoint.position, flameMaxDistance);
            }
        }
    }
}