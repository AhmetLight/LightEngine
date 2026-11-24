using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// 2D çarpışma geometrilerini (AABB, Daire) tanımlayan temel çarpıştırıcı bileşeni.
/// </summary>
public abstract class Collider2D : Component
{
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public bool IsTrigger { get; set; } = false;
    public bool IsStatic { get; set; } = false;
    public CollisionLayer Layer { get; set; } = CollisionLayer.Default;

    public event Action<Collider2D>? OnTriggerEnter;
    public event Action<Collider2D>? OnTriggerStay;
    public event Action<Collider2D>? OnTriggerExit;

    public event Action<Collision2D>? OnCollisionEnter;
    public event Action<Collision2D>? OnCollisionStay;
    public event Action<Collision2D>? OnCollisionExit;

    public override void Initialize()
    {
        base.Initialize();
        if (Engine.IsInitialized && Engine.Services.TryGet<IPhysics2DService>(out var physics) && physics != null)
        {
            physics.RegisterCollider(this);
        }
    }

    public override void Destroy()
    {
        if (Engine.IsInitialized && Engine.Services.TryGet<IPhysics2DService>(out var physics) && physics != null)
        {
            physics.UnregisterCollider(this);
        }
        base.Destroy();
    }

    /// <summary>
    /// Çarpıştırıcının dünya eksenindeki sınırlayıcı kutusunu (AABB) hesaplar.
    /// </summary>
    public abstract Rectangle GetAABB();

    internal void NotifyTriggerEnter(Collider2D other) => OnTriggerEnter?.Invoke(other);
    internal void NotifyTriggerStay(Collider2D other) => OnTriggerStay?.Invoke(other);
    internal void NotifyTriggerExit(Collider2D other) => OnTriggerExit?.Invoke(other);

    internal void NotifyCollisionEnter(Collision2D collision) => OnCollisionEnter?.Invoke(collision);
    internal void NotifyCollisionStay(Collision2D collision) => OnCollisionStay?.Invoke(collision);
    internal void NotifyCollisionExit(Collision2D collision) => OnCollisionExit?.Invoke(collision);
}
