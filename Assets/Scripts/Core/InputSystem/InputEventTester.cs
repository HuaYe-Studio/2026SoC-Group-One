using UnityEngine;

public class InputEventTester : MonoBehaviour
{
    private void OnEnable()
    {
        var reader = PlayerInputReader.Instance;
        reader.OnInteract += () => Debug.Log("【事件】按下 E (Interact)");
        reader.OnEatSpit += () => Debug.Log("【事件】按下 空格 (EatSpit)");
        reader.OnAbility1 += () => Debug.Log("【事件】按下 鼠标左键 (Ability1)");
        reader.OnAbility2 += () => Debug.Log("【事件】按下 鼠标右键 (Ability2)");
        reader.OnAnimalWheel += () => Debug.Log("【事件】按下 Tab (AnimalWheel)");
        reader.OnMenu += () => Debug.Log("【事件】按下 Esc (Menu)");
    }

    private void OnDisable()
    {
        // 由于是匿名委托，如果反复 OnEnable/OnDisable 可能重复订阅，正式测试建议用具名方法。
        // 这里简化处理，仅作一次性测试用。
    }
}