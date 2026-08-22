using UnityEngine;

public interface IHoldable : IDevourable
{
    void OnEquip(PlayerController playerController);
    void OnUnequip(PlayerController playerController);
    void PlaceInWorld(Vector3 position);
    bool CanVoluntarySpit { get; }
}
