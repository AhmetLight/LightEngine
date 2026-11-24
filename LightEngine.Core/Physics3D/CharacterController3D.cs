using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Physics3D;

/// <summary>
/// BepuPhysics v2 tabanlı, zemin algılama (ground raycast), yürüme, koşma ve zıplama
/// yeteneklerine sahip 3D Karakter Kontrolcüsü (Character Controller) bileşeni.
/// </summary>
public class CharacterController3D : Component
{
    private RigidBody3D? _rigidBody;
    private Transform3D? _transform3D;

    public float MoveSpeed { get; set; } = 6.5f;
    public float SprintMultiplier { get; set; } = 1.6f;
    public float JumpVelocity { get; set; } = 6.2f;
    public float GroundCheckDistance { get; set; } = 1.15f;
    public bool UseDefaultInput { get; set; } = true;

    public bool IsGrounded { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        if (Entity == null) return;

        _transform3D = Entity.GetComponent<Transform3D>();
        if (_transform3D == null)
        {
            _transform3D = new Transform3D();
            Entity.AddComponent(_transform3D);
        }

        _rigidBody = Entity.GetComponent<RigidBody3D>();
        if (_rigidBody == null)
        {
            _rigidBody = new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Capsule, mass: 70f)
            {
                Radius = 0.4f,
                CapsuleLength = 1.0f,
                LockRotation = true
            };
            Entity.AddComponent(_rigidBody);
        }
        else
        {
            _rigidBody.LockRotation = true;
        }
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        if (!IsEnabled || Entity == null || _transform3D == null || _rigidBody == null) return;

        // 1. Zemin kontrolü (aşağı yönlü BepuPhysics ışın testi)
        IsGrounded = CheckGrounded();

        if (!UseDefaultInput || !Engine.IsInitialized) return;
        if (!Engine.Services.TryGet<IInputService>(out var input) || input == null) return;

        // 2. WASD yön girdisi
        Vector3 wishDir = Vector3.Zero;
        if (input.IsKeyDown(Keys.W)) wishDir += Vector3.Forward;
        if (input.IsKeyDown(Keys.S)) wishDir += Vector3.Backward;
        if (input.IsKeyDown(Keys.A)) wishDir += Vector3.Left;
        if (input.IsKeyDown(Keys.D)) wishDir += Vector3.Right;

        bool sprint = input.IsKeyDown(Keys.LeftShift);
        Move(wishDir, sprint);

        // 3. Space ile zıplama
        if (input.IsKeyPressed(Keys.Space))
        {
            Jump();
        }
    }

    /// <summary>
    /// Karakteri belirtilen dünya yönünde hareket ettirir (dikey hız/yerçekimi korunur).
    /// </summary>
    public void Move(Vector3 worldDirection, bool sprint = false)
    {
        if (_rigidBody == null) return;

        Vector3 horizontal = new(worldDirection.X, 0f, worldDirection.Z);
        if (horizontal.LengthSquared() > 0.001f)
        {
            horizontal.Normalize();
        }

        float speed = MoveSpeed * (sprint ? SprintMultiplier : 1f);
        Vector3 curVel = _rigidBody.LinearVelocity;
        _rigidBody.SetLinearVelocity(new Vector3(horizontal.X * speed, curVel.Y, horizontal.Z * speed));
    }

    /// <summary>
    /// Karakter yerdeyse (IsGrounded) yukarı doğru zıplama hızı uygular.
    /// </summary>
    public bool Jump()
    {
        if (_rigidBody == null || !IsGrounded) return false;
        Vector3 curVel = _rigidBody.LinearVelocity;
        _rigidBody.SetLinearVelocity(new Vector3(curVel.X, JumpVelocity, curVel.Z));
        IsGrounded = false;
        return true;
    }

    private bool CheckGrounded()
    {
        if (_transform3D == null || !Engine.IsInitialized) return false;
        if (!Engine.Services.TryGet<IPhysics3DService>(out var phys) || phys == null) return false;

        return phys.Raycast(_transform3D.WorldPosition, Vector3.Down, GroundCheckDistance, out _, ignoreEntity: Entity);
    }
}
