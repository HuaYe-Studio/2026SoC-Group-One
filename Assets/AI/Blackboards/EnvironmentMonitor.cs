using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 环境监视器：挂载到动物NPC上，负责检测周围环境信息。
/// 包括威胁、食物、地形、同类等，供各状态行为查询使用。
/// </summary>
public class EnvironmentMonitor : MonoBehaviour
{
    [Header("Threat Detection")]
    [SerializeField] private float _threatRadius = 8f;
    [SerializeField] private LayerMask _threatLayer;
    [SerializeField] private float _fleeRadius = 5f;

    [Header("Food Detection")]
    [SerializeField] private float _foodRadius = 10f;
    [SerializeField] private LayerMask _foodLayer;

    [Header("Terrain Detection")]
    [SerializeField] private float _groundCheckDistance = 1.5f;
    [SerializeField] private float _wallCheckDistance = 1f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Fellow Detection")]
    [SerializeField] private float _fellowRadius = 5f;
    [SerializeField] private LayerMask _fellowLayer;

    // 威胁信息
    public bool IsThreatDetected { get; private set; }
    public Vector2 ThreatDirection { get; private set; }
    public float ThreatDistance { get; private set; }
    public Transform NearestThreat { get; private set; }
    public float ThreatRadius => _threatRadius;
    public float FleeRadius => _fleeRadius;

    // 食物信息
    public bool IsFoodDetected { get; private set; }
    public Vector2 FoodDirection { get; private set; }
    public float FoodDistance { get; private set; }
    public Transform NearestFood { get; private set; }
    public float FoodRadius => _foodRadius;

    // 地形信息
    public bool IsGroundedAhead { get; private set; }
    public bool IsWallAhead { get; private set; }
    public bool IsGapAhead { get; private set; }

    // 同类信息
    public List<Transform> NearbyFellows { get; private set; } = new List<Transform>();
    public int FellowCount => NearbyFellows.Count;
    public float FellowRadius => _fellowRadius;

    private void Update()
    {
        DetectThreats();
        DetectFood();
        DetectTerrain();
        DetectFellows();
    }

    private void DetectThreats()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _threatRadius, _threatLayer);

        float nearestDist = float.MaxValue;
        IsThreatDetected = false;
        NearestThreat = null;

        foreach (Collider2D hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                NearestThreat = hit.transform;
                IsThreatDetected = true;
            }
        }

        if (IsThreatDetected)
        {
            Vector2 toThreat = NearestThreat.position - transform.position;
            ThreatDirection = toThreat.normalized;
            ThreatDistance = nearestDist;
        }
    }

    private void DetectFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _foodRadius, _foodLayer);

        float nearestDist = float.MaxValue;
        IsFoodDetected = false;
        NearestFood = null;

        foreach (Collider2D hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                NearestFood = hit.transform;
                IsFoodDetected = true;
            }
        }

        if (IsFoodDetected)
        {
            Vector2 toFood = NearestFood.position - transform.position;
            FoodDirection = toFood.normalized;
            FoodDistance = nearestDist;
        }
    }

    private void DetectTerrain()
    {
        Vector2 forward = transform.localScale.x >= 0 ? Vector2.right : Vector2.left;

        // 前方地面检测（向下 + 前方）
        Vector2 groundCheckOrigin = (Vector2)transform.position + forward * 0.3f;
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down,
            _groundCheckDistance, _groundLayer);
        IsGroundedAhead = groundHit.collider != null;
        IsGapAhead = !IsGroundedAhead;

        // 前方墙壁检测
        Vector2 wallOrigin = (Vector2)transform.position + Vector2.up * 0.3f;
        RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, forward,
            _wallCheckDistance, _groundLayer);
        IsWallAhead = wallHit.collider != null;
    }

    private void DetectFellows()
    {
        NearbyFellows.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _fellowRadius, _fellowLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit.transform != transform)
                NearbyFellows.Add(hit.transform);
        }
    }

    /// <summary>
    /// 获取当前朝向的前方方向向量。
    /// </summary>
    public Vector2 GetForwardDirection()
    {
        return transform.localScale.x >= 0 ? Vector2.right : Vector2.left;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 威胁探测范围
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _threatRadius);

        // 逃跑触发范围
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _fleeRadius);

        // 食物探测范围
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _foodRadius);

        // 同类探测范围
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _fellowRadius);

        // 地形检测射线
        Vector2 forward = Application.isPlaying
            ? GetForwardDirection()
            : (transform.localScale.x >= 0 ? Vector2.right : Vector2.left);

        Gizmos.color = IsGapAhead ? Color.red : Color.green;
        Vector2 groundOrigin = (Vector2)transform.position + forward * 0.3f;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * _groundCheckDistance);

        Gizmos.color = IsWallAhead ? Color.red : Color.blue;
        Vector2 wallOrigin = (Vector2)transform.position + Vector2.up * 0.3f;
        Gizmos.DrawLine(wallOrigin, wallOrigin + forward * _wallCheckDistance);

        // 最近威胁连线
        if (NearestThreat != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, NearestThreat.position);
        }

        // 最近食物连线
        if (NearestFood != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, NearestFood.position);
        }
    }
#endif
}
