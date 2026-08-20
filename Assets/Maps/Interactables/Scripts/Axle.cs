using UnityEngine;

public class Axle : MonoBehaviour
{
    [Header("旋转速度")]
    [Tooltip("每秒旋转的角度，正值为逆时针")]
    public float rotationSpeed = 30f; // 可调

    void FixedUpdate() // 使用FixedUpdate处理物理相关
    {
        // 绕Z轴旋转，实现2D平面内的旋转
        transform.Rotate(0f, 0f, rotationSpeed * Time.fixedDeltaTime);
    }
}