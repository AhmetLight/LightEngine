using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Particles;

/// <summary>
/// 3D parçacık verisi (Hafızada düz struct olarak tutulur, heap tahsisi yapmaz).
/// </summary>
public struct Particle3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Color Color;
    public float Size;
    public float Age;
    public float Lifetime;
    public bool IsActive;
}

/// <summary>
/// 3D kamera yönelimli (Billboard) parçacık yayıcı bileşeni.
/// Ateş, duman, kıvılcım, toz ve patlama efektlerini çalışma zamanında sıfır GC çöpü ile üretir.
/// </summary>
public class ParticleEmitter3D : Component
{
    private Particle3D[] _particles = null!;
    private VertexPositionColorTexture[] _vertices = null!;
    private short[] _indices = null!;
    private DynamicVertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private BasicEffect? _effect;

    private float _emissionTimer;
    private readonly Random _random = new(42);

    public int MaxParticles { get; set; } = 256;
    public float EmissionRate { get; set; } = 60.0f; // Saniyede üretilen parçacık adedi
    public float ParticleLifetime { get; set; } = 1.5f;
    public float StartSpeed { get; set; } = 4.0f;
    public Vector3 Direction { get; set; } = Vector3.Up;
    public float SpreadAngle { get; set; } = 0.5f; // Konik yayılma açısı (radyan)
    public Vector3 Gravity { get; set; } = new Vector3(0f, -2.5f, 0f);

    public Color StartColor { get; set; } = new Color(255, 180, 50, 255);
    public Color EndColor { get; set; } = new Color(220, 50, 20, 0);
    public float StartSize { get; set; } = 0.35f;
    public float EndSize { get; set; } = 0.05f;

    public bool IsEmitting { get; set; } = true;
    public int ActiveCount { get; private set; }

    public ParticleEmitter3D()
    {
        AllocateBuffers();
    }

    public ParticleEmitter3D(int maxParticles, float emissionRate)
    {
        MaxParticles = Math.Max(16, maxParticles);
        EmissionRate = emissionRate;
        AllocateBuffers();
    }

    private void AllocateBuffers()
    {
        _particles = new Particle3D[MaxParticles];
        _vertices = new VertexPositionColorTexture[MaxParticles * 4];
        _indices = new short[MaxParticles * 6];

        for (int i = 0; i < MaxParticles; i++)
        {
            int vStart = i * 4;
            int iStart = i * 6;

            _indices[iStart + 0] = (short)(vStart + 0);
            _indices[iStart + 1] = (short)(vStart + 1);
            _indices[iStart + 2] = (short)(vStart + 2);

            _indices[iStart + 3] = (short)(vStart + 0);
            _indices[iStart + 4] = (short)(vStart + 2);
            _indices[iStart + 5] = (short)(vStart + 3);
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        Vector3 spawnOrigin = Vector3.Zero;
        if (Entity != null)
        {
            var t3d = Entity.GetComponent<Transform3D>();
            if (t3d != null) spawnOrigin = t3d.WorldPosition;
        }

        // 1. Yeni parçacıklar üret
        if (IsEmitting && EmissionRate > 0)
        {
            _emissionTimer += deltaTime;
            float interval = 1.0f / EmissionRate;
            while (_emissionTimer >= interval)
            {
                SpawnSingleParticle(spawnOrigin);
                _emissionTimer -= interval;
            }
        }

        // 2. Mevcut parçacıkları güncelle
        int active = 0;
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].IsActive) continue;

            _particles[i].Age += deltaTime;
            if (_particles[i].Age >= _particles[i].Lifetime)
            {
                _particles[i].IsActive = false;
                continue;
            }

