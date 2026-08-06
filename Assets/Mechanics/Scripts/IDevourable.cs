using UnityEngine;

public interface IDevourable
{
    Transform Transform { get; }
    SpriteRenderer SpriteRenderer { get; }
    bool IsTargeted { get; set; }

    bool CanBeDevoured(PlayerController playerController);

    void OnBeingDevoured();

    void ExecuteDevourOutcome(PlayerController playerController);

    void OnBeingSpitOut(Vector2 direction);

    float Priority { get; }

    bool DestroyAfterDevour { get; }
}
