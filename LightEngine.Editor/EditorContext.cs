using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Serialization;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;

namespace LightEngine.Editor;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public enum GizmoType
{
    Translate,
    Rotate,
    Scale,
    None
}

public record LogEntry(DateTime Timestamp, string Message, LogLevel Level);

/// <summary>
/// Editörün genel durumunu (Seçili nesne, aktif sahne, Play/Pause durumu, 2D/3D modu, FPS, Gizmo, tercihler ve loglar) tutan bağlam sınıfı.
/// </summary>
public class EditorContext
{
    public IScene? ActiveScene { get; set; }
    public IEntity? SelectedEntity { get; set; }

    public bool IsPlaying { get; set; }
    public bool IsPaused { get; set; }

    // 2D / 3D Editör Görünüm Modu ve 3D Kalite
    public bool Is3DMode { get; set; } = true;
    public QualityPreset Quality3D { get; set; } = QualityPreset.Medium;

    // Gerçek zamanlı FPS ve kare süresi (Draw tabanlı)
    public float FPS { get; set; } = 60f;
    public float FrameTimeMs { get; set; } = 16.6f;

    // Tercihler (Preferences)
    public bool VSync { get; set; } = true;
    public bool IsFixedTimeStep { get; set; } = false;

    // Panel Görünürlükleri
    public bool ShowHierarchy { get; set; } = true;
    public bool ShowInspector { get; set; } = true;
    public bool ShowSceneViewport { get; set; } = true;
    public bool ShowGameViewport { get; set; } = true;
    public bool ShowAssetBrowser { get; set; } = true;
    public bool ShowConsole { get; set; } = false;
    public bool ShowPreferences { get; set; } = false;

    // Viewport Sekme Odak Talepleri (Play basınca Game sekmesine, Stop basınca Scene sekmesine geçer)
    public bool FocusGameTabRequested { get; set; }
    public bool FocusSceneTabRequested { get; set; }

    // Gizmo Modu & Izgara Hizalama
    public GizmoType ActiveGizmo { get; set; } = GizmoType.Translate;
    public bool SnapToGrid { get; set; } = false;
    public float GridSnapSize { get; set; } = 32f;

    public string CurrentScenePath { get; set; } = "Untitled.scene";
    public string SelectedAssetPath { get; set; } = "";

    public List<LogEntry> Logs { get; } = new();

    public event Action? OnSceneModified;
    public event Action? OnPreferencesChanged;

    public void NotifySceneModified()
    {
        OnSceneModified?.Invoke();
    }

    public void NotifyPreferencesChanged()
    {
        OnPreferencesChanged?.Invoke();
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        Logs.Add(new LogEntry(DateTime.Now, message, level));
        if (Logs.Count > 500)
        {
            Logs.RemoveAt(0);
        }
    }

    // Undo / Redo Komut Yığını
    private readonly Stack<Commands.IEditorCommand> _undoStack = new();
    private readonly Stack<Commands.IEditorCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void ExecuteCommand(Commands.IEditorCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        NotifySceneModified();
    }

    public void RecordCommand(Commands.IEditorCommand command)
    {
        _undoStack.Push(command);
        _redoStack.Clear();
        NotifySceneModified();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        Log($"Undo: {cmd.Description}", LogLevel.Info);
        NotifySceneModified();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        Log($"Redo: {cmd.Description}", LogLevel.Info);
        NotifySceneModified();
    }

    // Play / Stop Sahne Durum Kopyası (Snapshot)
    private string? _playModeSceneSnapshot;

    public void StartPlayMode()
    {
        if (ActiveScene == null || IsPlaying) return;
        try
        {
            _playModeSceneSnapshot = SceneSerializer.Serialize(ActiveScene);
        }
        catch (Exception ex)
        {
            Log($"Scene snapshot error: {ex.Message}", LogLevel.Warning);
        }

        IsPlaying = true;
        IsPaused = false;
        FocusGameTabRequested = true;
        Log("Game mode started. (Snapshot created for clean restore; switched to Game view)", LogLevel.Info);
    }

    public void StopPlayMode()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        IsPaused = false;
        FocusSceneTabRequested = true;

