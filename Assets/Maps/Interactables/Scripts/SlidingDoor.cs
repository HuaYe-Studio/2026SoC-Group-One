using UnityEngine;

public class SlidingDoor : MonoBehaviour, IMovable
{
    [Header("Door Settings")]
    [SerializeField] private Transform _closedPoint;
    [SerializeField] private Transform _openPoint;
    [SerializeField] private float _slideSpeed = 5f;
    [SerializeField] private bool _startOpen = false;

    [Header("Movable Settings")]
    [SerializeField] private bool _canMove = true;
    [SerializeField] private bool _freezeX = false;
    [SerializeField] private bool _freezeY = false;

    [Header("Terminal Binding")]
    [SerializeField] private Terminal _terminal;
    [SerializeField] private bool _autoFindTerminal = true;

    private Vector2 _closedPos;
    private Vector2 _openPos;
    private float _progress;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool CanMove => _canMove;

    private void Start()
    {
        if (_autoFindTerminal && _terminal == null)
        {
            _terminal = GetComponent<Terminal>();
        }

        _closedPos = _closedPoint != null
            ? (Vector2)_closedPoint.position
            : (Vector2)transform.position;

        _openPos = _openPoint != null
            ? (Vector2)_openPoint.position
            : _closedPos + Vector2.right * 3f;

        _progress = _startOpen ? 1f : 0f;
        transform.position = _startOpen ? _openPos : _closedPos;
        _isOpen = _startOpen;

        if (_terminal != null)
        {
            _terminal.OnStateChanged.AddListener(OnTerminalStateChanged);
            OnTerminalStateChanged(_terminal.IsActive);
        }
    }

    private void Update()
    {
        if (_canMove)
        {
            float targetProgress = _isOpen ? 1f : 0f;
            _progress = Mathf.MoveTowards(_progress, targetProgress, _slideSpeed * Time.deltaTime);
            transform.position = Vector2.Lerp(_closedPos, _openPos, _progress);
        }
    }

    private void OnTerminalStateChanged(bool state)
    {
        _isOpen = state;
    }

    public void SetTerminal(Terminal terminal)
    {
        if (_terminal != null)
        {
            _terminal.OnStateChanged.RemoveListener(OnTerminalStateChanged);
        }

        _terminal = terminal;

        if (_terminal != null)
        {
            _terminal.OnStateChanged.AddListener(OnTerminalStateChanged);
            OnTerminalStateChanged(_terminal.IsActive);
        }
    }

    public void OpenDoor()
    {
        if (_isOpen) return;
        _isOpen = true;
    }

    public void CloseDoor()
    {
        if (!_isOpen) return;
        _isOpen = false;
    }

    public void ToggleDoor()
    {
        _isOpen = !_isOpen;
    }

    public void SetDoorState(bool open)
    {
        if (_isOpen == open) return;
        _isOpen = open;
    }

    public void SetCanMove(bool canMove) => _canMove = canMove;

    public void Move(Vector2 delta)
    {
        if (!_canMove) return;

        if (_freezeX) delta.x = 0;
        if (_freezeY) delta.y = 0;

        if (delta.magnitude <= 0.001f) return;

        transform.position += (Vector3)delta;
        _closedPos += delta;
        _openPos += delta;
    }

    public Vector2 GetPosition() => transform.position;

    private void OnDestroy()
    {
        if (_terminal != null)
        {
            _terminal.OnStateChanged.RemoveListener(OnTerminalStateChanged);
        }
    }
}

public interface IMovable
{
    bool CanMove { get; }
    void Move(Vector2 delta);
    Vector2 GetPosition();
}