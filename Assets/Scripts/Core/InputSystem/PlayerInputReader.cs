using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    private PlayerInputActions controls;
    private Camera mainCamera;

    // ---------- 单例 ----------
    private static PlayerInputReader _instance;
    public static PlayerInputReader Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("PlayerInputReader 尚未在场景中实例化！");
            }
            return _instance;
        }
    }

    // ---------- 轮询值 ----------
    public Vector2 MoveValue { get; private set; }
    public Vector2 ClimbFlyValue { get; private set; }
    public float ScrollValue { get; private set; }

    // ---------- 鼠标位置 ----------
    public Vector2 MouseScreenPosition { get; private set; }
    public Vector3 MouseWorldPosition { get; private set; } // Vector3 但 Z 始终为 0

    // ---------- 公开原始 Action ----------
    public InputAction Ability1Action => controls.Slime.Ability1;
    public InputAction Ability2Action => controls.Slime.Ability2;
    public InputAction AnimalWheelAction => controls.Slime.AnimalWheel;

    // ---------- 事件 ----------
    public event System.Action OnInteract;
    public event System.Action OnEatSpit;
    public event System.Action OnAbility1;
    public event System.Action OnAbility2;
    public event System.Action OnAnimalWheel;
    public event System.Action OnMenu;

    // ---------- 初始化 ----------
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        controls = new PlayerInputActions();
        mainCamera = Camera.main;

        controls.Slime.Enable();
        controls.UI.Disable();
    }

    private void OnEnable()
    {
        controls.Slime.Interact.performed += ctx => OnInteract?.Invoke();
        controls.Slime.EatSpit.performed += ctx => OnEatSpit?.Invoke();
        controls.Slime.Ability1.performed += ctx => OnAbility1?.Invoke();
        controls.Slime.Ability2.performed += ctx => OnAbility2?.Invoke();
        controls.Slime.AnimalWheel.performed += ctx => OnAnimalWheel?.Invoke();
        controls.Slime.Menu.performed += ctx => OnMenu?.Invoke();
    }

    private void Update()
    {
        MoveValue = controls.Slime.Move.ReadValue<Vector2>();
        ClimbFlyValue = controls.Slime.ClimbFly.ReadValue<Vector2>();
        ScrollValue = controls.Slime.Scroll.ReadValue<float>();
        MouseScreenPosition = Mouse.current.position.ReadValue();

        if (mainCamera != null)
        {
            Vector3 screenPos = new Vector3(MouseScreenPosition.x, MouseScreenPosition.y, 0f);
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;              // 2D 游戏强制 Z=0
            MouseWorldPosition = worldPos; // 整体赋值，不修改属性成员
        }
        else
        {
            MouseWorldPosition = Vector3.zero;
        }
    }

    private void OnDisable()
    {
        controls?.Slime.Disable();
        controls?.UI.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    // ---------- 公开切换接口 ----------
    public void SwitchToGameplay()
    {
        controls.UI.Disable();
        controls.Slime.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SwitchToUI()
    {
        controls.Slime.Disable();
        controls.UI.Enable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}