        if (!string.IsNullOrEmpty(_playModeSceneSnapshot))
        {
            try
            {
                var assetService = Engine.Services.Get<IAssetService>();
                var sceneService = Engine.Services.Get<ISceneService>();
                var restoredScene = SceneSerializer.Deserialize(_playModeSceneSnapshot, assetService);
                restoredScene.LoadContent();
                ActiveScene = restoredScene;
                sceneService.LoadScene(restoredScene);
                SelectedEntity = null;
                Log("Game mode stopped. Scene restored to pre-play edit state; switched back to Scene view.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"Scene restore error: {ex.Message}", LogLevel.Warning);
            }
        }
    }

    public IEntity? DuplicateEntity(IEntity entity)
    {
        if (ActiveScene == null || entity.IsDestroyed) return null;

        var clone = ActiveScene.CreateEntity($"{entity.Name}_Copy");
        clone.Tag = entity.Tag;
        clone.Transform.LocalPosition = entity.Transform.LocalPosition + new Vector2(25, 25);
        clone.Transform.LocalRotation = entity.Transform.LocalRotation;
        clone.Transform.LocalScale = entity.Transform.LocalScale;

        // Bileşenleri kopyala
        for (int i = 0; i < entity.Components.Count; i++)
        {
            var comp = entity.Components[i];
            if (comp is SpriteRenderer sr && sr.Texture != null)
            {
                clone.AddComponent(new SpriteRenderer(sr.Texture, sr.CenterOrigin)
                {
                    Color = sr.Color,
                    LayerDepth = sr.LayerDepth,
                    Scale = sr.Scale,
                    Effects = sr.Effects
                });
            }
            else if (comp is LightEngine.Core.Physics2D.BoxCollider2D box)
            {
                clone.AddComponent(new LightEngine.Core.Physics2D.BoxCollider2D(box.Width, box.Height)
                {
                    Offset = box.Offset,
                    IsTrigger = box.IsTrigger,
                    IsStatic = box.IsStatic,
                    Layer = box.Layer
                });
            }
            else if (comp is LightEngine.Core.Physics2D.CircleCollider2D circle)
            {
                clone.AddComponent(new LightEngine.Core.Physics2D.CircleCollider2D(circle.Radius)
                {
                    Offset = circle.Offset,
                    IsTrigger = circle.IsTrigger,
                    IsStatic = circle.IsStatic,
                    Layer = circle.Layer
                });
            }
            else if (comp is Transform3D t3d)
            {
                clone.AddComponent(new Transform3D
                {
                    LocalPosition = t3d.LocalPosition + new Vector3(1.5f, 0f, 0f),
                    LocalRotationDegrees = t3d.LocalRotationDegrees,
                    LocalScale = t3d.LocalScale
                });
            }
            else if (comp is MeshRenderer3D mr && mr.Mesh != null)
            {
                clone.AddComponent(new MeshRenderer3D(mr.Mesh, mr.Material.Clone())
                {
                    CastShadows = mr.CastShadows,
                    ReceiveShadows = mr.ReceiveShadows
                });
            }
            else if (comp is Light3D l3d)
            {
                clone.AddComponent(new Light3D
                {
                    LightType = l3d.LightType,
                    Color = l3d.Color,
                    SpecularColor = l3d.SpecularColor,
                    Intensity = l3d.Intensity,
                    Range = l3d.Range
                });
            }
            else if (comp is LightEngine.Core.Audio.AudioEmitter3D ae)
            {
                clone.AddComponent(new LightEngine.Core.Audio.AudioEmitter3D(ae.SoundPath, ae.Volume, ae.MaxDistance, ae.IsLooping)
                {
                    PlayOnStart = ae.PlayOnStart
                });
            }
            else if (comp is Terrain3D terrain)
            {
                clone.AddComponent(new Terrain3D(terrain.SizeX, terrain.SizeZ, terrain.MaxHeight, terrain.Seed)
                {
                    Frequency = terrain.Frequency,
                    ResolutionX = terrain.ResolutionX,
                    ResolutionZ = terrain.ResolutionZ,
                    TextureTiling = terrain.TextureTiling
                });
            }
            else if (comp is LightEngine.Core.Particles.ParticleEmitter3D pe3d)
            {
                clone.AddComponent(new LightEngine.Core.Particles.ParticleEmitter3D(pe3d.MaxParticles, pe3d.EmissionRate)
                {
                    ParticleLifetime = pe3d.ParticleLifetime,
                    StartSpeed = pe3d.StartSpeed,
                    SpreadAngle = pe3d.SpreadAngle,
                    StartSize = pe3d.StartSize,
                    EndSize = pe3d.EndSize,
                    Gravity = pe3d.Gravity,
                    StartColor = pe3d.StartColor,
                    EndColor = pe3d.EndColor
                });
            }
            else if (comp is ScriptComponent script && Engine.Services.TryGet<IScriptService>(out var scriptSvc) && scriptSvc != null)
            {
                var scriptCopy = scriptSvc.CreateScriptInstance(script.GetType().Name);
                if (scriptCopy != null) clone.AddComponent(scriptCopy);
            }
        }

        SelectedEntity = clone;
        Log($"Duplicated entity: {clone.Name}", LogLevel.Info);
        NotifySceneModified();
        return clone;
    }

