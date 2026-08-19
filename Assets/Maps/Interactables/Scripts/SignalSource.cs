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

    private Collider2D[] detectedObjects;
    private bool previousSignalState;

    public bool IsActive => isActive;

    public enum DetectionShape
    {
        Rectangle,
        Circle
    }

    private void Start()
    {
        previousSignalState = isActive;
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
        detectedObjects = Physics2D.OverlapBoxAll(transform.position, detectionSize, 0f, detectionLayer);

        foreach (Collider2D col in detectedObjects)
        {
            IHeavy heavy = col.GetComponent<IHeavy>();
            if (heavy != null && heavy.IsHeavy)
            {
                return true;
            }
        }

        return false;
    }

    private bool DetectInCircle()
    {
        detectedObjects = Physics2D.OverlapCircleAll(transform.position, detectionRadius, detectionLayer);

        foreach (Collider2D col in detectedObjects)
        {
            IHeavy heavy = col.GetComponent<IHeavy>();
            if (heavy != null && heavy.IsHeavy)
            {
                return true;
            }
        }

        return false;
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