using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Particles;

/// <summary>
/// Sıfır GC bellek tahsisi (Zero Allocation) ile patlama, duman, alev ve kıvılcım gibi
/// efektleri üreten ve çizen 2D Parçacık Yayıcı (Particle Emitter) bileşeni.
/// </summary>
public class ParticleEmitter : Component
{
    private Particle[] _particles;
    private int _maxParticles;
    private float _emissionTimer;
    private Texture2D? _particleTexture;
    private readonly Random _random = new();

    public int MaxParticles
    {
        get => _maxParticles;
        set
        {
            _maxParticles = Math.Max(1, value);
            _particles = new Particle[_maxParticles];
        }
    }

    public bool IsEmitting { get; set; } = true;
    public float EmissionRate { get; set; } = 50f; // saniyede üretilen parçacık

    public float MinLifetime { get; set; } = 0.5f;
    public float MaxLifetime { get; set; } = 1.5f;

    public float MinSpeed { get; set; } = 50f;
    public float MaxSpeed { get; set; } = 150f;

    public float StartSize { get; set; } = 8f;
    public float EndSize { get; set; } = 2f;

    public Color StartColor { get; set; } = Color.Orange;
    public Color EndColor { get; set; } = new Color(255, 50, 0, 0);

    public Vector2 Gravity { get; set; } = Vector2.Zero;
    public float SpreadAngle { get; set; } = 360f; // Derece cinsinden yayılma
    public float BaseAngle { get; set; } = 0f;

    public Texture2D? Texture
    {
        get => _particleTexture;
        set => _particleTexture = value;
    }

    public ParticleEmitter(int maxParticles = 500)
    {
        _maxParticles = maxParticles;
        _particles = new Particle[_maxParticles];
    }

    public override void Initialize()
    {
        base.Initialize();
        if (_particleTexture == null && Engine.IsInitialized && Engine.Services.TryGet<IAssetService>(out var asset) && asset != null)
        {
            _particleTexture = asset.GetWhitePixel();
        }
    }

    /// <summary>
    /// Tek seferde anlık parçacık patlaması (Burst) üretir (örn: Mermi vuruşu, patlama).
    /// </summary>
    public void EmitBurst(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnSingleParticle();
        }
    }

    public override void Update(float deltaTime)
    {
        // 1. Yaşayan parçacıkları güncelle
        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i].IsAlive)
            {
                _particles[i].Update(deltaTime);
            }
        }

        // 2. Sürekli emisyon üretimi
        if (IsEmitting && EmissionRate > 0)
        {
            _emissionTimer += deltaTime;
            float timePerParticle = 1f / EmissionRate;

            while (_emissionTimer >= timePerParticle)
            {
                SpawnSingleParticle();
                _emissionTimer -= timePerParticle;
            }
        }
    }

    private void SpawnSingleParticle()
    {
        int slot = FindDeadParticleSlot();
        if (slot == -1) return; // Havuz dolu

        Vector2 emitterPos = Entity != null ? Entity.Transform.WorldPosition : Vector2.Zero;

        // Açı ve hız hesabı
        float spreadRad = MathHelper.ToRadians(SpreadAngle);
        float baseRad = MathHelper.ToRadians(BaseAngle);
        float angle = baseRad + ((float)_random.NextDouble() - 0.5f) * spreadRad;

        float speed = MathHelper.Lerp(MinSpeed, MaxSpeed, (float)_random.NextDouble());
        Vector2 velocity = new(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);

        float lifetime = MathHelper.Lerp(MinLifetime, MaxLifetime, (float)_random.NextDouble());

        _particles[slot] = new Particle
        {
            Position = emitterPos,
            Velocity = velocity,
            Acceleration = Gravity,
            StartColor = StartColor,
            EndColor = EndColor,
            CurrentColor = StartColor,
            StartSize = StartSize,
            EndSize = EndSize,
            CurrentSize = StartSize,
            Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi),
            AngularVelocity = ((float)_random.NextDouble() - 0.5f) * 5f,
            Lifetime = 0f,
            MaxLifetime = lifetime
        };
    }

    private int FindDeadParticleSlot()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].IsAlive) return i;
        }
        return -1;
    }

    public override void Draw(SpriteBatchRenderer renderer)
    {
        if (!IsEnabled || _particleTexture == null) return;

        Vector2 origin = new(_particleTexture.Width / 2f, _particleTexture.Height / 2f);

        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].IsAlive) continue;

            var p = _particles[i];
            Vector2 scale = new(p.CurrentSize / _particleTexture.Width, p.CurrentSize / _particleTexture.Height);

            renderer.DrawSprite(
                texture: _particleTexture,
                position: p.Position,
                sourceRectangle: null,
                color: p.CurrentColor,
                rotation: p.Rotation,
                origin: origin,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 0.9f);
        }
    }
}
