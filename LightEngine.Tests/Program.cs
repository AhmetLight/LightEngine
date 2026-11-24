using LightEngine.Core.Animation3D;
using LightEngine.Core.Audio;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Physics3D;
using LightEngine.Core.Rendering3D;
using LightEngine.Core.Scripting;
using LightEngine.Core.Serialization;
using Microsoft.Xna.Framework;

namespace LightEngine.Tests;

/// <summary>
/// Light Engine'in tüm aşamalarını (ECS, Physics2D, JSON, Roslyn Hot Reload, 3D Math/Camera,
/// BepuPhysics v2 3D Physics, CharacterController3D, 3D Skeletal Animation Blend, 3D Spatial Audio & Atmosphere)
/// başsız (headless) olarak doğrulayan kapsamlı otomasyon test koşucusu.
/// </summary>
public static class Program
{
    private static int _passed;
    private static int _failed;

    public static int Main()
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("  Light Engine v1.0 - Full 9-Stage Verification Test Suite");
        Console.WriteLine("============================================================");

        RunTest("Stage 1: ServiceLocator, ObjectPool & ECS Transform2D Hierarchy", TestStage1Core);
        RunTest("Stage 2: Physics2D AABB Collision & Trigger Callbacks", TestStage2Physics2D);
        RunTest("Stage 2/5/6: SceneSerializer JSON Round-Trip (2D + 3D + Physics3D)", TestSerializationRoundTrip);
        RunTest("Stage 4: Roslyn C# Script Compilation & Live Hot Reload", TestStage4ScriptingHotReload);
        RunTest("Stage 5: Transform3D Hierarchy, Camera3D Frustum Culling & Ray Picking", TestStage5MathAndCamera3D);
        RunTest("Stage 6: BepuPhysics v2 Gravity, Floor Collision, Raycast3D & CharacterController3D", TestStage6BepuPhysics3D);
        RunTest("Stage 6: 3D Skeletal Animation Cross-Fade Blend & AnimationStateMachine3D", TestStage6SkeletalAnimation3D);
        RunTest("Stage 8: 3D Spatial Audio (AudioEmitter3D) & Procedural Atmosphere", TestStage8SpatialAudioAndAtmosphere);
        RunTest("Stage 9: Procedural Terrain3D (Heightmap) & ParticleEmitter3D (Zero-GC)", TestStage9TerrainAndParticleEmitter3D);

        Console.WriteLine("============================================================");
        Console.WriteLine($"  RESULTS: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("============================================================");

        return _failed == 0 ? 0 : 1;
    }

