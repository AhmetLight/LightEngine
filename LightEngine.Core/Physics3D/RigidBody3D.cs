using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics3D;

/// <summary>
/// 3D fizik gövdesinin hareket tipi.
/// </summary>
public enum BodyType3D
{
    Dynamic,
    Kinematic,
    Static
}

/// <summary>
/// 3D fizik çarpışma şekli.
/// </summary>
public enum ColliderShape3D
{
    Box,
    Sphere,
    Capsule
}

/// <summary>
/// Bir Entity'ye BepuPhysics v2 3D katı cisim fiziği (yerçekimi, çarpışma, impuls, hız) kazandıran bileşen.
/// </summary>
public class RigidBody3D : Component
{
    public BodyType3D BodyType { get; set; } = BodyType3D.Dynamic;
    public ColliderShape3D Shape { get; set; } = ColliderShape3D.Box;
    public float Mass { get; set; } = 1.0f;
    public Vector3 BoxSize { get; set; } = Vector3.One;
    public float Radius { get; set; } = 0.5f;
    public float CapsuleLength { get; set; } = 1.0f;
    public bool LockRotation { get; set; } = false;

    /// <summary>
    /// BepuPhysics iç gövde tanıtıcısı (dinamik/kinematik için).
    /// </summary>
    internal int BodyHandleValue { get; set; } = -1;

    /// <summary>
    /// BepuPhysics statik cisim tanıtıcısı (statik zemin/duvarlar için).
    /// </summary>
    internal int StaticHandleValue { get; set; } = -1;

    internal bool IsRegistered { get; set; }

    private Vector3 _pendingImpulse;
    private Vector3? _pendingVelocity;

    public Vector3 LinearVelocity { get; internal set; }
    public Vector3 AngularVelocity { get; internal set; }

    public RigidBody3D() { }

    public RigidBody3D(BodyType3D bodyType, ColliderShape3D shape = ColliderShape3D.Box, float mass = 1f)
    {
        BodyType = bodyType;
        Shape = shape;
        Mass = mass;
    }

    public override void Initialize()
    {
        base.Initialize();
        if (Entity != null && !Entity.HasComponent<Transform3D>())
        {
            Entity.AddComponent(new Transform3D());
        }

        if (Engine.IsInitialized && Engine.Services.TryGet<IPhysics3DService>(out var phys) && phys != null)
        {
            phys.RegisterBody(this);
        }
    }

    /// <summary>
    /// Cisme anlık itki (Impulse) uygular (zıplama, patlama, darbe vb.).
    /// </summary>
    public void ApplyImpulse(Vector3 impulse)
    {
        _pendingImpulse += impulse;
    }

    /// <summary>
    /// Cismin doğrusal hızını (LinearVelocity) doğrudan ayarlar.
    /// </summary>
    public void SetLinearVelocity(Vector3 velocity)
    {
        _pendingVelocity = velocity;
        LinearVelocity = velocity;
    }

    internal Vector3 ConsumePendingImpulse()
    {
        var imp = _pendingImpulse;
        _pendingImpulse = Vector3.Zero;
        return imp;
    }

    internal Vector3? ConsumePendingVelocity()
    {
        var vel = _pendingVelocity;
        _pendingVelocity = null;
        return vel;
    }

    public override void Destroy()
    {
        if (Engine.IsInitialized && Engine.Services.TryGet<IPhysics3DService>(out var phys) && phys != null)
        {
            phys.UnregisterBody(this);
        }
        base.Destroy();
    }
}
