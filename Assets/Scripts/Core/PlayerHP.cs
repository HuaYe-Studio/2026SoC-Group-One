using System.Collections;
using UnityEngine;

public class PlayerHP : MonoBehaviour
{
    [SerializeField] private int maxHP = 3;
    [SerializeField] private float invincibilityDuration = 1.5f;
    [SerializeField] private float flashInterval = 0.1f;
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private float respawnInvincibility = 1f;
    [SerializeField] private bool _godMode = true; // 调试：玩家永久无敌，不扣血不死亡

    private int _currentHP;
    private float _invincibilityEndTime;
    private Coroutine _flashCoroutine;
    private Coroutine _respawnCoroutine;
    private bool _isDead;

    public int CurrentHP => _currentHP;
    public int MaxHP => maxHP;
    public bool IsInvincible => Time.time < _invincibilityEndTime;
    public bool IsDead => _isDead;

    public void SetInvincible(float duration)
    {
        _invincibilityEndTime = Time.time + duration;
    }

    private void Awake()
    {
        _currentHP = maxHP;
    }

    private void Start()
    {
        MockEventCenter.TriggerCheckPlayerHP(_currentHP, maxHP);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame)
                TakeDamage(1);
            if (UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame)
                Heal(1);
            if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                Respawn();
        }
#endif
    }

    public void TakeDamage(int amount)
    {
        if (_isDead || IsInvincible || amount <= 0) return;

        _currentHP -= amount;
        MockEventCenter.TriggerPlayerHurt(_currentHP, maxHP);

        if (_currentHP <= 0)
        {
            _currentHP = 0;
            Die();
            return;
        }

        _invincibilityEndTime = Time.time + invincibilityDuration;
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;
        _currentHP = Mathf.Min(_currentHP + amount, maxHP);
        MockEventCenter.TriggerPlayerHeal(_currentHP, maxHP);
    }

    public void Respawn()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        _isDead = false;
        _currentHP = maxHP;
        _invincibilityEndTime = Time.time + respawnInvincibility;
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        _flashCoroutine = StartCoroutine(FlashRoutine());
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.enabled = true;

        MoveToLastSavePoint();

        MockEventCenter.TriggerPlayerRespawn();
        MockEventCenter.TriggerCheckPlayerHP(_currentHP, maxHP);
    }

    private void MoveToLastSavePoint()
    {
        if (SaveManager.Instance == null) return;
        SaveData data = SaveManager.Instance.LoadRawData();
        if (data == null) return; // no save yet → respawn in place

        // 传送前抑制存档点自动保存，避免复活落地立即重写存档和截图
        SaveManager.Instance.SuppressSavePointForRespawn();

        transform.position = data.GetSavePointPosition();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void Die()
    {
        _isDead = true;
        Debug.Log("Player died!");
        MockEventCenter.TriggerPlayerDeath();

        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.enabled = false;

        if (_respawnCoroutine != null)
            StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        _respawnCoroutine = null;
        Respawn();
    }

    private IEnumerator FlashRoutine()
    {
        var playerController = GetComponent<PlayerController>();
        SpriteRenderer[] renderers = playerController != null && playerController.ActiveForm != null
            ? playerController.ActiveForm.GetComponentsInChildren<SpriteRenderer>()
            : GetComponentsInChildren<SpriteRenderer>();

        while (Time.time < _invincibilityEndTime)
        {
            foreach (var sr in renderers)
                sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(flashInterval);
        }

        foreach (var sr in renderers)
            sr.enabled = true;
    }
}
