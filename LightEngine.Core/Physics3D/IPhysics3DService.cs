using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics3D;

/// <summary>
/// 3D ışın (Raycast) çarpışma sonucunu tutar.
/// </summary>
public readonly struct RaycastHit3D
{
    public IEntity? Entity { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public float Distance { get; }

    public RaycastHit3D(IEntity? entity, Vector3 point, Vector3 normal, float distance)
    {
        Entity = entity;
        Point = point;
        Normal = normal;
        Distance = distance;
    }
}

/// <summary>
/// BepuPhysics v2 tabanlı 3D fizik simülasyonu, çarpışma ve ışın testi (Raycast) servis arayüzü.
/// </summary>
public interface IPhysics3DService : IDisposable
{
    Vector3 Gravity { get; set; }
    int DynamicBodyCount { get; }
    int StaticBodyCount { get; }

    void RegisterBody(RigidBody3D body);
    void UnregisterBody(RigidBody3D body);
    void FixedUpdate(float fixedDeltaTime);
    bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit3D hit, IEntity? ignoreEntity = null);
    void Clear();
}
