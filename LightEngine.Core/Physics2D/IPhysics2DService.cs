using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// 2D fizik ve çarpışma işlemlerini yöneten servis arayüzü.
/// </summary>
public interface IPhysics2DService
{
    CollisionMatrix Matrix { get; }
    void RegisterCollider(Collider2D collider);
    void UnregisterCollider(Collider2D collider);
    void FixedUpdate(float fixedDeltaTime);
    bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out RaycastHit2D hit, CollisionLayer layerMask = CollisionLayer.All);
}

public struct RaycastHit2D
{
    public Collider2D? Collider;
    public Vector2 Point;
    public Vector2 Normal;
    public float Distance;
}
