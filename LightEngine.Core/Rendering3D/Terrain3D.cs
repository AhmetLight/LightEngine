using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Physics3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Yükseklik haritası (Heightmap) tabanlı prosedürel 3D arazi (Terrain) bileşeni.
/// Düşük sistemlerde akıcı çalışacak şekilde optimize edilmiş tepe/vadi kırılımları,
/// yumuşak normal hesaplaması, doku koordinatları ve BepuPhysics v2 statik zemin çarpışması üretir.
/// </summary>
public class Terrain3D : Component
{
    private Mesh3D? _mesh;
    private MeshRenderer3D? _renderer;
    private RigidBody3D? _rigidBody;

    public int ResolutionX { get; set; } = 32;
    public int ResolutionZ { get; set; } = 32;
    public float SizeX { get; set; } = 40.0f;
    public float SizeZ { get; set; } = 40.0f;
    public float MaxHeight { get; set; } = 4.5f;
    public float Frequency { get; set; } = 0.08f;
    public int Seed { get; set; } = 1337;
    public float TextureTiling { get; set; } = 8.0f;

    public Mesh3D? GeneratedMesh => _mesh;

    public Terrain3D() { }

    public Terrain3D(float sizeX, float sizeZ, float maxHeight, int seed = 1337)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
        MaxHeight = maxHeight;
        Seed = seed;
    }

    public override void Initialize()
    {
        base.Initialize();
        EnsureMeshAndCollision();
    }

    /// <summary>
    /// Arazi geometrisini ve çarpışmasını verilen parametrelerle yeniden oluşturur.
    /// </summary>
    public void Regenerate()
    {
        _mesh?.Dispose();
        _mesh = null;
        EnsureMeshAndCollision();
    }

    public void EnsureMeshAndCollision()
    {
        if (Entity == null) return;

        if (Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
        {
            var gd = r3d.GraphicsDevice;
            if (_mesh == null && gd != null)
            {
                _mesh = GenerateTerrainMesh(gd);
            }

            _renderer = Entity.GetComponent<MeshRenderer3D>();
            if (_renderer == null && _mesh != null)
            {
                var mat = new Material3D
                {
                    DiffuseColor = new Color(75, 130, 85),
                    Roughness = 0.88f,
                    SpecularPower = 12f
                };
                _renderer = new MeshRenderer3D(_mesh, mat);
                Entity.AddComponent(_renderer);
            }
            else if (_renderer != null && _mesh != null)
            {
                _renderer.Mesh = _mesh;
            }
        }

        // BepuPhysics Statik Zemin Çarpışması
        _rigidBody = Entity.GetComponent<RigidBody3D>();
        if (_rigidBody == null)
        {
            _rigidBody = new RigidBody3D(BodyType3D.Static, ColliderShape3D.Box, 0f)
            {
                BoxSize = new Vector3(SizeX, Math.Max(0.5f, MaxHeight), SizeZ)
            };
            Entity.AddComponent(_rigidBody);
        }
    }

    /// <summary>
    /// Verilen dünya koordinatlarındaki (X, Z) arazi yüksekliğini (Y) analitik olarak örnekler.
    /// Karakter denetleyicileri ve fizik raycast işlemleri için sıfır maliyetli zemin algılama sunar.
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        float localX = worldX;
        float localZ = worldZ;

        var t3d = Entity?.GetComponent<Transform3D>();
        if (t3d != null)
        {
            localX -= t3d.WorldPosition.X;
            localZ -= t3d.WorldPosition.Z;
        }

        float halfX = SizeX * 0.5f;
        float halfZ = SizeZ * 0.5f;

        if (localX < -halfX || localX > halfX || localZ < -halfZ || localZ > halfZ)
        {
            return 0f;
        }

        float baseHeight = EvaluateNoise(localX, localZ);
        float baseY = t3d?.WorldPosition.Y ?? 0f;
        return baseY + baseHeight;
    }

    private float EvaluateNoise(float x, float z)
    {
        float fx = (x + Seed * 17.13f) * Frequency;
        float fz = (z + Seed * 31.79f) * Frequency;

        // Çok katmanlı (Octave) tepelik/vadi harmonikleri
        float h1 = MathF.Sin(fx) * MathF.Cos(fz);
        float h2 = MathF.Sin(fx * 2.1f + 1.2f) * MathF.Cos(fz * 1.9f + 0.8f) * 0.5f;
        float h3 = MathF.Sin(fx * 4.3f) * MathF.Sin(fz * 4.1f) * 0.25f;

        float combined = (h1 + h2 + h3) / 1.75f; // [-1, 1]
        float normalized = (combined + 1.0f) * 0.5f; // [0, 1]

        return normalized * MaxHeight;
    }

    private Mesh3D GenerateTerrainMesh(GraphicsDevice graphicsDevice)
    {
        int vertsX = Math.Clamp(ResolutionX, 4, 128) + 1;
        int vertsZ = Math.Clamp(ResolutionZ, 4, 128) + 1;
        int totalVerts = vertsX * vertsZ;
        int totalIndices = (vertsX - 1) * (vertsZ - 1) * 6;

        var vertices = new VertexPositionNormalTexture[totalVerts];
        var indices = new int[totalIndices];

        float halfX = SizeX * 0.5f;
        float halfZ = SizeZ * 0.5f;
        float stepX = SizeX / (vertsX - 1);
        float stepZ = SizeZ / (vertsZ - 1);

        // 1. Köşe Noktaları ve UV Koordinatları
        for (int z = 0; z < vertsZ; z++)
        {
            for (int x = 0; x < vertsX; x++)
            {
                int index = z * vertsX + x;
                float px = -halfX + x * stepX;
                float pz = -halfZ + z * stepZ;
                float py = EvaluateNoise(px, pz);

                float u = (float)x / (vertsX - 1) * TextureTiling;
                float v = (float)z / (vertsZ - 1) * TextureTiling;

                vertices[index] = new VertexPositionNormalTexture(new Vector3(px, py, pz), Vector3.Up, new Vector2(u, v));
            }
        }

        // 2. İndeksler (Triangle Grid)
        int idx = 0;
        for (int z = 0; z < vertsZ - 1; z++)
        {
            for (int x = 0; x < vertsX - 1; x++)
            {
                int topLeft = z * vertsX + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * vertsX + x;
                int bottomRight = bottomLeft + 1;

                indices[idx++] = topLeft;
                indices[idx++] = bottomLeft;
                indices[idx++] = topRight;

                indices[idx++] = topRight;
                indices[idx++] = bottomLeft;
                indices[idx++] = bottomRight;
            }
        }

        // 3. Yumuşak Yüzey Normalleri (Smooth Normal Calculation)
        var normals = new Vector3[totalVerts];
        for (int i = 0; i < totalIndices; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 v0 = vertices[i0].Position;
            Vector3 v1 = vertices[i1].Position;
            Vector3 v2 = vertices[i2].Position;

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
            if (normal.LengthSquared() > 0.0001f)
            {
                normal.Normalize();
                normals[i0] += normal;
                normals[i1] += normal;
                normals[i2] += normal;
            }
        }

        for (int i = 0; i < totalVerts; i++)
        {
            if (normals[i].LengthSquared() > 0.0001f)
            {
                normals[i].Normalize();
                vertices[i].Normal = normals[i];
            }
        }

        return new Mesh3D(graphicsDevice, "ProceduralTerrain", vertices, indices, PrimitiveType3D.Custom);
    }

    public override void Destroy()
    {
        _mesh?.Dispose();
        _mesh = null;
        base.Destroy();
    }
}
