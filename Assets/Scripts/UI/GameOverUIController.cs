using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class GameOverUIController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _gameOverPanel;

    void OnEnable() 
    {
        MockEventCenter.OnPlayerDeath += ShowGameOverPanel;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDisable()
    {
        MockEventCenter.OnPlayerDeath -= ShowGameOverPanel;
    }

    private void ShowGameOverPanel()
    {
        _gameOverPanel.DOFade(1f,0.2f).SetUpdate(true);
        _gameOverPanel.blocksRaycasts = true;
        _gameOverPanel.interactable = true;
    }

    private void HideGameOverPanel()
    {
        
    }

    public void BackToContinue()
    {
        MockEventCenter.TriggerPlayerRespawn();
    }

    public void BackToMainMenu()
    {
        
    }
}
