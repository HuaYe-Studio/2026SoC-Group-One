using System.Collections;
using UnityEngine;

public class PlayerHP : MonoBehaviour
{
    [SerializeField] private int maxHP = 3;
    [SerializeField] private float invincibilityDuration = 1.5f;
    [SerializeField] private float flashInterval = 0.1f;

    private int currentHP;
    private float invincibilityEndTime;
    private Coroutine flashCoroutine;
    private bool isDead;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsInvincible => Time.time < invincibilityEndTime;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHP = maxHP;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
            TakeDamage(1);

        if (Input.GetKeyDown(KeyCode.L))
            Heal(1);
    }

    public void TakeDamage(int amount)
    {
        if (isDead || IsInvincible || amount <= 0) return;

        currentHP -= amount;
        MockEventCenter.TriggerPlayerHurt(currentHP, maxHP);

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
            return;
        }

        invincibilityEndTime = Time.time + invincibilityDuration;
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        MockEventCenter.TriggerPlayerHeal(currentHP, maxHP);
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player died!");
        MockEventCenter.TriggerPlayerDeath();

        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;
    }

    private IEnumerator FlashRoutine()
    {
        var playerController = GetComponent<PlayerController>();
        SpriteRenderer[] renderers = playerController != null && playerController.ActiveForm != null
            ? playerController.ActiveForm.GetComponentsInChildren<SpriteRenderer>()
            : GetComponentsInChildren<SpriteRenderer>();

        while (Time.time < invincibilityEndTime)
        {
            foreach (var sr in renderers)
                sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(flashInterval);
        }

        foreach (var sr in renderers)
            sr.enabled = true;
    }
}
