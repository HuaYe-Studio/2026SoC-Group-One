using TMPro;
using UnityEngine;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private float offsetX = 80f;
    [SerializeField] private float offsetY = -30f;
    [Header("Style")]
    [SerializeField] private float fontSize = 48f;
    [SerializeField] private Color heartColor = new(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color staminaColor = new(0.2f, 0.6f, 1f, 1f);

    private TextMeshProUGUI heartsText;
    private TextMeshProUGUI staminaText;

    private void Awake()
    {
        CreateHeartsDisplay();
        CreateStaminaDisplay();
    }

    private void OnEnable()
    {
        MockEventCenter.OnPlayerHurt += UpdateHearts;
        MockEventCenter.OnPlayerHeal += UpdateHearts;
        MockEventCenter.OnPlayerDeath += OnDeath;
        MockEventCenter.OnStaminaChanged += UpdateStamina;
    }

    private void OnDisable()
    {
        MockEventCenter.OnPlayerHurt -= UpdateHearts;
        MockEventCenter.OnPlayerHeal -= UpdateHearts;
        MockEventCenter.OnPlayerDeath -= OnDeath;
        MockEventCenter.OnStaminaChanged -= UpdateStamina;
    }

    private void CreateHeartsDisplay()
    {
        var canvasGo = new GameObject("HeartsCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var rectTransform = canvasGo.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(offsetX, offsetY);
        rectTransform.sizeDelta = new Vector2(300, 120);

        var textGo = new GameObject("HeartsText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        heartsText = textGo.GetComponent<TextMeshProUGUI>();
        heartsText.fontSize = fontSize;
        heartsText.alignment = TextAlignmentOptions.Left;
        heartsText.color = heartColor;
        heartsText.rectTransform.anchoredPosition = new Vector2(0, 0);
        heartsText.rectTransform.sizeDelta = new Vector2(300, 60);

        var playerHP = GetComponent<PlayerHP>();
        if (playerHP != null)
            UpdateHearts(playerHP.CurrentHP, playerHP.MaxHP);
    }

    private void CreateStaminaDisplay()
    {
        var textGo = new GameObject("StaminaText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(heartsText.transform.parent, false);
        staminaText = textGo.GetComponent<TextMeshProUGUI>();
        staminaText.fontSize = fontSize * 0.6f;
        staminaText.alignment = TextAlignmentOptions.Left;
        staminaText.color = staminaColor;
        staminaText.rectTransform.anchoredPosition = new Vector2(0, -50);
        staminaText.rectTransform.sizeDelta = new Vector2(300, 40);

        var playerStamina = GetComponent<PlayerStamina>();
        if (playerStamina != null)
            UpdateStamina(playerStamina.Current, playerStamina.Max);
    }

    private void UpdateHearts(int current, int max)
    {
        if (heartsText == null) return;
        var filled = new string('♥', current);
        var empty = new string('♡', max - current);
        heartsText.text = filled + empty;
    }

    private void UpdateStamina(float current, float max)
    {
        if (staminaText == null) return;
        int bars = Mathf.CeilToInt(max / 20f);
        int filled = Mathf.CeilToInt(current / 20f);
        var filledStr = new string('█', filled);
        var emptyStr = new string('░', bars - filled);
        staminaText.text = $"{filledStr}{emptyStr} {(int)current}/{max}";
    }

    private void OnDeath()
    {
        if (heartsText == null) return;
        var hp = GetComponent<PlayerHP>();
        if (hp != null)
        {
            var empty = new string('♡', hp.MaxHP);
            heartsText.text = empty;
        }
    }
}