    /// <summary>
    /// Aktif sahneye yeni bir 3D temel nesne (Küp, Küre, Düzlem, Silindir, Koni, Piramit) ekler ve seçer.
    /// </summary>
    public IEntity? Create3DPrimitive(PrimitiveType3D type, Vector3? position = null, Color? color = null)
    {
        if (ActiveScene == null) return null;
        if (!Engine.Services.TryGet<IRender3DService>(out var r3d) || r3d == null) return null;

        var entity = ActiveScene.CreateEntity($"{type}_3D");
        entity.Tag = "3DObject";

        var t3d = new Transform3D
        {
            LocalPosition = position ?? new Vector3(0f, type == PrimitiveType3D.Plane ? 0f : 0.75f, 0f)
        };
        entity.AddComponent(t3d);

        var mat = new Material3D
        {
            DiffuseColor = color ?? type switch
            {
                PrimitiveType3D.Cube => new Color(90, 165, 235),
                PrimitiveType3D.Sphere => new Color(235, 110, 95),
                PrimitiveType3D.Plane => new Color(85, 92, 108),
                PrimitiveType3D.Cylinder => new Color(95, 210, 145),
                PrimitiveType3D.Cone => new Color(235, 185, 75),
                PrimitiveType3D.Pyramid => new Color(185, 120, 235),
                _ => new Color(195, 205, 220)
            }
        };
        entity.AddComponent(new MeshRenderer3D(r3d.GetPrimitiveMesh(type), mat));

        Is3DMode = true;
        SelectedEntity = entity;
        Log($"Created 3D {type}: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye yeni bir 3D ışık kaynağı ekler ve seçer.
    /// </summary>
    public IEntity? Create3DLight(LightType3D lightType)
    {
        if (ActiveScene == null) return null;

        var entity = ActiveScene.CreateEntity($"{lightType}Light_3D");
        entity.Tag = "Light";

        entity.AddComponent(new Transform3D
        {
            LocalPosition = new Vector3(0f, 5f, 0f),
            LocalRotationDegrees = new Vector3(-45f, 35f, 0f)
        });
        entity.AddComponent(new Light3D
        {
            LightType = lightType,
            Color = lightType == LightType3D.Directional ? new Color(255, 244, 225) : new Color(140, 200, 255),
            Intensity = 1.0f
        });

        Is3DMode = true;
        SelectedEntity = entity;
        Log($"Created 3D {lightType} Light: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye BepuPhysics v2 destekli dinamik bir 3D fizik nesnesi (Küp veya Küre) ekler.
    /// </summary>
    public IEntity? Create3DPhysicsObject(PrimitiveType3D type, Vector3? position = null)
    {
        var entity = Create3DPrimitive(type, position);
        if (entity == null) return null;

        entity.Name = $"Physics_{type}_3D";
        entity.Tag = "Physics3D";

        var shape = type == PrimitiveType3D.Sphere ? LightEngine.Core.Physics3D.ColliderShape3D.Sphere : LightEngine.Core.Physics3D.ColliderShape3D.Box;
        var rb = new LightEngine.Core.Physics3D.RigidBody3D(LightEngine.Core.Physics3D.BodyType3D.Dynamic, shape, 1.5f);
        if (shape == LightEngine.Core.Physics3D.ColliderShape3D.Sphere) rb.Radius = 0.5f;
        else rb.BoxSize = Vector3.One;

        entity.AddComponent(rb);
        Log($"Attached BepuPhysics v2 Rigidbody to {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye WASD + Space Zıplama destekli 3D Karakter Kontrolcüsü ekler.
    /// </summary>
    public IEntity? Create3DCharacterController(Vector3? position = null)
    {
        var entity = Create3DPrimitive(PrimitiveType3D.Cylinder, position ?? new Vector3(0, 1.0f, 0));
        if (entity == null) return null;

        entity.Name = "Player_Character3D";
        entity.Tag = "Player3D";

        var t3d = entity.GetComponent<Transform3D>();
        if (t3d != null) t3d.LocalScale = new Vector3(0.8f, 1.8f, 0.8f);

        var rb = new LightEngine.Core.Physics3D.RigidBody3D(LightEngine.Core.Physics3D.BodyType3D.Dynamic, LightEngine.Core.Physics3D.ColliderShape3D.Capsule, 75f)
        {
            CapsuleLength = 1.0f,
            Radius = 0.4f,
            LockRotation = true
        };
        entity.AddComponent(rb);

        var cc = new LightEngine.Core.Physics3D.CharacterController3D
        {
            MoveSpeed = 7.0f,
            SprintMultiplier = 1.6f,
            JumpVelocity = 6.5f
        };
        entity.AddComponent(cc);

        Log($"Created 3D Character Controller: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye 3D uzamsal ses yayıcısı (AudioEmitter3D) ekler.
    /// </summary>
    public IEntity? Create3DAudioEmitter(Vector3? position = null)
    {
        if (ActiveScene == null) return null;

        var entity = ActiveScene.CreateEntity("AudioEmitter_3D");
        entity.Tag = "Audio3D";

        entity.AddComponent(new Transform3D
        {
            LocalPosition = position ?? new Vector3(0, 1.5f, 0)
        });
        entity.AddComponent(new LightEngine.Core.Audio.AudioEmitter3D("", 1.0f, 35f, true));

        Is3DMode = true;
        SelectedEntity = entity;
        Log($"Created 3D Audio Emitter: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye prosedürel yükseklik haritalı 3D Arazi (Terrain3D) ekler.
    /// </summary>
    public IEntity? Create3DTerrain(Vector3? position = null)
    {
        if (ActiveScene == null) return null;

        var entity = ActiveScene.CreateEntity("Terrain_3D");
        entity.Tag = "Ground";

        entity.AddComponent(new Transform3D
        {
            LocalPosition = position ?? new Vector3(0f, 0f, 0f)
        });

        var terrain = new Terrain3D(40f, 40f, 4.5f, 1337);
        entity.AddComponent(terrain);

        Is3DMode = true;
        SelectedEntity = entity;
        Log($"Created 3D Terrain: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    /// <summary>
    /// Aktif sahneye 3D Parçacık Yayıcısı (ParticleEmitter3D) ekler.
    /// </summary>
    public IEntity? Create3DParticleEmitter(Vector3? position = null)
    {
        if (ActiveScene == null) return null;

        var entity = ActiveScene.CreateEntity("ParticleEmitter_3D");
        entity.Tag = "FX3D";

        entity.AddComponent(new Transform3D
        {
            LocalPosition = position ?? new Vector3(0f, 1.0f, 0f)
        });

        var emitter = new LightEngine.Core.Particles.ParticleEmitter3D(256, 60.0f);
        entity.AddComponent(emitter);

        Is3DMode = true;
        SelectedEntity = entity;
        Log($"Created 3D Particle Emitter: {entity.Name}", LogLevel.Info);
        NotifySceneModified();
        return entity;
    }

    public void ClearLogs()
    {
        Logs.Clear();
    }
}

