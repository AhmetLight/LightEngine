using System.Globalization;
using System.Text.Json;
using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Particles;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Physics3D;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Serialization;

/// <summary>
/// Sahneleri insan tarafından okunabilir JSON formatında diske kaydeden ve yükleyen serileştirici sınıfı.
/// </summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Sahneyi biçimlendirilmiş bir JSON metnine dönüştürür.
    /// </summary>
    public static string Serialize(IScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var data = new SceneData
        {
            Name = scene.Name
        };

        for (int i = 0; i < scene.Entities.Count; i++)
        {
            var entity = scene.Entities[i];
            if (entity.IsDestroyed) continue;

            var entityData = new EntityData
            {
                Id = entity.Id.ToString(),
                Name = entity.Name,
                Tag = entity.Tag,
                IsActive = entity.IsActive,
                Transform = new TransformData
                {
                    X = entity.Transform.LocalPosition.X,
                    Y = entity.Transform.LocalPosition.Y,
                    Rotation = entity.Transform.LocalRotation,
                    ScaleX = entity.Transform.LocalScale.X,
                    ScaleY = entity.Transform.LocalScale.Y
                }
            };

            for (int c = 0; c < entity.Components.Count; c++)
            {
                var comp = entity.Components[c];
                var compData = SerializeComponent(comp);
                if (compData != null)
                {
                    entityData.Components.Add(compData);
                }
            }

            data.Entities.Add(entityData);
        }

        return JsonSerializer.Serialize(data, s_options);
    }

    /// <summary>
    /// Sahneyi belirtilen dosya yoluna JSON olarak kaydeder.
    /// </summary>
    public static void SaveToFile(IScene scene, string filePath)
    {
        string json = Serialize(scene);
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// JSON metnini ayrıştırarak yeni bir Scene nesnesi üretir.
    /// </summary>
    public static IScene Deserialize(string jsonContent, IAssetService assetService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        var data = JsonSerializer.Deserialize<SceneData>(jsonContent, s_options)
                   ?? throw new InvalidOperationException("JSON sahne verisi çözümlenemedi.");

        var scene = new Scene(data.Name);

        for (int i = 0; i < data.Entities.Count; i++)
        {
            var entityData = data.Entities[i];
            var entity = scene.CreateEntity(entityData.Name);
            entity.Tag = entityData.Tag;
            entity.IsActive = entityData.IsActive;

            entity.Transform.LocalPosition = new Vector2(entityData.Transform.X, entityData.Transform.Y);
            entity.Transform.LocalRotation = entityData.Transform.Rotation;
            entity.Transform.LocalScale = new Vector2(entityData.Transform.ScaleX, entityData.Transform.ScaleY);

            for (int c = 0; c < entityData.Components.Count; c++)
            {
                var compData = entityData.Components[c];
                DeserializeAndAttachComponent(entity, compData, assetService);
            }
        }

        scene.Initialize();
        return scene;
    }

    /// <summary>
    /// JSON dosyasından sahneyi okur ve yükler.
    /// </summary>
    public static IScene LoadFromFile(string filePath, IAssetService assetService)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Sahne dosyası bulunamadı: {filePath}");
        }

        string json = File.ReadAllText(filePath);
        return Deserialize(json, assetService);
    }

    private static ComponentData? SerializeComponent(IComponent comp)
    {
        var data = new ComponentData
        {
            Type = comp.GetType().Name
        };

        if (comp is SpriteRenderer sr)
        {
            data.Properties["ColorR"] = sr.Color.R.ToString();
            data.Properties["ColorG"] = sr.Color.G.ToString();
            data.Properties["ColorB"] = sr.Color.B.ToString();
            data.Properties["ColorA"] = sr.Color.A.ToString();
            data.Properties["LayerDepth"] = sr.LayerDepth.ToString();
            data.Properties["CenterOrigin"] = sr.CenterOrigin.ToString();
            return data;
        }

        if (comp is BoxCollider2D box)
        {
            data.Properties["Width"] = box.Width.ToString();
            data.Properties["Height"] = box.Height.ToString();
            data.Properties["IsTrigger"] = box.IsTrigger.ToString();
            data.Properties["IsStatic"] = box.IsStatic.ToString();
            data.Properties["Layer"] = ((int)box.Layer).ToString();
            return data;
        }

        if (comp is CircleCollider2D circle)
        {
            data.Properties["Radius"] = circle.Radius.ToString();
            data.Properties["IsTrigger"] = circle.IsTrigger.ToString();
            data.Properties["IsStatic"] = circle.IsStatic.ToString();
            data.Properties["Layer"] = ((int)circle.Layer).ToString();
            return data;
        }

        if (comp is ParticleEmitter pe)
        {
            data.Properties["MaxParticles"] = pe.MaxParticles.ToString();
            data.Properties["EmissionRate"] = pe.EmissionRate.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is Transform3D t3d)
        {
            data.Properties["PosX"] = t3d.LocalPosition.X.ToString(CultureInfo.InvariantCulture);
            data.Properties["PosY"] = t3d.LocalPosition.Y.ToString(CultureInfo.InvariantCulture);
            data.Properties["PosZ"] = t3d.LocalPosition.Z.ToString(CultureInfo.InvariantCulture);
            data.Properties["RotX"] = t3d.LocalRotationDegrees.X.ToString(CultureInfo.InvariantCulture);
            data.Properties["RotY"] = t3d.LocalRotationDegrees.Y.ToString(CultureInfo.InvariantCulture);
            data.Properties["RotZ"] = t3d.LocalRotationDegrees.Z.ToString(CultureInfo.InvariantCulture);
            data.Properties["ScaleX"] = t3d.LocalScale.X.ToString(CultureInfo.InvariantCulture);
            data.Properties["ScaleY"] = t3d.LocalScale.Y.ToString(CultureInfo.InvariantCulture);
            data.Properties["ScaleZ"] = t3d.LocalScale.Z.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is MeshRenderer3D mr)
        {
            data.Properties["Primitive"] = (mr.Mesh?.PrimitiveType ?? PrimitiveType3D.Cube).ToString();
            if (!string.IsNullOrEmpty(mr.Mesh?.SourcePath))
            {
                data.Properties["ModelPath"] = mr.Mesh.SourcePath;
            }
            data.Properties["ColorR"] = mr.Material.DiffuseColor.R.ToString();
            data.Properties["ColorG"] = mr.Material.DiffuseColor.G.ToString();
            data.Properties["ColorB"] = mr.Material.DiffuseColor.B.ToString();
            data.Properties["SpecularPower"] = mr.Material.SpecularPower.ToString(CultureInfo.InvariantCulture);
            data.Properties["Wireframe"] = mr.Material.Wireframe.ToString();
            data.Properties["EnableLighting"] = mr.Material.EnableLighting.ToString();
            return data;
        }

        if (comp is Light3D light)
        {
            data.Properties["LightType"] = light.LightType.ToString();
            data.Properties["ColorR"] = light.Color.R.ToString();
            data.Properties["ColorG"] = light.Color.G.ToString();
            data.Properties["ColorB"] = light.Color.B.ToString();
            data.Properties["Intensity"] = light.Intensity.ToString(CultureInfo.InvariantCulture);
            data.Properties["Range"] = light.Range.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is RigidBody3D rb3d)
        {
            data.Properties["BodyType"] = rb3d.BodyType.ToString();
            data.Properties["Shape"] = rb3d.Shape.ToString();
            data.Properties["Mass"] = rb3d.Mass.ToString(CultureInfo.InvariantCulture);
            data.Properties["BoxX"] = rb3d.BoxSize.X.ToString(CultureInfo.InvariantCulture);
            data.Properties["BoxY"] = rb3d.BoxSize.Y.ToString(CultureInfo.InvariantCulture);
            data.Properties["BoxZ"] = rb3d.BoxSize.Z.ToString(CultureInfo.InvariantCulture);
            data.Properties["Radius"] = rb3d.Radius.ToString(CultureInfo.InvariantCulture);
            data.Properties["CapsuleLength"] = rb3d.CapsuleLength.ToString(CultureInfo.InvariantCulture);
            data.Properties["LockRotation"] = rb3d.LockRotation.ToString();
            return data;
        }

        if (comp is CharacterController3D cc3d)
        {
            data.Properties["MoveSpeed"] = cc3d.MoveSpeed.ToString(CultureInfo.InvariantCulture);
            data.Properties["SprintMultiplier"] = cc3d.SprintMultiplier.ToString(CultureInfo.InvariantCulture);
            data.Properties["JumpVelocity"] = cc3d.JumpVelocity.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is LightEngine.Core.Audio.AudioEmitter3D ae3d)
        {
            data.Properties["SoundPath"] = ae3d.SoundPath;
            data.Properties["Volume"] = ae3d.Volume.ToString(CultureInfo.InvariantCulture);
            data.Properties["MaxDistance"] = ae3d.MaxDistance.ToString(CultureInfo.InvariantCulture);
            data.Properties["PlayOnStart"] = ae3d.PlayOnStart.ToString();
            data.Properties["IsLooping"] = ae3d.IsLooping.ToString();
            return data;
        }

        if (comp is Terrain3D terrain)
        {
            data.Properties["SizeX"] = terrain.SizeX.ToString(CultureInfo.InvariantCulture);
            data.Properties["SizeZ"] = terrain.SizeZ.ToString(CultureInfo.InvariantCulture);
            data.Properties["MaxHeight"] = terrain.MaxHeight.ToString(CultureInfo.InvariantCulture);
            data.Properties["Frequency"] = terrain.Frequency.ToString(CultureInfo.InvariantCulture);
            data.Properties["Seed"] = terrain.Seed.ToString();
            data.Properties["ResolutionX"] = terrain.ResolutionX.ToString();
            data.Properties["ResolutionZ"] = terrain.ResolutionZ.ToString();
            data.Properties["TextureTiling"] = terrain.TextureTiling.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is LightEngine.Core.Particles.ParticleEmitter3D pe3d)
        {
            data.Properties["MaxParticles"] = pe3d.MaxParticles.ToString();
            data.Properties["EmissionRate"] = pe3d.EmissionRate.ToString(CultureInfo.InvariantCulture);
            data.Properties["ParticleLifetime"] = pe3d.ParticleLifetime.ToString(CultureInfo.InvariantCulture);
            data.Properties["StartSpeed"] = pe3d.StartSpeed.ToString(CultureInfo.InvariantCulture);
            data.Properties["SpreadAngle"] = pe3d.SpreadAngle.ToString(CultureInfo.InvariantCulture);
            data.Properties["StartSize"] = pe3d.StartSize.ToString(CultureInfo.InvariantCulture);
            data.Properties["EndSize"] = pe3d.EndSize.ToString(CultureInfo.InvariantCulture);
            data.Properties["GravityY"] = pe3d.Gravity.Y.ToString(CultureInfo.InvariantCulture);
            return data;
        }

        if (comp is ScriptComponent script)
        {
            data.Type = "ScriptComponent";
            data.Properties["ScriptName"] = script.GetType().Name;
            return data;
        }

        return null;
    }

    private static void DeserializeAndAttachComponent(IEntity entity, ComponentData data, IAssetService assetService)
    {
        switch (data.Type)
        {
            case nameof(SpriteRenderer):
            {
                byte r = byte.Parse(data.Properties.GetValueOrDefault("ColorR", "255"));
                byte g = byte.Parse(data.Properties.GetValueOrDefault("ColorG", "255"));
                byte b = byte.Parse(data.Properties.GetValueOrDefault("ColorB", "255"));
                byte a = byte.Parse(data.Properties.GetValueOrDefault("ColorA", "255"));
                float depth = ParseFloat(data.Properties.GetValueOrDefault("LayerDepth", "0"));
                bool center = bool.Parse(data.Properties.GetValueOrDefault("CenterOrigin", "true"));

                var whitePixel = assetService.GetWhitePixel();
                var sr = new SpriteRenderer(whitePixel, center)
                {
                    Color = new Color(r, g, b, a),
                    LayerDepth = depth
                };
                entity.AddComponent(sr);
                break;
            }

            case nameof(BoxCollider2D):
            {
                float w = ParseFloat(data.Properties.GetValueOrDefault("Width", "32"));
                float h = ParseFloat(data.Properties.GetValueOrDefault("Height", "32"));
                bool trigger = bool.Parse(data.Properties.GetValueOrDefault("IsTrigger", "false"));
                bool isStatic = bool.Parse(data.Properties.GetValueOrDefault("IsStatic", "false"));
                int layer = int.Parse(data.Properties.GetValueOrDefault("Layer", "1"));

                var collider = new BoxCollider2D(w, h)
                {
                    IsTrigger = trigger,
                    IsStatic = isStatic,
                    Layer = (CollisionLayer)layer
                };
                entity.AddComponent(collider);
                break;
            }

            case nameof(CircleCollider2D):
            {
                float rad = ParseFloat(data.Properties.GetValueOrDefault("Radius", "16"));
                bool trigger = bool.Parse(data.Properties.GetValueOrDefault("IsTrigger", "false"));
                bool isStatic = bool.Parse(data.Properties.GetValueOrDefault("IsStatic", "false"));
                int layer = int.Parse(data.Properties.GetValueOrDefault("Layer", "1"));

                var circle = new CircleCollider2D(rad)
                {
                    IsTrigger = trigger,
                    IsStatic = isStatic,
                    Layer = (CollisionLayer)layer
                };
                entity.AddComponent(circle);
                break;
            }

            case nameof(ParticleEmitter):
            {
                int max = int.Parse(data.Properties.GetValueOrDefault("MaxParticles", "500"));
                float rate = ParseFloat(data.Properties.GetValueOrDefault("EmissionRate", "50"));
                var emitter = new ParticleEmitter(max)
                {
                    EmissionRate = rate
                };
                entity.AddComponent(emitter);
                break;
            }

            case nameof(Transform3D):
            {
                var t3d = new Transform3D
                {
                    LocalPosition = new Vector3(
                        ParseFloat(data.Properties.GetValueOrDefault("PosX", "0")),
                        ParseFloat(data.Properties.GetValueOrDefault("PosY", "0")),
                        ParseFloat(data.Properties.GetValueOrDefault("PosZ", "0"))),
                    LocalRotationDegrees = new Vector3(
                        ParseFloat(data.Properties.GetValueOrDefault("RotX", "0")),
                        ParseFloat(data.Properties.GetValueOrDefault("RotY", "0")),
                        ParseFloat(data.Properties.GetValueOrDefault("RotZ", "0"))),
                    LocalScale = new Vector3(
                        ParseFloat(data.Properties.GetValueOrDefault("ScaleX", "1")),
                        ParseFloat(data.Properties.GetValueOrDefault("ScaleY", "1")),
                        ParseFloat(data.Properties.GetValueOrDefault("ScaleZ", "1")))
                };
                entity.AddComponent(t3d);
                break;
            }

            case nameof(MeshRenderer3D):
            {
                if (!Engine.IsInitialized || !Engine.Services.TryGet<IRender3DService>(out var r3d) || r3d == null)
                    break;

                Mesh3D mesh;
                if (data.Properties.TryGetValue("ModelPath", out var modelPath) && File.Exists(modelPath))
                {
                    mesh = r3d.LoadModel(modelPath);
                }
                else
                {
                    Enum.TryParse<PrimitiveType3D>(data.Properties.GetValueOrDefault("Primitive", "Cube"), out var prim);
                    mesh = r3d.GetPrimitiveMesh(prim);
                }

                byte r = byte.Parse(data.Properties.GetValueOrDefault("ColorR", "195"));
                byte g = byte.Parse(data.Properties.GetValueOrDefault("ColorG", "205"));
                byte b = byte.Parse(data.Properties.GetValueOrDefault("ColorB", "220"));

                var mat = new Material3D
                {
                    DiffuseColor = new Color(r, g, b),
                    SpecularPower = ParseFloat(data.Properties.GetValueOrDefault("SpecularPower", "32")),
                    Wireframe = bool.Parse(data.Properties.GetValueOrDefault("Wireframe", "false")),
                    EnableLighting = bool.Parse(data.Properties.GetValueOrDefault("EnableLighting", "true"))
                };
                entity.AddComponent(new MeshRenderer3D(mesh, mat));
                break;
            }

            case nameof(Light3D):
            {
                Enum.TryParse<LightType3D>(data.Properties.GetValueOrDefault("LightType", "Directional"), out var lt);
                byte r = byte.Parse(data.Properties.GetValueOrDefault("ColorR", "255"));
                byte g = byte.Parse(data.Properties.GetValueOrDefault("ColorG", "245"));
                byte b = byte.Parse(data.Properties.GetValueOrDefault("ColorB", "230"));

                entity.AddComponent(new Light3D
                {
                    LightType = lt,
                    Color = new Color(r, g, b),
                    Intensity = ParseFloat(data.Properties.GetValueOrDefault("Intensity", "1")),
                    Range = ParseFloat(data.Properties.GetValueOrDefault("Range", "25"))
                });
                break;
            }

            case nameof(RigidBody3D):
            {
                Enum.TryParse<BodyType3D>(data.Properties.GetValueOrDefault("BodyType", "Dynamic"), out var bt);
                Enum.TryParse<ColliderShape3D>(data.Properties.GetValueOrDefault("Shape", "Box"), out var sh);
                entity.AddComponent(new RigidBody3D(bt, sh, ParseFloat(data.Properties.GetValueOrDefault("Mass", "1")))
                {
                    BoxSize = new Vector3(
                        ParseFloat(data.Properties.GetValueOrDefault("BoxX", "1")),
                        ParseFloat(data.Properties.GetValueOrDefault("BoxY", "1")),
                        ParseFloat(data.Properties.GetValueOrDefault("BoxZ", "1"))),
                    Radius = ParseFloat(data.Properties.GetValueOrDefault("Radius", "0.5")),
                    CapsuleLength = ParseFloat(data.Properties.GetValueOrDefault("CapsuleLength", "1")),
                    LockRotation = bool.Parse(data.Properties.GetValueOrDefault("LockRotation", "false"))
                });
                break;
            }

            case nameof(CharacterController3D):
            {
                entity.AddComponent(new CharacterController3D
                {
                    MoveSpeed = ParseFloat(data.Properties.GetValueOrDefault("MoveSpeed", "6.5")),
                    SprintMultiplier = ParseFloat(data.Properties.GetValueOrDefault("SprintMultiplier", "1.6")),
                    JumpVelocity = ParseFloat(data.Properties.GetValueOrDefault("JumpVelocity", "6.2"))
                });
                break;
            }

            case nameof(LightEngine.Core.Audio.AudioEmitter3D):
            {
                string sound = data.Properties.GetValueOrDefault("SoundPath", "");
                float vol = ParseFloat(data.Properties.GetValueOrDefault("Volume", "1.0"));
                float maxDist = ParseFloat(data.Properties.GetValueOrDefault("MaxDistance", "35.0"));
                bool playStart = bool.Parse(data.Properties.GetValueOrDefault("PlayOnStart", "false"));
                bool loop = bool.Parse(data.Properties.GetValueOrDefault("IsLooping", "false"));

                entity.AddComponent(new LightEngine.Core.Audio.AudioEmitter3D(sound, vol, maxDist, loop)
                {
                    PlayOnStart = playStart
                });
                break;
            }

            case nameof(Terrain3D):
            {
                float sx = ParseFloat(data.Properties.GetValueOrDefault("SizeX", "40"));
                float sz = ParseFloat(data.Properties.GetValueOrDefault("SizeZ", "40"));
                float mh = ParseFloat(data.Properties.GetValueOrDefault("MaxHeight", "4.5"));
                float freq = ParseFloat(data.Properties.GetValueOrDefault("Frequency", "0.08"));
                int seed = int.Parse(data.Properties.GetValueOrDefault("Seed", "1337"));
                int rx = int.Parse(data.Properties.GetValueOrDefault("ResolutionX", "32"));
                int rz = int.Parse(data.Properties.GetValueOrDefault("ResolutionZ", "32"));
                float tiling = ParseFloat(data.Properties.GetValueOrDefault("TextureTiling", "8"));

                entity.AddComponent(new Terrain3D(sx, sz, mh, seed)
                {
                    Frequency = freq,
                    ResolutionX = rx,
                    ResolutionZ = rz,
                    TextureTiling = tiling
                });
                break;
            }

            case nameof(LightEngine.Core.Particles.ParticleEmitter3D):
            {
                int maxP = int.Parse(data.Properties.GetValueOrDefault("MaxParticles", "256"));
                float rate = ParseFloat(data.Properties.GetValueOrDefault("EmissionRate", "60"));
                float life = ParseFloat(data.Properties.GetValueOrDefault("ParticleLifetime", "1.5"));
                float spd = ParseFloat(data.Properties.GetValueOrDefault("StartSpeed", "4"));
                float spread = ParseFloat(data.Properties.GetValueOrDefault("SpreadAngle", "0.5"));
                float sSize = ParseFloat(data.Properties.GetValueOrDefault("StartSize", "0.35"));
                float eSize = ParseFloat(data.Properties.GetValueOrDefault("EndSize", "0.05"));
                float gravY = ParseFloat(data.Properties.GetValueOrDefault("GravityY", "-2.5"));

                entity.AddComponent(new LightEngine.Core.Particles.ParticleEmitter3D(maxP, rate)
                {
                    ParticleLifetime = life,
                    StartSpeed = spd,
                    SpreadAngle = spread,
                    StartSize = sSize,
                    EndSize = eSize,
                    Gravity = new Vector3(0f, gravY, 0f)
                });
                break;
            }

            case "ScriptComponent":
            {
                if (Engine.IsInitialized && Engine.Services.TryGet<IScriptService>(out var scriptSvc) && scriptSvc != null)
                {
                    string scriptName = data.Properties.GetValueOrDefault("ScriptName", "");
                    var instance = scriptSvc.CreateScriptInstance(scriptName);
                    if (instance != null)
                    {
                        entity.AddComponent(instance);
                    }
                }
                break;
            }
        }
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float inv))
            return inv;
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out float cur))
            return cur;
        return 0f;
    }
}
