using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    private static PlayerInputReader _instance;
    private PlayerInputActions controls;
    private Camera mainCamera;

    // ---------- 单例 ----------
    public static PlayerInputReader Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PlayerInputReader");
                _instance = go.AddComponent<PlayerInputReader>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    // ---------- 轮询值 ----------
    public Vector2 MoveValue { get; private set; }
    public Vector2 ClimbFlyValue { get; private set; }
    public float ScrollValue { get; private set; }

    // ---------- 鼠标位置 ----------
    public Vector2 MouseScreenPosition { get; private set; }
    public Vector3 MouseWorldPosition { get; private set; }

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
    public event System.Action OnUICancel;  // UI Map 的 Cancel

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
        // 订阅 Slime Map 的事件（使用命名方法）
        controls.Slime.Interact.performed += OnInteractHandler;
        controls.Slime.EatSpit.performed += OnEatSpitHandler;
        controls.Slime.Ability1.performed += OnAbility1Handler;
        controls.Slime.Ability2.performed += OnAbility2Handler;
        controls.Slime.AnimalWheel.performed += OnAnimalWheelHandler;
        controls.Slime.Menu.performed += OnMenuHandler;

        // 订阅 UI Map 的 Cancel 事件
        controls.UI.Cancel.performed += OnUICancelHandler;
    }

    private void OnDisable()
    {
        // 取消所有事件订阅（防止内存泄漏）
        controls.Slime.Interact.performed -= OnInteractHandler;
        controls.Slime.EatSpit.performed -= OnEatSpitHandler;
        controls.Slime.Ability1.performed -= OnAbility1Handler;
        controls.Slime.Ability2.performed -= OnAbility2Handler;
        controls.Slime.AnimalWheel.performed -= OnAnimalWheelHandler;
        controls.Slime.Menu.performed -= OnMenuHandler;
        controls.UI.Cancel.performed -= OnUICancelHandler;

        controls?.Slime.Disable();
        controls?.UI.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    // ---------- Update 轮询 ----------
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
            worldPos.z = 0f;
            MouseWorldPosition = worldPos;
        }
        else
        {
            MouseWorldPosition = Vector3.zero;
        }
    }

    // ---------- 事件处理方法（命名方法，用于正确订阅/取消） ----------
    private void OnInteractHandler(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
    private void OnEatSpitHandler(InputAction.CallbackContext ctx) => OnEatSpit?.Invoke();
    private void OnAbility1Handler(InputAction.CallbackContext ctx) => OnAbility1?.Invoke();
    private void OnAbility2Handler(InputAction.CallbackContext ctx) => OnAbility2?.Invoke();
    private void OnAnimalWheelHandler(InputAction.CallbackContext ctx) => OnAnimalWheel?.Invoke();
    private void OnMenuHandler(InputAction.CallbackContext ctx) => OnMenu?.Invoke();
    private void OnUICancelHandler(InputAction.CallbackContext ctx) => OnUICancel?.Invoke();

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