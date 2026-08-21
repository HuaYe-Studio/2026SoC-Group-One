using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private Transform closedPoint;
    [SerializeField] private Transform openPoint;
    [SerializeField] private float slideSpeed = 5f;
    [SerializeField] private bool startOpen = false;

    [Header("Terminal")]
    [SerializeField] private Terminal terminal;
    [SerializeField] private bool autoFindTerminal = true;

    private Rigidbody2D rb;
    private Vector2 closedPos;
    private Vector2 openPos;
    private bool isOpen;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        // 关键设置：IsKinematic 让门不受外力影响
        rb.isKinematic = true;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (autoFindTerminal && terminal == null)
            terminal = GetComponent<Terminal>();

        closedPos = closedPoint != null ? (Vector2)closedPoint.position : (Vector2)transform.position;
        openPos = openPoint != null ? (Vector2)openPoint.position : closedPos + Vector2.right * 3f;

        rb.position = startOpen ? openPos : closedPos;
        isOpen = startOpen;

        if (terminal != null)
        {
            terminal.OnStateChanged.AddListener(OnTerminalChanged);
            OnTerminalChanged(terminal.IsActive);
        }
    }

    private void FixedUpdate()
    {
        Vector2 targetPos = isOpen ? openPos : closedPos;

        // 使用 MovePosition 控制 kinematic rigidbody
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, slideSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    private void OnTerminalChanged(bool state)
    {
        isOpen = state;
    }

    public void SetTerminal(Terminal newTerminal)
    {
        if (terminal != null)
            terminal.OnStateChanged.RemoveListener(OnTerminalChanged);

        terminal = newTerminal;

        if (terminal != null)
        {
            terminal.OnStateChanged.AddListener(OnTerminalChanged);
            OnTerminalChanged(terminal.IsActive);
        }
    }

    public void ForceOpen() => isOpen = true;
    public void ForceClose() => isOpen = false;
    public void Toggle() => isOpen = !isOpen;
}