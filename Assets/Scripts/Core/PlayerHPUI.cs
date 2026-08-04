using TMPro;
using UnityEngine;

public class PlayerHPUI : MonoBehaviour
{
    private TextMeshProUGUI heartsText;

    private void Awake()
    {
        CreateHeartsDisplay();
    }

    private void OnEnable()
    {
        MockEventCenter.OnPlayerHurt += UpdateHearts;
        MockEventCenter.OnPlayerHeal += UpdateHearts;
        MockEventCenter.OnPlayerDeath += OnDeath;
    }

    private void OnDisable()
    {
        MockEventCenter.OnPlayerHurt -= UpdateHearts;
        MockEventCenter.OnPlayerHeal -= UpdateHearts;
        MockEventCenter.OnPlayerDeath -= OnDeath;
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
        rectTransform.anchoredPosition = new Vector2(80, -30);
        rectTransform.sizeDelta = new Vector2(300, 60);

        var textGo = new GameObject("HeartsText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        heartsText = textGo.GetComponent<TextMeshProUGUI>();
        heartsText.fontSize = 48;
        heartsText.alignment = TextAlignmentOptions.Left;
        heartsText.color = new Color(1f, 0.2f, 0.2f, 1f);

        var playerHP = GetComponent<PlayerHP>();
        if (playerHP != null)
            UpdateHearts(playerHP.CurrentHP, playerHP.MaxHP);
    }

    private void UpdateHearts(int current, int max)
    {
        if (heartsText == null) return;
        var filled = new string('♥', current);
        var empty = new string('♡', max - current);
        heartsText.text = filled + empty;
    }

    private void OnDeath()
    {
        if (heartsText == null) return;
        var empty = new string('♡', GetComponent<PlayerHP>().MaxHP);
        heartsText.text = empty;
    }
}
