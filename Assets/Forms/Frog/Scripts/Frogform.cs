using UnityEngine;

public class FrogForm : BaseForm
{

    public override void Initialize(PlayerController ctrl)
    {
        base.Initialize(ctrl);
        moveSpeed = 3f;
        jumpForce = 14f;
        gravityScale = 0.7f;
        fallGravityMultiplier = 1.2f;
        jumpCutMultiplier = 0.4f;
    }
}