using LightEngine.Core.Animation;
using LightEngine.Core.Assets;
using LightEngine.Core.Audio;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Particles;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Components;

/// <summary>
/// Oyuncu girdilerini okuyarak karakteri hareket ettiren, animasyon durum makinesini (State Machine)
/// besleyen, kamerayı takip ettiren, parçacık efektlerini ve ses efektlerini tetikleyen 2D kontrolcü.
/// </summary>
public class PlayerController : Component
{
    private ObjectPool<Entity>? _laserPool;
    private Texture2D? _laserTexture;
    private AnimationStateMachine? _stateMachine;
    private ParticleEmitter? _thrusterEmitter;
    private ParticleEmitter? _worldBurstEmitter;

    public float MoveSpeed { get; set; } = 320f;
    public float CameraSmoothSpeed { get; set; } = 6f;

    public void SetupLaserPool(ObjectPool<Entity> laserPool, Texture2D laserTexture)
    {
        _laserPool = laserPool;
        _laserTexture = laserTexture;
    }

    public void SetWorldBurstEmitter(ParticleEmitter emitter)
    {
        _worldBurstEmitter = emitter;
    }

    public override void Initialize()
    {
        base.Initialize();
        _stateMachine = Entity?.GetComponent<AnimationStateMachine>();
        _thrusterEmitter = Entity?.GetComponent<ParticleEmitter>();
    }

    public override void Update(float deltaTime)
    {
        if (Entity == null || !IsEnabled) return;

        var input = Engine.Services.Get<IInputService>();
        var scene = Entity.Scene;
        if (scene == null) return;

        var camera = scene.Camera;

        // 1. HAREKET (WASD / Ok tuşları / Eksenler)
        float moveX = input.GetAxis("Horizontal");
        float moveY = input.GetAxis("Vertical");
        Vector2 moveDir = new(moveX, moveY);
        float currentSpeed = 0f;

        if (moveDir.LengthSquared() > 0.001f)
        {
            moveDir.Normalize();
            Entity.Transform.Translate(moveDir * (MoveSpeed * deltaTime));
            currentSpeed = MoveSpeed;

            // Motor alevi parçacık yayılımını aktif et
            if (_thrusterEmitter != null)
            {
                _thrusterEmitter.IsEmitting = true;
            }
        }
        else
        {
            // Durulduğunda motor alevini durdur
            if (_thrusterEmitter != null)
            {
                _thrusterEmitter.IsEmitting = false;
            }
        }

        // 2. ANİMASYON DURUM MAKİNESİNE HIZ PARAMETRESİNİ İLET
        if (_stateMachine != null)
        {
            _stateMachine.SetFloat("Speed", currentSpeed);
        }

        // 3. FAREYE DOĞRU DÖNME (LookAt Mouse World Position)
        Vector2 mouseWorldPos = camera.ScreenToWorld(input.MousePosition);
        Vector2 toMouse = mouseWorldPos - Entity.Transform.WorldPosition;
        if (toMouse.LengthSquared() > 1f)
        {
            float targetAngle = MathF.Atan2(toMouse.Y, toMouse.X);
            Entity.Transform.WorldRotation = targetAngle;
        }

        // 4. KAMERA TAKİBİ (Camera Follow)
        camera.Follow(Entity.Transform.WorldPosition, CameraSmoothSpeed, deltaTime);

        // 5. KAMERA YAKINLAŞTIRMA (Mouse Scroll Zoom)
        int scroll = input.ScrollWheelDelta;
        if (scroll != 0)
        {
            float zoomChange = scroll > 0 ? 0.1f : -0.1f;
            camera.Zoom += zoomChange;
        }

        // Zoom sıfırlama (R tuşu)
        if (input.IsKeyPressed(Keys.R))
        {
            camera.Zoom = 1.0f;
        }

        // 6. PATLAMA VE KAMERA SARSINTISI TESTİ (Space veya E tuşu)
        if (input.IsActionPressed("ShakeCamera") || input.IsKeyPressed(Keys.E))
        {
            camera.Shake(intensity: 16f, duration: 0.4f);

            // Prosedürel patlama sesi çal
            if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
            {
                audio.PlayExplosionSound();
            }

            // Patlama parçacık patlaması üret
            _worldBurstEmitter?.EmitBurst(40);
        }

        // 7. HAVUZLANMIŞ MERMİ ATEŞLEME (Sol Tık veya Enter)
        if (input.IsActionPressed("Fire"))
        {
            FireLaser(toMouse);
        }

        // 8. SESİ AÇ/KAPAT (M tuşu)
        if (input.IsKeyPressed(Keys.M))
        {
            if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
            {
                audio.IsMuted = !audio.IsMuted;
            }
        }
    }

    private void FireLaser(Vector2 directionVector)
    {
        if (_laserPool == null || _laserTexture == null || Entity?.Scene == null) return;

        if (directionVector.LengthSquared() < 0.001f)
        {
            directionVector = Entity.Transform.Right;
        }
        else
        {
            directionVector.Normalize();
        }

        // Havuzdan nesne al (Sıfır yeni bellek tahsisi)
        var laserEntity = _laserPool.Get();
        laserEntity.IsActive = true;
        laserEntity.Transform.WorldPosition = Entity.Transform.WorldPosition + (directionVector * 24f);
        laserEntity.Transform.WorldRotation = MathF.Atan2(directionVector.Y, directionVector.X);

        var laserComp = laserEntity.GetComponent<PooledLaser>();
        if (laserComp != null)
        {
            laserComp.Direction = directionVector;
            laserComp.CurrentTimer = 0f;
            laserComp.OnLifetimeExpired = (returnedEntity) =>
            {
                returnedEntity.IsActive = false;
                _laserPool.Return(returnedEntity);
            };
        }

        // Prosedürel lazer sesi çal
        if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
        {
            audio.PlayLaserSound();
        }

        // Ateşlendiğinde hafif geri tepme sarsıntısı
        Entity.Scene.Camera.Shake(intensity: 3.5f, duration: 0.12f);
    }
}
