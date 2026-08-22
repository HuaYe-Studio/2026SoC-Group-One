using UnityEngine;
using UnityEngine.Events;

public class SignalSource : MonoBehaviour
{
    [Header("Signal Settings")]
    [SerializeField] private bool isActive = false;
    [SerializeField] private bool detectHeavyObject = true;

    [Header("Detection Settings")]
    [SerializeField] private DetectionShape detectionShape = DetectionShape.Rectangle;
    [SerializeField] private Vector2 detectionSize = new Vector2(1f, 0.5f);
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask detectionLayer = -1;

    [Header("Events")]
    public UnityEvent<bool> onSignalChanged;
    public UnityEvent onSignalOn;
    public UnityEvent onSignalOff;

    private static readonly Collider2D[] _hitBuffer = new Collider2D[16];

    public bool IsActive => isActive;

    public enum DetectionShape
    {
        Rectangle,
        Circle
    }

    private void FixedUpdate()
    {
        if (detectHeavyObject)
        {
            DetectHeavyObjects();
        }
    }

    private void DetectHeavyObjects()
    {
        bool hasHeavyObject = false;

        switch (detectionShape)
        {
            case DetectionShape.Rectangle:
                hasHeavyObject = DetectInRectangle();
                break;
            case DetectionShape.Circle:
                hasHeavyObject = DetectInCircle();
                break;
        }

        SetSignal(hasHeavyObject);
    }

    private bool DetectInRectangle()
    {
        int hitCount = Physics2D.OverlapBoxNonAlloc(transform.position, detectionSize, 0f, _hitBuffer, detectionLayer);

        if (hitCount > 0) LogDetectionResult(hitCount, "Box");

        for (int i = 0; i < hitCount; i++)
        {
            // GetComponentInParent：IHeavy 可能挂在玩家根节点上（持有 HeavyStone 时
            // SetPlayerHeavy 加到 "Player" 根），而碰撞体在形态子物体上
            IHeavy heavy = _hitBuffer[i].GetComponentInParent<IHeavy>();
            if (heavy != null && heavy.IsHeavy)
            {
                return true;
            }
        }

        return false;
    }

    private bool DetectInCircle()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, _hitBuffer, detectionLayer);

        if (hitCount > 0) LogDetectionResult(hitCount, "Circle");

        for (int i = 0; i < hitCount; i++)
        {
            IHeavy heavy = _hitBuffer[i].GetComponentInParent<IHeavy>();
            if (heavy != null && heavy.IsHeavy)
            {
                return true;
            }
        }

        return false;
    }

    // 诊断：打印检测框内找到的碰撞体与 IHeavy，便于定位压力板检测断点
    private void LogDetectionResult(int hitCount, string shape)
    {
        string info = $"[SignalSource] {name} {shape} hits={hitCount}:";
        for (int i = 0; i < hitCount && i < _hitBuffer.Length; i++)
        {
            if (_hitBuffer[i] == null) continue;
            IHeavy heavy = _hitBuffer[i].GetComponentInParent<IHeavy>();
            info += $" [{_hitBuffer[i].name}|{(_hitBuffer[i].gameObject.layer)}|heavy={heavy?.IsHeavy}]";
        }
        Debug.Log(info);
    }

    public void SetSignal(bool state)
    {
        if (isActive != state)
        {
            isActive = state;
            OnSignalChanged();
        }
    }

    private void OnSignalChanged()
    {
        onSignalChanged?.Invoke(isActive);

        if (isActive)
        {
            onSignalOn?.Invoke();
        }
        else
        {
            onSignalOff?.Invoke();
        }
    }

    public bool GetSignal()
    {
        return isActive;
    }

    public void ForceDetect()
    {
        DetectHeavyObjects();
    }

    public void SetRectangleDetection(Vector2 size)
    {
        detectionShape = DetectionShape.Rectangle;
        detectionSize = size;
    }

    public void SetCircleDetection(float radius)
    {
        detectionShape = DetectionShape.Circle;
        detectionRadius = radius;
    }
}

public interface IHeavy
{
    bool IsHeavy { get; }
}