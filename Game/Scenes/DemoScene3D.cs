using LightEngine.Core.Animation3D;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Physics3D;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Scenes;

/// <summary>
/// Light Engine 3D yeteneklerini sergileyen oynanabilir 3D örnek sahne:
/// BepuPhysics v2 katı cisim fiziği, CharacterController3D (WASD + Zıplama),
/// Baked Lightmap, Blinn-Phong çoklu ışıklandırma, SkeletalAnimator3D ve fizik küresi fırlatma.
/// </summary>
public class DemoScene3D : Scene
{
    private IEntity? _player;
    private float _spawnCooldown;

    public DemoScene3D() : base("DemoScene_3D") { }

    public override void LoadContent()
    {
        base.LoadContent();

        var r3d = Engine.Services.Get<IRender3DService>();
        var phys3d = Engine.Services.Get<IPhysics3DService>();
        phys3d.Clear();

        // 1. Güneş Işığı (Directional Light)
        var sun = CreateEntity("SunLight");
        sun.AddComponent(new Transform3D
        {
            LocalPosition = new Vector3(0f, 12f, 0f),
            LocalRotationDegrees = new Vector3(-48f, 32f, 0f)
        });
        sun.AddComponent(new Light3D
        {
            LightType = LightType3D.Directional,
            Color = new Color(255, 244, 224),
            Intensity = 1.1f
        });

        // 2. Statik Zemin (BepuPhysics v2 Statik Kutu + Baked Lightmap alıcısı)
        var floor = CreateEntity("Floor");
        floor.Tag = "Ground";
        floor.AddComponent(new Transform3D
        {
            LocalPosition = new Vector3(0f, -0.1f, 0f),
            LocalScale = new Vector3(2.4f, 1f, 2.4f)
        });
        floor.AddComponent(new MeshRenderer3D(
            r3d.GetPrimitiveMesh(PrimitiveType3D.Plane),
            new Material3D { DiffuseColor = new Color(88, 96, 112), Roughness = 0.85f }));
        floor.AddComponent(new RigidBody3D(BodyType3D.Static, ColliderShape3D.Box, 0f)
        {
            BoxSize = new Vector3(24f, 0.2f, 24f)
        });

        // 3. Oyuncu (CharacterController3D + Humanoid SkeletalAnimator3D)
        _player = CreateEntity("Player3D");
        _player.Tag = "Player";
        _player.AddComponent(new Transform3D
        {
            LocalPosition = new Vector3(0f, 1.5f, 5f)
        });
        _player.AddComponent(new MeshRenderer3D(
            r3d.GetPrimitiveMesh(PrimitiveType3D.Cylinder),
            new Material3D { DiffuseColor = new Color(65, 175, 250), Roughness = 0.3f }));
        _player.AddComponent(new CharacterController3D
        {
            MoveSpeed = 6.5f,
            JumpVelocity = 6.5f
        });

        var skel = Skeleton3D.CreateSimpleHumanoid();
        var animator = new SkeletalAnimator3D(skel);
        var idleClip = new AnimationClip3D("Idle", 1.2f, true);
        var spineCh = idleClip.AddChannel(skel.FindBoneIndex("Spine"));
        spineCh.AddKeyframe(0f, new Vector3(0f, 0.9f, 0f), Quaternion.Identity, Vector3.One);
        spineCh.AddKeyframe(0.6f, new Vector3(0f, 0.95f, 0f), Quaternion.Identity, Vector3.One);
        spineCh.AddKeyframe(1.2f, new Vector3(0f, 0.9f, 0f), Quaternion.Identity, Vector3.One);
        animator.AddClip(idleClip);
        animator.Play("Idle");
        _player.AddComponent(animator);

        // 4. Fizikli Kutu Kulesi (BepuPhysics v2 Dinamik Küpler)
        Color[] crateColors =
        {
            new(225, 145, 55),
            new(235, 95, 85),
            new(95, 205, 135),
            new(165, 120, 235)
        };

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3 - row; col++)
            {
                var crate = CreateEntity($"Crate_{row}_{col}");
                crate.Tag = "Crate";
                float x = (col - (2 - row) * 0.5f) * 1.15f;
                float y = 0.6f + row * 1.15f;
                crate.AddComponent(new Transform3D { LocalPosition = new Vector3(x, y, -2.5f) });
                crate.AddComponent(new MeshRenderer3D(
                    r3d.GetPrimitiveMesh(PrimitiveType3D.Cube),
                    new Material3D { DiffuseColor = crateColors[(row + col) % crateColors.Length], Roughness = 0.4f }));
                crate.AddComponent(new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Box, 1.5f)
                {
                    BoxSize = Vector3.One
                });
            }
        }

        // Kamera başlangıç konumu
        Camera3D.Position = new Vector3(0f, 4.5f, 11.5f);
        Camera3D.LookAt(new Vector3(0f, 1.0f, -1f));

        Initialize();

        // Statik zemin Lightmap'ini pişir (Half-Life 2 tarzı yumuşak gölgeler)
        r3d.BakeStaticLightmaps(this, 128);
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        _spawnCooldown = Math.Max(0f, _spawnCooldown - deltaTime);

        // Oyuncuyu üçüncü şahıs 3D kamera ile yumuşak takip et
        if (_player != null && !_player.IsDestroyed)
        {
            var pt = _player.GetComponent<Transform3D>();
            if (pt != null)
            {
                Vector3 desiredCam = pt.WorldPosition + new Vector3(0f, 4.2f, 8.5f);
                Camera3D.Position = Vector3.Lerp(Camera3D.Position, desiredCam, Math.Clamp(deltaTime * 5f, 0f, 1f));
                Camera3D.LookAt(pt.WorldPosition + new Vector3(0f, 0.5f, -2f));
            }
        }

        // E tuşu veya sol tık ile 3D fizik küresi fırlat!
        var input = Engine.Services.Get<IInputService>();
        if (_spawnCooldown <= 0f && (input.IsKeyPressed(Keys.E) || input.IsKeyPressed(Keys.Enter)))
        {
            _spawnCooldown = 0.25f;
            LaunchPhysicsBall();
        }
    }

    private void LaunchPhysicsBall()
    {
        var r3d = Engine.Services.Get<IRender3DService>();
        var ball = CreateEntity("PhysicsBall");
        ball.Tag = "Projectile";

        Vector3 spawnPos = Camera3D.Position + Camera3D.Forward * 2.0f;
        ball.AddComponent(new Transform3D
        {
            LocalPosition = spawnPos,
            LocalScale = new Vector3(0.75f)
        });
        ball.AddComponent(new MeshRenderer3D(
            r3d.GetPrimitiveMesh(PrimitiveType3D.Sphere),
            new Material3D { DiffuseColor = new Color(255, 200, 60), Metallic = 0.7f, Roughness = 0.15f }));

        var rb = new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Sphere, 3.0f) { Radius = 0.375f };
        ball.AddComponent(rb);
        ball.Initialize();
        rb.SetLinearVelocity(Camera3D.Forward * 18f);
    }
}
