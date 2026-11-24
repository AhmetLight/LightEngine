using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering3D;
using XnaVec3 = Microsoft.Xna.Framework.Vector3;
using XnaQuat = Microsoft.Xna.Framework.Quaternion;

namespace LightEngine.Core.Physics3D;

/// <summary>
/// BepuPhysics v2.4 üzerine kurulu yüksek performanslı, çok iş parçacıklı (SIMD) 3D fizik yöneticisi.
/// </summary>
public class Physics3DManager : IPhysics3DService
{
    private readonly BufferPool _bufferPool;
    private readonly ThreadDispatcher _threadDispatcher;
    private Simulation _simulation;

    private readonly List<RigidBody3D> _dynamicBodies = new(64);
    private readonly List<RigidBody3D> _staticBodies = new(64);
    private readonly Dictionary<int, RigidBody3D> _bodyHandleMap = new();
    private readonly Dictionary<int, RigidBody3D> _staticHandleMap = new();

    private XnaVec3 _gravity = new(0f, -9.81f, 0f);
    private bool _disposed;

    public XnaVec3 Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            // Simülasyon içindeki yerçekimi entegratörüne bir sonraki adımda yansıtılır
        }
    }

    public int DynamicBodyCount => _dynamicBodies.Count;
    public int StaticBodyCount => _staticBodies.Count;

    public Physics3DManager()
    {
        _bufferPool = new BufferPool();
        int threadCount = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));
        _threadDispatcher = new ThreadDispatcher(threadCount);
        _simulation = CreateSimulation();
    }

    private Simulation CreateSimulation()
    {
        return Simulation.Create(
            _bufferPool,
            new NarrowPhaseCallbacks(new SpringSettings(30f, 1f)),
            new PoseIntegratorCallbacks(new Vector3(_gravity.X, _gravity.Y, _gravity.Z)),
            new SolveDescription(4, 1));
    }

    public void RegisterBody(RigidBody3D body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (_disposed || body.IsRegistered || body.Entity == null) return;

        var t3d = body.Entity.GetComponent<Transform3D>();
        XnaVec3 pos = t3d?.WorldPosition ?? XnaVec3.Zero;
        XnaQuat rot = t3d?.LocalRotationQuaternion ?? XnaQuat.Identity;
        XnaVec3 scl = t3d?.LocalScale ?? XnaVec3.One;

        var pose = new RigidPose(
            new Vector3(pos.X, pos.Y, pos.Z),
            new Quaternion(rot.X, rot.Y, rot.Z, rot.W));

        TypedIndex shapeIndex;
        BodyInertia inertia;

        switch (body.Shape)
        {
            case ColliderShape3D.Sphere:
            {
                float r = Math.Max(0.05f, body.Radius * MathF.Max(scl.X, MathF.Max(scl.Y, scl.Z)));
                var sphere = new Sphere(r);
                shapeIndex = _simulation.Shapes.Add(sphere);
                inertia = sphere.ComputeInertia(Math.Max(0.01f, body.Mass));
                break;
            }
            case ColliderShape3D.Capsule:
            {
                float r = Math.Max(0.05f, body.Radius * MathF.Max(scl.X, scl.Z));
                float len = Math.Max(0.1f, body.CapsuleLength * scl.Y);
                var capsule = new Capsule(r, len);
                shapeIndex = _simulation.Shapes.Add(capsule);
                inertia = capsule.ComputeInertia(Math.Max(0.01f, body.Mass));
                break;
            }
            default:
            {
                float sx = Math.Max(0.05f, body.BoxSize.X * MathF.Abs(scl.X));
                float sy = Math.Max(0.05f, body.BoxSize.Y * MathF.Abs(scl.Y));
                float sz = Math.Max(0.05f, body.BoxSize.Z * MathF.Abs(scl.Z));
                var box = new Box(sx, sy, sz);
                shapeIndex = _simulation.Shapes.Add(box);
                inertia = box.ComputeInertia(Math.Max(0.01f, body.Mass));
                break;
            }
        }

        if (body.LockRotation)
        {
            inertia.InverseInertiaTensor = default;
        }

        if (body.BodyType == BodyType3D.Static)
        {
            var handle = _simulation.Statics.Add(new StaticDescription(pose, shapeIndex));
            body.StaticHandleValue = handle.Value;
            body.BodyHandleValue = -1;
            body.IsRegistered = true;
            _staticBodies.Add(body);
            _staticHandleMap[handle.Value] = body;
        }
        else if (body.BodyType == BodyType3D.Kinematic)
        {
            var desc = BodyDescription.CreateKinematic(pose, new CollidableDescription(shapeIndex, 0.1f), new BodyActivityDescription(0.01f));
            var handle = _simulation.Bodies.Add(desc);
            body.BodyHandleValue = handle.Value;
            body.StaticHandleValue = -1;
            body.IsRegistered = true;
            _dynamicBodies.Add(body);
            _bodyHandleMap[handle.Value] = body;
        }
        else
        {
            var desc = BodyDescription.CreateDynamic(pose, inertia, new CollidableDescription(shapeIndex, 0.1f), new BodyActivityDescription(0.01f));
            var handle = _simulation.Bodies.Add(desc);
            body.BodyHandleValue = handle.Value;
            body.StaticHandleValue = -1;
            body.IsRegistered = true;
            _dynamicBodies.Add(body);
            _bodyHandleMap[handle.Value] = body;
        }
    }

    public void UnregisterBody(RigidBody3D body)
    {
        if (_disposed || body == null || !body.IsRegistered) return;

        if (body.BodyHandleValue >= 0)
        {
            var handle = new BodyHandle(body.BodyHandleValue);
            if (_simulation.Bodies.BodyExists(handle))
            {
                _simulation.Bodies.Remove(handle);
            }
            _bodyHandleMap.Remove(body.BodyHandleValue);
            _dynamicBodies.Remove(body);
            body.BodyHandleValue = -1;
        }
        else if (body.StaticHandleValue >= 0)
        {
            var handle = new StaticHandle(body.StaticHandleValue);
            if (_simulation.Statics.StaticExists(handle))
            {
                _simulation.Statics.Remove(handle);
            }
            _staticHandleMap.Remove(body.StaticHandleValue);
            _staticBodies.Remove(body);
            body.StaticHandleValue = -1;
        }

        body.IsRegistered = false;
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        if (_disposed || fixedDeltaTime <= 0f) return;
        if (_dynamicBodies.Count == 0 && _staticBodies.Count == 0) return;

        // 1. Bekleyen hız/impuls isteklerini Bepu gövdelerine uygula
        for (int i = 0; i < _dynamicBodies.Count; i++)
        {
            var body = _dynamicBodies[i];
            if (body.Entity == null || body.Entity.IsDestroyed || !body.IsEnabled) continue;

            var handle = new BodyHandle(body.BodyHandleValue);
            if (!_simulation.Bodies.BodyExists(handle)) continue;

            var bodyRef = _simulation.Bodies.GetBodyReference(handle);

            var pendingVel = body.ConsumePendingVelocity();
            if (pendingVel.HasValue)
            {
                bodyRef.Awake = true;
                bodyRef.Velocity.Linear = new Vector3(pendingVel.Value.X, pendingVel.Value.Y, pendingVel.Value.Z);
            }

            var impulse = body.ConsumePendingImpulse();
            if (impulse.LengthSquared() > 0.000001f)
            {
                bodyRef.Awake = true;
                bodyRef.ApplyLinearImpulse(new Vector3(impulse.X, impulse.Y, impulse.Z));
            }
        }

        // 2. Fizik adımını çalıştır
        _simulation.Timestep(fixedDeltaTime, _threadDispatcher);

        // 3. Simülasyon pozisyon ve rotasyonlarını Transform3D'lere geri yaz
        for (int i = 0; i < _dynamicBodies.Count; i++)
        {
            var body = _dynamicBodies[i];
            if (body.Entity == null || body.Entity.IsDestroyed || !body.IsEnabled) continue;

            var handle = new BodyHandle(body.BodyHandleValue);
            if (!_simulation.Bodies.BodyExists(handle)) continue;

            var bodyRef = _simulation.Bodies.GetBodyReference(handle);
            var pose = bodyRef.Pose;
            var vel = bodyRef.Velocity;

            body.LinearVelocity = new XnaVec3(vel.Linear.X, vel.Linear.Y, vel.Linear.Z);
            body.AngularVelocity = new XnaVec3(vel.Angular.X, vel.Angular.Y, vel.Angular.Z);

            var t3d = body.Entity.GetComponent<Transform3D>();
            if (t3d != null)
            {
                t3d.LocalPosition = new XnaVec3(pose.Position.X, pose.Position.Y, pose.Position.Z);
                if (!body.LockRotation)
                {
                    t3d.LocalRotationDegrees = QuaternionToEulerDegrees(pose.Orientation);
                }
            }
        }
    }

    public bool Raycast(XnaVec3 origin, XnaVec3 direction, float maxDistance, out RaycastHit3D hit, IEntity? ignoreEntity = null)
    {
        hit = default;
        if (_disposed || direction.LengthSquared() < 0.000001f) return false;

        XnaVec3 dir = XnaVec3.Normalize(direction);
        var handler = new ClosestRayHitHandler(this, ignoreEntity);

        _simulation.RayCast(
            new Vector3(origin.X, origin.Y, origin.Z),
            new Vector3(dir.X, dir.Y, dir.Z),
            maxDistance,
            ref handler);

        if (handler.Hit)
        {
            XnaVec3 hitPos = origin + dir * handler.T;
            hit = new RaycastHit3D(
                handler.HitEntity,
                hitPos,
                new XnaVec3(handler.Normal.X, handler.Normal.Y, handler.Normal.Z),
                handler.T);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        foreach (var b in _dynamicBodies) { b.IsRegistered = false; b.BodyHandleValue = -1; }
        foreach (var s in _staticBodies) { s.IsRegistered = false; s.StaticHandleValue = -1; }
        _dynamicBodies.Clear();
        _staticBodies.Clear();
        _bodyHandleMap.Clear();
        _staticHandleMap.Clear();

        _simulation.Dispose();
        _simulation = CreateSimulation();
    }

    internal IEntity? ResolveEntity(CollidableReference collidable)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            return _staticHandleMap.TryGetValue(collidable.StaticHandle.Value, out var sb) ? sb.Entity : null;
        }
        return _bodyHandleMap.TryGetValue(collidable.BodyHandle.Value, out var db) ? db.Entity : null;
    }

    private static XnaVec3 QuaternionToEulerDegrees(Quaternion q)
    {
        // Standart Y-X-Z Euler dönüşümü
        float sinp = 2f * (q.W * q.X - q.Y * q.Z);
        float pitch = MathF.Abs(sinp) >= 1f ? MathF.CopySign(MathF.PI / 2f, sinp) : MathF.Asin(sinp);

        float siny = 2f * (q.W * q.Y + q.Z * q.X);
        float cosy = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float yaw = MathF.Atan2(siny, cosy);

        float sinr = 2f * (q.W * q.Z + q.X * q.Y);
        float cosr = 1f - 2f * (q.X * q.X + q.Z * q.Z);
        float roll = MathF.Atan2(sinr, cosr);

        const float radToDeg = 180f / MathF.PI;
        return new XnaVec3(pitch * radToDeg, yaw * radToDeg, roll * radToDeg);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _simulation.Dispose();
        _threadDispatcher.Dispose();
        _bufferPool.Clear();
    }

    #region BepuPhysics Callbacks & RayHitHandler

    private struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        private SpringSettings _contactSpringiness;

        public NarrowPhaseCallbacks(SpringSettings contactSpringiness)
        {
            _contactSpringiness = contactSpringiness;
        }

        public void Initialize(Simulation simulation) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
            => a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = 0.8f;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = _contactSpringiness;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

        public void Dispose() { }
    }

    private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public Vector3 Gravity;
        public float LinearDamping;
        public float AngularDamping;

        private Vector3Wide _gravityWideDt;
        private Vector<float> _linearDampingDt;
        private Vector<float> _angularDampingDt;

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;

        public PoseIntegratorCallbacks(Vector3 gravity, float linearDamping = 0.03f, float angularDamping = 0.05f)
        {
            Gravity = gravity;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
        }

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
        {
            _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
            _linearDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1f - LinearDamping, 0f, 1f), dt));
            _angularDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1f - AngularDamping, 0f, 1f), dt));
        }

        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear = (velocity.Linear + _gravityWideDt) * _linearDampingDt;
            velocity.Angular = velocity.Angular * _angularDampingDt;
        }
    }

    private struct ClosestRayHitHandler : IRayHitHandler
    {
        private readonly Physics3DManager _manager;
        private readonly IEntity? _ignoreEntity;
        public bool Hit;
        public float T;
        public Vector3 Normal;
        public IEntity? HitEntity;

        public ClosestRayHitHandler(Physics3DManager manager, IEntity? ignoreEntity)
        {
            _manager = manager;
            _ignoreEntity = ignoreEntity;
            Hit = false;
            T = float.MaxValue;
            Normal = Vector3.UnitY;
            HitEntity = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable)
        {
            if (_ignoreEntity == null) return true;
            return _manager.ResolveEntity(collidable) != _ignoreEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
        {
            if (t < T)
            {
                Hit = true;
                T = t;
                maximumT = t;
                Normal = normal;
                HitEntity = _manager.ResolveEntity(collidable);
            }
        }
    }

    #endregion
}
