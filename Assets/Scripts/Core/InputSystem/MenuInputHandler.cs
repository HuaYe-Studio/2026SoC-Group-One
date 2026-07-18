using UnityEngine;

public class MenuInputHandler : MonoBehaviour
{
    private bool isMenuOpen = false;

    private void OnEnable()
    {
        PlayerInputReader.Instance.OnMenu += ToggleMenu;
    }

    private void OnDisable()
    {
        if (PlayerInputReader.Instance != null)
            PlayerInputReader.Instance.OnMenu -= ToggleMenu;
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (isMenuOpen)
        {
            PlayerInputReader.Instance.SwitchToUI();
            Debug.Log("【菜单】打开菜单，切换到 UI Map");
            // 这里打开你的菜单 Canvas
        }
        else
        {
            PlayerInputReader.Instance.SwitchToGameplay();
            Debug.Log("【菜单】关闭菜单，切换到 Gameplay Map");
            // 这里关闭你的菜单 Canvas
        }
    }
}
