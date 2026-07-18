using UnityEngine;

public class InputPollingTester : MonoBehaviour
{
    private void Update()
    {
        Vector2 move = PlayerInputReader.Instance.MoveValue;
        Vector2 climb = PlayerInputReader.Instance.ClimbFlyValue;

        if (move != Vector2.zero)
            Debug.Log($"【轮询】Move: ({move.x:F1}, {move.y:F1})");
        if (climb != Vector2.zero)
            Debug.Log($"【轮询】ClimbFly: ({climb.x:F1}, {climb.y:F1})");
    }
}