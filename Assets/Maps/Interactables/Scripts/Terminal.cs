using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class Terminal : MonoBehaviour
{
    [Header("Signal Sources")]
    [SerializeField] private List<SignalSource> _signalSources = new List<SignalSource>();

    [Header("Trigger Mode")]
    [SerializeField] private TriggerMode _triggerMode = TriggerMode.Any;

    [Header("Events")]
    public UnityEvent<bool> OnStateChanged;
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    private bool _isActive;

    public bool IsActive => _isActive;

    public enum TriggerMode
    {
        Any,        // 任意一个信号源激活即可触发
        All,        // 所有信号源都必须激活
        Majority    // 超过一半的信号源激活
    }

    private void Start()
    {
        UpdateState();
    }

    private void Update()
    {
        bool newState = CalculateState();
        if (newState != _isActive)
        {
            SetState(newState);
        }
    }

    public void SetSignalSources(List<SignalSource> sources)
    {
        _signalSources = sources ?? new List<SignalSource>();
        UpdateState();
    }

    public void AddSignalSource(SignalSource source)
    {
        if (source != null && !_signalSources.Contains(source))
        {
            _signalSources.Add(source);
            UpdateState();
        }
    }

    public void RemoveSignalSource(SignalSource source)
    {
        if (_signalSources.Remove(source))
        {
            UpdateState();
        }
    }

    public void ClearSignalSources()
    {
        _signalSources.Clear();
        UpdateState();
    }

    private bool CalculateState()
    {
        if (_signalSources.Count == 0) return false;

        int activeCount = 0;
        foreach (var source in _signalSources)
        {
            if (source != null && source.IsActive)
            {
                activeCount++;
            }
        }

        switch (_triggerMode)
        {
            case TriggerMode.Any:
                return activeCount > 0;
            case TriggerMode.All:
                return activeCount == _signalSources.Count;
            case TriggerMode.Majority:
                return activeCount > _signalSources.Count / 2f;
            default:
                return false;
        }
    }

    private void UpdateState()
    {
        SetState(CalculateState());
    }

    private void SetState(bool state)
    {
        if (_isActive == state) return;

        _isActive = state;

        OnStateChanged?.Invoke(_isActive);

        if (_isActive)
        {
            OnActivated?.Invoke();
        }
        else
        {
            OnDeactivated?.Invoke();
        }
    }
}