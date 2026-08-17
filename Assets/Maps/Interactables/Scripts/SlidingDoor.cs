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

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (autoFindTerminal && terminal == null)
            terminal = GetComponent<Terminal>();

        closedPos = closedPoint != null ? (Vector2)closedPoint.position : (Vector2)transform.position;
        openPos = openPoint != null ? (Vector2)openPoint.position : closedPos + Vector2.right * 3f;

        transform.position = startOpen ? openPos : closedPos;
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
        float distance = Vector2.Distance(transform.position, targetPos);

        if (distance < 0.01f)
        {
            rb.velocity = Vector2.zero;
            transform.position = targetPos;
        }
        else
        {
            Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
            rb.velocity = direction * slideSpeed;
        }
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