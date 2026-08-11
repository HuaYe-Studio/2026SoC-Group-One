using System;
using AbilitySystem;
using UnityEngine;

[Serializable]
public struct HoldableModifier
{
    [Header("Movement")]
    public float moveSpeedMultiplier;
    public float gravityScaleMultiplier;
    public float fallGravityMultiplier;
    public float massMultiplier;

    [Header("Wall Climb (SlimeForm only)")]
    public float climbSpeedMultiplier;
    public float slideDownSpeedMultiplier;
    public float climbStaminaCostMultiplier;
    public float clingStaminaCostMultiplier;

    [Header("Appearance")]
    public Color tintColor;
    public Sprite overlaySprite;

    [Header("Granted Ability")]
    public AbilityInputBinding grantedAbility;

    public readonly bool HasAnyEffect =>
        !Mathf.Approximately(moveSpeedMultiplier, 1f) ||
        !Mathf.Approximately(gravityScaleMultiplier, 1f) ||
        !Mathf.Approximately(fallGravityMultiplier, 1f) ||
        !Mathf.Approximately(massMultiplier, 1f) ||
        !Mathf.Approximately(climbSpeedMultiplier, 1f) ||
        !Mathf.Approximately(slideDownSpeedMultiplier, 1f) ||
        !Mathf.Approximately(climbStaminaCostMultiplier, 1f) ||
        !Mathf.Approximately(clingStaminaCostMultiplier, 1f) ||
        tintColor.a > 0f ||
        overlaySprite != null ||
        grantedAbility != null;
}
