using UnityEngine;

public class DragSkillExample : MonoBehaviour
{
    private bool isCharging = false;

    private void OnEnable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAbility1Started += OnSkillStart;
            PlayerInputReader.Instance.OnAbility1Performed += OnSkillHold;
            PlayerInputReader.Instance.OnAbility1Canceled += OnSkillRelease;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
        {
            PlayerInputReader.Instance.OnAbility1Started -= OnSkillStart;
            PlayerInputReader.Instance.OnAbility1Performed -= OnSkillHold;
            PlayerInputReader.Instance.OnAbility1Canceled -= OnSkillRelease;
        }
    }

    private void OnSkillStart()
    {
        isCharging = true;
        Debug.Log("【技能】开始蓄力/瞄准，鼠标位置: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里可以播放蓄力动画或显示瞄准线
    }

    private void OnSkillHold()
    {
        if (!isCharging) return;
        Debug.Log("【技能】拖动瞄准中... 目标点: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里每帧更新瞄准线的终点
    }

    private void OnSkillRelease()
    {
        if (!isCharging) return;
        isCharging = false;
        Debug.Log("【技能】释放！最终位置: " + PlayerInputReader.Instance.MouseWorldPosition);
        // 这里生成技能特效
    }
}