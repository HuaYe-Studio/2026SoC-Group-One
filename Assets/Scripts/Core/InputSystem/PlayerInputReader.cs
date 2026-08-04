using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    private static PlayerInputReader _instance;
    private PlayerInputActions controls;
    private Camera mainCamera;

    // ---------- 单例 ----------
    /// <summary>
    /// 获取 PlayerInputReader 实例。必须在 Scene_Persistent 中显式放置该组件，
    /// 若为 null 说明场景未加载或组件缺失。
    /// </summary>
    public static PlayerInputReader Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("PlayerInputReader 未初始化，请确保 Scene_Persistent 中包含该组件");
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

    // ---------- 事件：简单按钮触发（performed）----------
    public event System.Action OnInteract;
    public event System.Action OnInput_Space;
    public event System.Action OnAbility1;
    public event System.Action OnAbility2;
    public event System.Action OnAnimalWheel;
    public event System.Action OnMenu;
    public event System.Action OnUICancel;

    // ---------- 事件：区分按下 / 按住 / 松开的细粒度事件 ----------
    // Ability1（鼠标左键 / Gamepad South）
    public event System.Action OnAbility1Started;
    public event System.Action OnAbility1Performed;
    public event System.Action OnAbility1Canceled;

    // Ability2（鼠标右键 / Gamepad D-pad Left）
    public event System.Action OnAbility2Started;
    public event System.Action OnAbility2Performed;
    public event System.Action OnAbility2Canceled;

    // Input_Space（空格 / Gamepad East）
    public event System.Action OnInput_SpaceStarted;
    public event System.Action OnInput_SpaceCanceled;

    // AnimalWheel（Tab / Gamepad D-pad Right）
    public event System.Action OnAnimalWheelStarted;
    public event System.Action OnAnimalWheelCanceled;

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
        // Slime Map — 简单按钮事件
        controls.Slime.Interact.performed += OnInteractHandler;
        controls.Slime.Input_Space.performed += OnInput_SpaceHandler;
        controls.Slime.Input_Space.started += OnInput_SpaceStartedHandler;
        controls.Slime.Input_Space.canceled += OnInput_SpaceCanceledHandler;
        controls.Slime.Menu.performed += OnMenuHandler;

        // Slime Map — Ability1 细粒度
        controls.Slime.Ability1.started += OnAbility1StartedHandler;
        controls.Slime.Ability1.performed += OnAbility1PerformedHandler;
        controls.Slime.Ability1.canceled += OnAbility1CanceledHandler;

        // Slime Map — Ability2 细粒度
        controls.Slime.Ability2.started += OnAbility2StartedHandler;
        controls.Slime.Ability2.performed += OnAbility2PerformedHandler;
        controls.Slime.Ability2.canceled += OnAbility2CanceledHandler;

        // Slime Map — AnimalWheel 细粒度
        controls.Slime.AnimalWheel.started += OnAnimalWheelStartedHandler;
        controls.Slime.AnimalWheel.performed += OnAnimalWheelPerformedHandler;
        controls.Slime.AnimalWheel.canceled += OnAnimalWheelCanceledHandler;

        // UI Map
        controls.UI.Cancel.performed += OnUICancelHandler;
    }

    private void OnDisable()
    {
        if (controls == null) return;
        
        controls.Slime.Interact.performed -= OnInteractHandler;
        controls.Slime.Input_Space.performed -= OnInput_SpaceHandler;
        controls.Slime.Input_Space.started -= OnInput_SpaceStartedHandler;
        controls.Slime.Input_Space.canceled -= OnInput_SpaceCanceledHandler;
        controls.Slime.Menu.performed -= OnMenuHandler;

        controls.Slime.Ability1.started -= OnAbility1StartedHandler;
        controls.Slime.Ability1.performed -= OnAbility1PerformedHandler;
        controls.Slime.Ability1.canceled -= OnAbility1CanceledHandler;

        controls.Slime.Ability2.started -= OnAbility2StartedHandler;
        controls.Slime.Ability2.performed -= OnAbility2PerformedHandler;
        controls.Slime.Ability2.canceled -= OnAbility2CanceledHandler;

        controls.Slime.AnimalWheel.started -= OnAnimalWheelStartedHandler;
        controls.Slime.AnimalWheel.performed -= OnAnimalWheelPerformedHandler;
        controls.Slime.AnimalWheel.canceled -= OnAnimalWheelCanceledHandler;

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

    // ---------- 事件处理方法 ----------
    private void OnInteractHandler(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
    private void OnInput_SpaceHandler(InputAction.CallbackContext ctx) => OnInput_Space?.Invoke();
    private void OnInput_SpaceStartedHandler(InputAction.CallbackContext ctx) => OnInput_SpaceStarted?.Invoke();
    private void OnInput_SpaceCanceledHandler(InputAction.CallbackContext ctx) => OnInput_SpaceCanceled?.Invoke();
    private void OnMenuHandler(InputAction.CallbackContext ctx) => OnMenu?.Invoke();
    public void OnMenuTrigger()=>OnMenu?.Invoke();
    private void OnUICancelHandler(InputAction.CallbackContext ctx) => OnUICancel?.Invoke();
    public void OnUICancelTrigger()=>OnUICancel?.Invoke();

    private void OnAbility1StartedHandler(InputAction.CallbackContext ctx) => OnAbility1Started?.Invoke();
    private void OnAbility1PerformedHandler(InputAction.CallbackContext ctx)
    {
        OnAbility1?.Invoke();
        OnAbility1Performed?.Invoke();
    }
    private void OnAbility1CanceledHandler(InputAction.CallbackContext ctx) => OnAbility1Canceled?.Invoke();

    private void OnAbility2StartedHandler(InputAction.CallbackContext ctx) => OnAbility2Started?.Invoke();
    private void OnAbility2PerformedHandler(InputAction.CallbackContext ctx)
    {
        OnAbility2?.Invoke();
        OnAbility2Performed?.Invoke();
    }
    private void OnAbility2CanceledHandler(InputAction.CallbackContext ctx) => OnAbility2Canceled?.Invoke();

    private void OnAnimalWheelStartedHandler(InputAction.CallbackContext ctx) => OnAnimalWheelStarted?.Invoke();
    private void OnAnimalWheelPerformedHandler(InputAction.CallbackContext ctx) => OnAnimalWheel?.Invoke();
    private void OnAnimalWheelCanceledHandler(InputAction.CallbackContext ctx) => OnAnimalWheelCanceled?.Invoke();

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
