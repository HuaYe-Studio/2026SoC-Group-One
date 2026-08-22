using UnityEngine;

// 建议将 Animator 参数设为 Integer 或 Trigger
// 这里同步动画：当状态切换时，驱动 Animator 状态
public partial class BubbleFishForm 
{
    private void UpdateAnimator()
    {
        if (animator == null) return;
        
        // 映射 BubbleState 到 Animator 的状态
        animator.SetInteger("BubbleState", (int)bubbleState);
        animator.SetFloat("ExpansionProgress", currentExpansion);
    }
}
