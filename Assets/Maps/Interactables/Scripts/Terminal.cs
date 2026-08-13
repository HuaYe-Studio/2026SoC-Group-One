using UnityEngine;
using UnityEngine.Events;

public class Terminal : MonoBehaviour
{
    [Header("Signal Source")]
    [SerializeField] private SignalSource _signalSource;

    [Header("Events")]
    public UnityEvent<bool> OnStateChanged;
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    private bool _isActive;

    public bool IsActive => _isActive;

    private void Start()
    {
        if (_signalSource != null)
        {
            OnSignalReceived(_signalSource.GetSignal());
        }
    }

    private void Update()
    {
        if (_signalSource != null && _signalSource.IsActive != _isActive)
        {
            OnSignalReceived(_signalSource.IsActive);
        }
    }

    public void SetSignalSource(SignalSource source)
    {
        _signalSource = source;

        if (_signalSource != null)
        {
            OnSignalReceived(_signalSource.GetSignal());
        }
    }

    private void OnSignalReceived(bool signalState)
    {
        SetState(signalState);
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