            float t = _particles[i].Age / _particles[i].Lifetime;
            _particles[i].Velocity += Gravity * deltaTime;
            _particles[i].Position += _particles[i].Velocity * deltaTime;
            _particles[i].Color = Color.Lerp(StartColor, EndColor, t);
            _particles[i].Size = MathHelper.Lerp(StartSize, EndSize, t);
            active++;
        }

        ActiveCount = active;
    }

    private void SpawnSingleParticle(Vector3 origin)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].IsActive)
            {
                _particles[i].IsActive = true;
                _particles[i].Age = 0f;
                _particles[i].Lifetime = ParticleLifetime * (0.8f + (float)_random.NextDouble() * 0.4f);
                _particles[i].Position = origin;
                _particles[i].Color = StartColor;
                _particles[i].Size = StartSize;

                // Konik rastgele yön hesapla
                Vector3 dir = Direction;
                if (dir.LengthSquared() < 0.001f) dir = Vector3.Up;
                dir.Normalize();

                float rx = ((float)_random.NextDouble() * 2f - 1f) * SpreadAngle;
                float ry = ((float)_random.NextDouble() * 2f - 1f) * SpreadAngle;
                float rz = ((float)_random.NextDouble() * 2f - 1f) * SpreadAngle;
                Vector3 vel = (dir + new Vector3(rx, ry, rz));
                vel.Normalize();

                float speed = StartSpeed * (0.8f + (float)_random.NextDouble() * 0.4f);
                _particles[i].Velocity = vel * speed;
                break;
            }
        }
    }

    /// <summary>
    /// Kameraya bakan billboard kuadları olarak 3D parçacıkları çizer.
    /// </summary>
    public void Draw3D(GraphicsDevice graphicsDevice, Camera3D camera)
    {
        if (ActiveCount == 0 || camera == null) return;

        EnsureGpuBuffers(graphicsDevice);

        Vector3 camRight = camera.Right;
        Vector3 camUp = camera.Up;

        int vertIdx = 0;
        int activeDrawn = 0;

        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].IsActive) continue;

            Vector3 pos = _particles[i].Position;
            float halfSize = _particles[i].Size * 0.5f;
            Color col = _particles[i].Color;

            Vector3 right = camRight * halfSize;
            Vector3 up = camUp * halfSize;

            _vertices[vertIdx + 0] = new VertexPositionColorTexture(pos - right - up, col, new Vector2(0, 1));
            _vertices[vertIdx + 1] = new VertexPositionColorTexture(pos - right + up, col, new Vector2(0, 0));
            _vertices[vertIdx + 2] = new VertexPositionColorTexture(pos + right + up, col, new Vector2(1, 0));
            _vertices[vertIdx + 3] = new VertexPositionColorTexture(pos + right - up, col, new Vector2(1, 1));

            vertIdx += 4;
            activeDrawn++;
        }

        if (activeDrawn == 0) return;

        _vertexBuffer!.SetData(_vertices, 0, activeDrawn * 4);

        if (_effect == null)
        {
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
        }

        _effect.View = camera.ViewMatrix;
        _effect.Projection = camera.ProjectionMatrix;
        _effect.World = Matrix.Identity;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;

        var prevBlend = graphicsDevice.BlendState;
        var prevDepth = graphicsDevice.DepthStencilState;

        graphicsDevice.BlendState = BlendState.Additive;
        graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, activeDrawn * 2);
        }

        graphicsDevice.BlendState = prevBlend;
        graphicsDevice.DepthStencilState = prevDepth;
    }

    private void EnsureGpuBuffers(GraphicsDevice graphicsDevice)
    {
        if (_vertexBuffer == null || _vertexBuffer.IsDisposed)
        {
            _vertexBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(VertexPositionColorTexture), MaxParticles * 4, BufferUsage.WriteOnly);
        }
        if (_indexBuffer == null || _indexBuffer.IsDisposed)
        {
            _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, MaxParticles * 6, BufferUsage.WriteOnly);
            _indexBuffer.SetData(_indices);
        }
    }

    public override void Destroy()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _effect?.Dispose();
        _vertexBuffer = null;
        _indexBuffer = null;
        _effect = null;
        base.Destroy();
    }
}
