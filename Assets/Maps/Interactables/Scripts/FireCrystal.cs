using UnityEngine;

public class FireCrystal : DevourableObject
{
    protected override void ExecuteDevourOutcomeOverride(PlayerController pc)
    {
        base.ExecuteDevourOutcomeOverride(pc);
        pc.GetComponent<ElementAbilityManager>()?.UnlockElement(ElementType.Fire);
    }
}
