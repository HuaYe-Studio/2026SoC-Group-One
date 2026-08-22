using UnityEngine;

public class MenuInputHandler : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel; // 在 Inspector 中拖入你的菜单面板
    private bool isMenuOpen = false;

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnMenu += ToggleMenu;       // 游戏模式 Esc
            PlayerInputReader.Instance.OnUICancel += OnUICancel;   // UI 模式 Esc
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnMenu -= ToggleMenu;
            PlayerInputReader.Instance.OnUICancel -= OnUICancel;
        }
    }

    private void OnUICancel()
    {
        if (isMenuOpen)
            ToggleMenu();
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (isMenuOpen)
        {
            PlayerInputReader.Instance.SwitchToUI();
            if (menuPanel != null) menuPanel.SetActive(true);
            Debug.Log("【菜单】打开菜单，切换到 UI Map");
        }
        else
        {
            PlayerInputReader.Instance.SwitchToGameplay();
            if (menuPanel != null) menuPanel.SetActive(false);
            Debug.Log("【菜单】关闭菜单，切换到 Gameplay Map");
        }
    }
}