    private static void RunTest(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"[FAIL] {name}");
            Console.WriteLine($"       {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }

    private static void TestStage1Core()
    {
        var locator = new ServiceLocator();
        Engine.Services = locator;

        var pool = new ObjectPool<List<int>>(initialCapacity: 4, onReturn: l => l.Clear());
        var item = pool.Get();
        item.Add(42);
        pool.Return(item);
        var reused = pool.Get();
        Assert(ReferenceEquals(item, reused) && reused.Count == 0, "ObjectPool should reuse and reset instances.");

        var scene = new Scene("TestScene");
        var parent = scene.CreateEntity("Parent");
        parent.Transform.LocalPosition = new Vector2(100, 50);

        var child = scene.CreateEntity("Child");
        child.Transform.SetParent(parent.Transform);
        child.Transform.LocalPosition = new Vector2(20, 10);

        Assert(child.Transform.WorldPosition == new Vector2(120, 60),
            $"Expected child world pos (120, 60), got {child.Transform.WorldPosition}");
    }

    private static void TestStage2Physics2D()
    {
        Engine.Services = new ServiceLocator();
        var phys2d = new Physics2DManager();
        Engine.Services.Register<IPhysics2DService>(phys2d);

        var scene = new Scene("Phys2DTest");
        var a = scene.CreateEntity("BoxA");
        a.Transform.LocalPosition = new Vector2(0, 0);
        var colA = new BoxCollider2D(40, 40) { IsTrigger = true, Layer = CollisionLayer.Player };
        a.AddComponent(colA);

        var b = scene.CreateEntity("BoxB");
        b.Transform.LocalPosition = new Vector2(20, 0);
        var colB = new BoxCollider2D(40, 40) { Layer = CollisionLayer.Enemy };
        b.AddComponent(colB);

        bool triggered = false;
        colA.OnTriggerEnter += other => { if (other == colB) triggered = true; };

        scene.Initialize();
        phys2d.FixedUpdate(1f / 60f);

        Assert(triggered, "BoxCollider2D trigger overlap should invoke OnTriggerEnter.");
    }

    private static void TestSerializationRoundTrip()
    {
        Engine.Services = new ServiceLocator();
        var scene = new Scene("HybridSaveTest");
        var e = scene.CreateEntity("Hero3D");
        e.Tag = "Player";
        e.AddComponent(new Transform3D
        {
            LocalPosition = new Vector3(1.5f, 2.5f, -3.5f),
            LocalRotationDegrees = new Vector3(10f, 45f, 0f),
            LocalScale = new Vector3(2f, 2f, 2f)
        });
        e.AddComponent(new Light3D { LightType = LightType3D.Point, Intensity = 2.5f, Range = 30f });
        e.AddComponent(new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Sphere, 5f) { Radius = 0.75f });

        string json = SceneSerializer.Serialize(scene);
        Assert(json.Contains("Transform3D") && json.Contains("Light3D") && json.Contains("RigidBody3D"),
            "Serialized JSON must include Transform3D, Light3D, and RigidBody3D.");

        var loaded = SceneSerializer.Deserialize(json, null!);
        var restored = loaded.FindEntityByName("Hero3D");
        Assert(restored != null, "Deserialized entity Hero3D not found.");

        var t3d = restored!.GetComponent<Transform3D>();
        Assert(t3d != null && MathF.Abs(t3d.LocalPosition.X - 1.5f) < 0.01f && MathF.Abs(t3d.LocalPosition.Z + 3.5f) < 0.01f,
            "Transform3D coordinates must round-trip accurately.");

        var rb = restored.GetComponent<RigidBody3D>();
        Assert(rb != null && rb.Shape == ColliderShape3D.Sphere && MathF.Abs(rb.Mass - 5f) < 0.01f,
            "RigidBody3D properties must round-trip accurately.");
    }

    private static void TestStage4ScriptingHotReload()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"LightEngine_ScriptTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var scriptMgr = new ScriptManager();
            string scriptPath = Path.Combine(tempDir, "TestMover.cs");

