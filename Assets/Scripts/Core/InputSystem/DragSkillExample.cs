using UnityEngine;
using UnityEngine.InputSystem;

public class DragSkillExample : MonoBehaviour
{
    private bool isCharging = false;

    private void OnEnable()
    {
        var ability1 = PlayerInputReader.Instance.Ability1Action;
        ability1.started += OnSkillStart;
        ability1.performed += OnSkillHold;
        ability1.canceled += OnSkillRelease;
    }

    private void OnDisable()
    {
        var ability1 = PlayerInputReader.Instance.Ability1Action;
        ability1.started -= OnSkillStart;
        ability1.performed -= OnSkillHold;
        ability1.canceled -= OnSkillRelease;
    }

    private void OnSkillStart(InputAction.CallbackContext ctx)
    {
        isCharging = true;
        Debug.Log("【技能】开始蓄力/瞄准，鼠标位置: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里可以播放蓄力动画或显示瞄准线
    }

    private void OnSkillHold(InputAction.CallbackContext ctx)
    {
        if (!isCharging) return;
        Debug.Log("【技能】拖动瞄准中... 目标点: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里每帧更新瞄准线的终点
    }

    private void OnSkillRelease(InputAction.CallbackContext ctx)
    {
        if (!isCharging) return;
        isCharging = false;
        Debug.Log("【技能】释放！最终位置: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里生成技能特效
    }
}