            // Sürüm 1: X pozisyonunu +Speed * dt kadar artırır
            File.WriteAllText(scriptPath, @"
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;
public class TestMover : ScriptComponent
{
    public float Speed = 10f;
    public override void OnUpdate(float dt)
    {
        if (Transform3D != null) Transform3D.Translate(new Vector3(Speed * dt, 0f, 0f));
    }
}");

            scriptMgr.Initialize(tempDir);
            Assert(scriptMgr.AvailableScriptNames.Contains("TestMover"), "Roslyn should compile TestMover script.");

            var scene = new Scene("ScriptScene");
            var entity = scene.CreateEntity("Actor");
            var t3d = new Transform3D();
            entity.AddComponent(t3d);

            var scriptV1 = scriptMgr.CreateScriptInstance("TestMover");
            Assert(scriptV1 != null, "CreateScriptInstance(TestMover) returned null.");

            // Public alanı çalışma zamanında 25f olarak değiştir (Hot Reload sonrası korunmalı!)
            scriptV1!.GetType().GetField("Speed")!.SetValue(scriptV1, 25f);
            entity.AddComponent(scriptV1);
            scene.Initialize();

            scene.Update(1.0f);
            Assert(MathF.Abs(t3d.LocalPosition.X - 25f) < 0.01f, $"Expected X=25 after V1 update, got {t3d.LocalPosition.X}");

            // Sürüm 2: Artık Y ekseninde +Speed * dt hareket ettirsin! (Hot Reload)
            File.WriteAllText(scriptPath, @"
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;
public class TestMover : ScriptComponent
{
    public float Speed = 10f;
    public override void OnUpdate(float dt)
    {
        if (Transform3D != null) Transform3D.Translate(new Vector3(0f, Speed * dt, 0f));
    }
}");

            bool reloaded = scriptMgr.ReloadScripts(scene);
            Assert(reloaded, "Hot reload compilation failed: " + string.Join("; ", scriptMgr.LastDiagnostics));

            scene.Update(1.0f);
            Assert(MathF.Abs(t3d.LocalPosition.Y - 25f) < 0.01f,
                $"Hot Reload must preserve Speed=25 and execute V2 logic (Expected Y=25, got {t3d.LocalPosition.Y}).");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void TestStage5MathAndCamera3D()
    {
        var parent = new Transform3D { LocalPosition = new Vector3(10f, 0f, 0f) };
        var child = new Transform3D { Parent = parent, LocalPosition = new Vector3(0f, 5f, 0f) };
        Assert(Vector3.Distance(child.WorldPosition, new Vector3(10f, 5f, 0f)) < 0.001f,
            $"Transform3D hierarchy mismatch: {child.WorldPosition}");

        var cam = new Camera3D { Position = new Vector3(0f, 2f, 10f), AspectRatio = 16f / 9f };
        cam.LookAt(Vector3.Zero);

        var visibleBox = new BoundingBox(new Vector3(-1f), new Vector3(1f));
        var behindBox = new BoundingBox(new Vector3(-1f, -1f, 30f), new Vector3(1f, 1f, 32f));

        Assert(cam.Frustum.Contains(visibleBox) != ContainmentType.Disjoint, "Box in front of camera should be inside frustum.");
        Assert(cam.Frustum.Contains(behindBox) == ContainmentType.Disjoint, "Box behind camera should be culled.");

        Ray centerRay = cam.ScreenPointToRay(new Vector2(640f, 360f), 1280, 720);
        float? hit = centerRay.Intersects(visibleBox);
        Assert(hit.HasValue && hit.Value > 0f, "Center screen ray should intersect origin bounding box.");

        var q = new QualitySettings3D();
        q.ApplyPreset(QualityPreset.Low);
        Assert(!q.FogEnabled && !q.PreferPerPixelLighting, "Low quality preset should disable fog and per-pixel lighting.");
        q.ApplyPreset(QualityPreset.High);
        Assert(q.FogEnabled && q.PreferPerPixelLighting, "High quality preset should enable fog and per-pixel lighting.");
    }

    private static void TestStage6BepuPhysics3D()
    {
        Engine.Services = new ServiceLocator();
        using var phys3d = new Physics3DManager();
        Engine.Services.Register<IPhysics3DService>(phys3d);

        var scene = new Scene("BepuTest");

        // Statik Zemin (Y = 0)
        var floor = scene.CreateEntity("Floor");
        floor.AddComponent(new Transform3D { LocalPosition = new Vector3(0f, -0.5f, 0f) });
        floor.AddComponent(new RigidBody3D(BodyType3D.Static, ColliderShape3D.Box, 0f) { BoxSize = new Vector3(20f, 1f, 20f) });

        // Dinamik Düşen Küre (Y = 4)
        var ball = scene.CreateEntity("Ball");
        var ballT = new Transform3D { LocalPosition = new Vector3(0f, 4f, 0f) };
        ball.AddComponent(ballT);
        ball.AddComponent(new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Sphere, 1f) { Radius = 0.5f });

        // Karakter Kontrolcüsü (X = 4, Y = 1.0)
        var player = scene.CreateEntity("Player");
        var playerT = new Transform3D { LocalPosition = new Vector3(4f, 1.0f, 0f) };
        player.AddComponent(playerT);
        var cc = new CharacterController3D { UseDefaultInput = false, JumpVelocity = 7f };
        player.AddComponent(cc);

        scene.Initialize();

        // 120 sabit fizik adımı (2 saniye) simüle et
        for (int i = 0; i < 120; i++)
        {
            phys3d.FixedUpdate(1f / 60f);
            scene.FixedUpdate(1f / 60f);
        }

        // Küre yere düşüp Y ~ 0.5 civarında durmuş olmalı (zeminin içine geçmemeli!)
        Assert(ballT.LocalPosition.Y < 1.5f && ballT.LocalPosition.Y > 0.2f,
            $"BepuPhysics dynamic sphere should land on floor (expected ~0.5, got {ballT.LocalPosition.Y:0.00}).");

        // 3D Raycast testi
        bool rayHit = phys3d.Raycast(new Vector3(0f, 5f, 0f), Vector3.Down, 10f, out var hitInfo);
        Assert(rayHit && hitInfo.Entity == ball, $"Expected raycast to hit Ball, got {hitInfo.Entity?.Name ?? "null"}");

        // CharacterController3D zemin algılama ve zıplama testi
        Assert(cc.IsGrounded, "CharacterController3D should detect static floor below it.");
        bool jumped = cc.Jump();
        Assert(jumped, "CharacterController3D.Jump() should succeed while grounded.");
        phys3d.FixedUpdate(1f / 60f);
        Assert(player.GetComponent<RigidBody3D>()!.LinearVelocity.Y > 4f, "Player should have upward velocity after Jump().");
    }

    private static void TestStage6SkeletalAnimation3D()
    {
        var skeleton = Skeleton3D.CreateSimpleHumanoid();
        Assert(skeleton.BoneCount == 5, "Simple humanoid should have 5 bones.");

        int spineIdx = skeleton.FindBoneIndex("Spine");

        var idleClip = new AnimationClip3D("Idle", 1.0f, true);
        var idleCh = idleClip.AddChannel(spineIdx);
        idleCh.AddKeyframe(0f, new Vector3(0, 0.9f, 0), Quaternion.Identity, Vector3.One);
        idleCh.AddKeyframe(1f, new Vector3(0, 0.9f, 0), Quaternion.Identity, Vector3.One);

        var walkClip = new AnimationClip3D("Walk", 1.0f, true);
        var walkCh = walkClip.AddChannel(spineIdx);
        walkCh.AddKeyframe(0f, new Vector3(0, 1.2f, 0), Quaternion.CreateFromYawPitchRoll(0, 0.2f, 0), Vector3.One);
        walkCh.AddKeyframe(1f, new Vector3(0, 1.2f, 0), Quaternion.CreateFromYawPitchRoll(0, 0.2f, 0), Vector3.One);

        var scene = new Scene("Anim3DScene");
        var npc = scene.CreateEntity("NPC");
        var animator = new SkeletalAnimator3D(skeleton);
        animator.AddClip(idleClip);
        animator.AddClip(walkClip);

        var fsm = new AnimationStateMachine3D();
        fsm.AddState("Idle", "Idle", isDefault: true);
        fsm.AddState("Walk", "Walk");
        fsm.AddTransition("Idle", "Walk", m => m.GetFloat("Speed") > 0.5f, blendDuration: 0.4f);

        npc.AddComponent(animator);
        npc.AddComponent(fsm);
        scene.Initialize();

        Assert(fsm.CurrentState == "Idle", "Initial state should be Idle.");

        // Hızı artır -> StateMachine otomatik Walk'a CrossFade başlatmalı
        fsm.SetFloat("Speed", 3.0f);
        scene.Update(0.01f); // Geçişi tetikle
        Assert(fsm.CurrentState == "Walk", "StateMachine3D should transition to Walk when Speed > 0.5.");

        scene.Update(0.2f); // 0.4s blend'in yarısı (%50 blend)
        Assert(animator.IsBlending && animator.BlendFactor > 0.2f && animator.BlendFactor < 0.8f,
            $"Animator should be mid Cross-Fade blend (got {animator.BlendFactor:0.00}).");

        // Blend tamamlandığında
        scene.Update(0.3f);
        Assert(!animator.IsBlending, "Cross-Fade blend should finish after blendDuration.");
        Assert(animator.SkinningMatrices.Count == 5, "SkinningMatrices count should match bone count.");
    }

    private static void TestStage8SpatialAudioAndAtmosphere()
    {
        // 1. Procedural Skybox & Half-Life 2 Atmosfer Ayarları
        var quality = new QualitySettings3D();
        Assert(quality.SkyboxEnabled, "Skybox should be enabled by default.");
        Assert(quality.SkyZenithColor != quality.SkyHorizonColor, "Sky zenith and horizon colors should form an atmospheric gradient.");

        // 2. 3D Spatial Audio & Uzamsal Ses Bileşeni
        var scene = new Scene("AudioScene");
        var emitterEntity = scene.CreateEntity("SoundSource");
        var t3d = new Transform3D { LocalPosition = new Vector3(0f, 0f, 20f) };
        emitterEntity.AddComponent(t3d);
        var emitter = new AudioEmitter3D("laser.wav", volume: 0.8f, maxDistance: 40.0f, isLooping: true)
        {
            PlayOnStart = false
        };
        emitterEntity.AddComponent(emitter);

        var lightEntity = scene.CreateEntity("SunLight");
        var sun = new Light3D { LightType = LightType3D.Directional, Color = Color.White, Intensity = 1.2f };
        lightEntity.AddComponent(sun);

        scene.Initialize();

        Assert(emitter.Volume == 0.8f && emitter.MaxDistance == 40.0f && emitter.IsLooping,
            "AudioEmitter3D properties should match constructor arguments.");

        // 3. JSON Serialization Round-Trip (AudioEmitter3D + Light3D + Transform3D)
        string json = SceneSerializer.Serialize(scene);
        Assert(json.Contains("AudioEmitter3D") && json.Contains("laser.wav") && json.Contains("SunLight"),
            "Serialized scene must contain AudioEmitter3D and SunLight.");

        var restoredScene = SceneSerializer.Deserialize(json, null!);
        var restoredEmitter = restoredScene.FindEntityByName("SoundSource")?.GetComponent<AudioEmitter3D>();
        Assert(restoredEmitter != null && restoredEmitter.SoundPath == "laser.wav" && MathF.Abs(restoredEmitter.MaxDistance - 40f) < 0.01f,
            "AudioEmitter3D should serialize and deserialize accurately.");

        var restoredSun = restoredScene.FindEntityByName("SunLight")?.GetComponent<Light3D>();
        Assert(restoredSun != null && restoredSun.LightType == LightType3D.Directional && MathF.Abs(restoredSun.Intensity - 1.2f) < 0.01f,
            "Light3D should round-trip correctly.");
    }

    private static void TestStage9TerrainAndParticleEmitter3D()
    {
        Engine.Services = new ServiceLocator();

        // 1. Prosedürel Terrain3D ve Yükseklik Örnekleme (Analytical Height Sampling)
        var terrain = new Terrain3D(50f, 50f, 6.0f, seed: 42);
        float centerH = terrain.SampleHeight(0f, 0f);
        Assert(centerH >= 0f && centerH <= 6.0f, $"Terrain center height should be between 0 and 6, got {centerH}");

        float outH = terrain.SampleHeight(120f, 120f);
        Assert(outH == 0f, "Terrain height outside boundary should sample to 0");

        // 2. 3D Parçacık Yayıcısı (ParticleEmitter3D) Zero-GC Simülasyonu
        var emitter = new LightEngine.Core.Particles.ParticleEmitter3D(128, 50.0f)
        {
            ParticleLifetime = 2.0f,
            StartSpeed = 5.0f,
            Gravity = new Vector3(0f, -9.8f, 0f)
        };

        // Simülasyon adımı işlet -> Parçacık üretilmeli ve güncellenmeli
        emitter.Update(0.1f);
        Assert(emitter.ActiveCount > 0, $"ParticleEmitter3D should emit active particles (got {emitter.ActiveCount})");

        // 3. JSON Serialization Round-Trip (Terrain3D + ParticleEmitter3D)
        var scene = new Scene("TerrainAndFXScene");
        var terrEnt = scene.CreateEntity("TerrainEntity");
        terrEnt.AddComponent(new Transform3D { LocalPosition = new Vector3(0, 0, 0) });
        terrEnt.AddComponent(terrain);

        var fxEnt = scene.CreateEntity("FireFX");
        fxEnt.AddComponent(new Transform3D { LocalPosition = new Vector3(0, 2f, 0) });
        fxEnt.AddComponent(emitter);

        string json = SceneSerializer.Serialize(scene);
        Assert(json.Contains("Terrain3D") && json.Contains("ParticleEmitter3D"),
            "Serialized JSON must contain Terrain3D and ParticleEmitter3D components.");

        var restoredScene = SceneSerializer.Deserialize(json, null!);
        var restoredTerrain = restoredScene.FindEntityByName("TerrainEntity")?.GetComponent<Terrain3D>();
        Assert(restoredTerrain != null && MathF.Abs(restoredTerrain.MaxHeight - 6.0f) < 0.01f && restoredTerrain.Seed == 42,
            "Terrain3D should accurately round-trip all heightmap parameters.");

        var restoredEmitter = restoredScene.FindEntityByName("FireFX")?.GetComponent<LightEngine.Core.Particles.ParticleEmitter3D>();
        Assert(restoredEmitter != null && restoredEmitter.MaxParticles == 128 && MathF.Abs(restoredEmitter.ParticleLifetime - 2.0f) < 0.01f,
            "ParticleEmitter3D should accurately round-trip simulation parameters.");
    }
}
