using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// MonoGame GPU hattı üzerinde Blinn-Phong çoklu ışıklandırma, Frustum Culling,
/// 3D sonsuz ızgara, seçim çerçevesi ve XYZ gizmo çizimi yapan hafif 3D render yöneticisi.
/// Her karede bellek ayırmayı (allocation) önlemek için önceden ayrılmış tamponlar kullanır.
/// </summary>
public class Renderer3D : IRender3DService
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _litEffect;
    private readonly BasicEffect _lineEffect;
    private readonly Dictionary<PrimitiveType3D, Mesh3D> _primitiveCache = new();
    private readonly Dictionary<string, Mesh3D> _modelCache = new(StringComparer.OrdinalIgnoreCase);

    // Allocation azaltma: yeniden kullanılabilir ışık listesi ve çizgi tamponları
    private readonly List<Light3D> _sceneLights = new(8);
    private readonly VertexPositionColor[] _gridVertices;
    private readonly VertexPositionColor[] _skyVertices;
    private readonly VertexPositionColor[] _boxLineBuffer = new VertexPositionColor[24];
    private readonly VertexPositionColor[] _singleLineBuffer = new VertexPositionColor[2];
    private readonly VertexPositionColor[] _gizmoBuffer = new VertexPositionColor[6];

    private readonly RasterizerState _solidRasterizer = new() { CullMode = CullMode.CullCounterClockwiseFace, FillMode = FillMode.Solid };
    private readonly RasterizerState _wireframeRasterizer = new() { CullMode = CullMode.None, FillMode = FillMode.WireFrame };

    public GraphicsDevice GraphicsDevice => _graphicsDevice;
    public QualitySettings3D Quality { get; } = new();
    public CascadedShadowMap3D ShadowMap { get; }
    public PostProcessor3D PostProcessor { get; }
    public int RenderedMeshCount { get; private set; }
    public int CulledMeshCount { get; private set; }
    public int RenderedTriangleCount { get; private set; }

    public Renderer3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        ShadowMap = new CascadedShadowMap3D(_graphicsDevice);
        PostProcessor = new PostProcessor3D(_graphicsDevice);

        _litEffect = new BasicEffect(_graphicsDevice)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = false
        };

        _lineEffect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = false
        };

        _gridVertices = BuildGridVertices(24, 1f);
        _skyVertices = BuildSkyboxVertices(250f);
    }


    public void ApplyQualityPreset(QualityPreset preset)
    {
        Quality.ApplyPreset(preset);
        ShadowMap.ConfigureForQuality(preset);
        PostProcessor.Settings.ApplyPreset(preset);
    }

    public void BakeStaticLightmaps(IScene scene, int resolution = 128)
    {
        ArgumentNullException.ThrowIfNull(scene);

        foreach (var entity in scene.Entities)
        {
            if (!entity.IsActive || entity.IsDestroyed) continue;
            var mr = entity.GetComponent<MeshRenderer3D>();
            if (mr == null || mr.Mesh == null || !mr.ReceiveShadows) continue;

            if (mr.Mesh.PrimitiveType == PrimitiveType3D.Plane)
            {
                var bounds = mr.GetWorldBoundingBox();
                mr.Material.LightmapTexture?.Dispose();
                mr.Material.LightmapTexture = LightmapBaker3D.BakePlanarLightmap(_graphicsDevice, scene, bounds, resolution, Quality.AmbientLightColor);
            }
        }
    }

    public Mesh3D GetPrimitiveMesh(PrimitiveType3D type)
    {
        if (!_primitiveCache.TryGetValue(type, out var mesh))
        {
            mesh = ProceduralMeshFactory.CreatePrimitive(_graphicsDevice, type);
            _primitiveCache[type] = mesh;
        }
        return mesh;
    }

    public Mesh3D LoadModel(string filePath)
    {
        if (_modelCache.TryGetValue(filePath, out var cached))
        {
            return cached;
        }

        var mesh = Model3DLoader.Load(_graphicsDevice, filePath);
        _modelCache[filePath] = mesh;
        return mesh;
    }

    public void RenderScene(IScene scene, Camera3D camera, bool drawEditorGrid = false, IEntity? selectedEntity = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);

        RenderedMeshCount = 0;
        CulledMeshCount = 0;
        RenderedTriangleCount = 0;

        // 3D derinlik tamponu ve karıştırma durumlarını hazırla
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        Matrix view = camera.ViewMatrix;
        Matrix proj = camera.ProjectionMatrix;
        BoundingFrustum frustum = camera.Frustum;

        // 0. Prosedürel Atmosferik Gökyüzü / Ufuk Gradyanı (Half-Life 2 tarzı atmosfer)
        if (Quality.SkyboxEnabled)
        {
            DrawProceduralSky(camera);
        }

        // 1. Editör 3D Izgarasını ve Dünya Eksenlerini çiz
        if (drawEditorGrid)
        {
            DrawEditorGrid(view, proj);
        }

        // 2. Sahnedeki tüm aktif 3D ışıkları topla
        _sceneLights.Clear();
        var entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (!e.IsActive || e.IsDestroyed) continue;
            var light = e.GetComponent<Light3D>();
            if (light != null && light.IsEnabled)
            {
                _sceneLights.Add(light);
            }
        }

        ConfigureEffectLighting(view, proj);

        // 3. Sahnedeki tüm MeshRenderer3D bileşenlerini Frustum Culling ile çiz
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.IsActive || entity.IsDestroyed) continue;

            var meshRenderer = entity.GetComponent<MeshRenderer3D>();
            if (meshRenderer == null || !meshRenderer.IsEnabled || meshRenderer.Mesh == null) continue;

            // Frustum Culling kontrolü
            BoundingBox worldBounds = meshRenderer.GetWorldBoundingBox();
            if (frustum.Contains(worldBounds) == ContainmentType.Disjoint)
            {
                CulledMeshCount++;
                continue;
            }

            var t3d = entity.GetComponent<Transform3D>();
            Matrix world = t3d != null ? t3d.WorldMatrix : Matrix.Identity;

            DrawMesh(meshRenderer.Mesh, meshRenderer.Material, world);
            RenderedMeshCount++;
            RenderedTriangleCount += meshRenderer.Mesh.PrimitiveCount;
        }

        // 3.5. 3D Parçacık Yayıcıları (ParticleEmitter3D) Çiz
        for (int i = 0; i < scene.Entities.Count; i++)
        {
            var entity = scene.Entities[i];
            if (!entity.IsActive || entity.IsDestroyed) continue;
            var emitter = entity.GetComponent<LightEngine.Core.Particles.ParticleEmitter3D>();
            if (emitter != null && emitter.IsEnabled)
            {
                emitter.Draw3D(_graphicsDevice, camera);
            }
        }

        // 4. Seçili 3D varlık varsa altın tel kafes sınır kutusu ve 3D XYZ gizmo çiz
        if (selectedEntity != null && !selectedEntity.IsDestroyed)
        {
            var t3d = selectedEntity.GetComponent<Transform3D>();
            var mr = selectedEntity.GetComponent<MeshRenderer3D>();

            if (mr != null && mr.Mesh != null)
            {
                DrawBoundingBox(camera, mr.GetWorldBoundingBox(), Color.Gold);
            }

            if (t3d != null)
            {
                DrawAxisGizmo3D(view, proj, t3d.WorldPosition, 1.5f);
            }
        }

        // 2D SpriteBatch ile çakışmaması için rasterizer durumunu sıfırla
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
    }

    private void ConfigureEffectLighting(Matrix view, Matrix proj)
    {
        _litEffect.View = view;
        _litEffect.Projection = proj;
        _litEffect.PreferPerPixelLighting = Quality.PreferPerPixelLighting;
        _litEffect.AmbientLightColor = Quality.AmbientLightColor.ToVector3();

        _litEffect.FogEnabled = Quality.FogEnabled;
        _litEffect.FogStart = Quality.FogStart;
        _litEffect.FogEnd = Quality.FogEnd;
        _litEffect.FogColor = Quality.FogColor.ToVector3();

        // Önce ışıkları sıfırla
        _litEffect.DirectionalLight0.Enabled = false;
        _litEffect.DirectionalLight1.Enabled = false;
        _litEffect.DirectionalLight2.Enabled = false;

        if (_sceneLights.Count == 0)
        {
            // Sahne ışığı yoksa varsayılan HL2 tarzı 3 noktalı stüdyo ışığı uygula
            _litEffect.DirectionalLight0.Enabled = true;
            _litEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -0.85f, -0.45f));
            _litEffect.DirectionalLight0.DiffuseColor = new Vector3(0.95f, 0.92f, 0.86f);
            _litEffect.DirectionalLight0.SpecularColor = new Vector3(0.75f, 0.75f, 0.75f);

            _litEffect.DirectionalLight1.Enabled = true;
            _litEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0.6f, -0.3f, 0.7f));
            _litEffect.DirectionalLight1.DiffuseColor = new Vector3(0.28f, 0.36f, 0.50f);
            _litEffect.DirectionalLight1.SpecularColor = new Vector3(0.15f, 0.18f, 0.25f);
            return;
        }

        int count = Math.Min(_sceneLights.Count, 3);
        for (int i = 0; i < count; i++)
        {
            var src = _sceneLights[i];
            var dst = i switch
            {
                0 => _litEffect.DirectionalLight0,
                1 => _litEffect.DirectionalLight1,
                _ => _litEffect.DirectionalLight2
            };

            dst.Enabled = true;
            dst.Direction = src.Direction;
            dst.DiffuseColor = src.Color.ToVector3() * src.Intensity;
            dst.SpecularColor = src.SpecularColor.ToVector3() * src.Intensity;
        }
    }

    private void DrawMesh(Mesh3D mesh, Material3D material, Matrix world)
    {
        _litEffect.World = world;
        _litEffect.LightingEnabled = material.EnableLighting && material.LightmapTexture == null;
        _litEffect.DiffuseColor = material.DiffuseColor.ToVector3();

        // Hafif PBR Roughness/Metallic -> Blinn-Phong parlamasına haritalama
        float smoothness = 1f - Math.Clamp(material.Roughness, 0.05f, 0.98f);
        float derivedPower = MathHelper.Lerp(material.SpecularPower, 8f + smoothness * smoothness * 120f, 0.5f);
        Vector3 specTint = Vector3.Lerp(material.SpecularColor.ToVector3(), material.DiffuseColor.ToVector3(), material.Metallic);

        _litEffect.SpecularColor = specTint * (0.25f + smoothness * 0.75f);
        _litEffect.SpecularPower = derivedPower;
        _litEffect.EmissiveColor = material.EmissiveColor.ToVector3();
        _litEffect.Alpha = material.Alpha;

        if (material.LightmapTexture != null)
        {
            _litEffect.TextureEnabled = true;
            _litEffect.Texture = material.LightmapTexture;
        }
        else if (material.DiffuseTexture != null)
        {
            _litEffect.TextureEnabled = true;
            _litEffect.Texture = material.DiffuseTexture;
        }
        else
        {
            _litEffect.TextureEnabled = false;
        }

        _graphicsDevice.RasterizerState = material.Wireframe ? _wireframeRasterizer : _solidRasterizer;
        _graphicsDevice.BlendState = material.Alpha < 0.99f ? BlendState.AlphaBlend : BlendState.Opaque;

        _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
        _graphicsDevice.Indices = mesh.IndexBuffer;

        foreach (var pass in _litEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
        }
    }

    private void DrawEditorGrid(Matrix view, Matrix proj)
    {
        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = view;
        _lineEffect.Projection = proj;

        _graphicsDevice.RasterizerState = _wireframeRasterizer;
        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _gridVertices, 0, _gridVertices.Length / 2);
        }
    }

    private void DrawAxisGizmo3D(Matrix view, Matrix proj, Vector3 origin, float length)
    {
        // Gizmo her zaman nesnenin üstünde görünsün diye derinlik testini kapat
        var prevDepth = _graphicsDevice.DepthStencilState;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;

        _gizmoBuffer[0] = new VertexPositionColor(origin, Color.Crimson);
        _gizmoBuffer[1] = new VertexPositionColor(origin + Vector3.UnitX * length, Color.Crimson);

        _gizmoBuffer[2] = new VertexPositionColor(origin, Color.LimeGreen);
        _gizmoBuffer[3] = new VertexPositionColor(origin + Vector3.UnitY * length, Color.LimeGreen);

        _gizmoBuffer[4] = new VertexPositionColor(origin, Color.DodgerBlue);
        _gizmoBuffer[5] = new VertexPositionColor(origin + Vector3.UnitZ * length, Color.DodgerBlue);

        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = view;
        _lineEffect.Projection = proj;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _gizmoBuffer, 0, 3);
        }

        _graphicsDevice.DepthStencilState = prevDepth;
    }

    public void DrawLine3D(Camera3D camera, Vector3 start, Vector3 end, Color color)
    {
        _singleLineBuffer[0] = new VertexPositionColor(start, color);
        _singleLineBuffer[1] = new VertexPositionColor(end, color);

        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = camera.ViewMatrix;
        _lineEffect.Projection = camera.ProjectionMatrix;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _singleLineBuffer, 0, 1);
        }
    }

    public void DrawBoundingBox(Camera3D camera, BoundingBox box, Color color)
    {
        Vector3 min = box.Min;
        Vector3 max = box.Max;

        Vector3 c0 = new(min.X, min.Y, min.Z);
        Vector3 c1 = new(max.X, min.Y, min.Z);
        Vector3 c2 = new(max.X, min.Y, max.Z);
        Vector3 c3 = new(min.X, min.Y, max.Z);
        Vector3 c4 = new(min.X, max.Y, min.Z);
        Vector3 c5 = new(max.X, max.Y, min.Z);
        Vector3 c6 = new(max.X, max.Y, max.Z);
        Vector3 c7 = new(min.X, max.Y, max.Z);

        // Alt yüz
        SetEdge(0, c0, c1, color); SetEdge(1, c1, c2, color); SetEdge(2, c2, c3, color); SetEdge(3, c3, c0, color);
        // Üst yüz
        SetEdge(4, c4, c5, color); SetEdge(5, c5, c6, color); SetEdge(6, c6, c7, color); SetEdge(7, c7, c4, color);
        // Dikey kenarlar
        SetEdge(8, c0, c4, color); SetEdge(9, c1, c5, color); SetEdge(10, c2, c6, color); SetEdge(11, c3, c7, color);

        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = camera.ViewMatrix;
        _lineEffect.Projection = camera.ProjectionMatrix;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _boxLineBuffer, 0, 12);
        }
    }

    private void SetEdge(int edgeIdx, Vector3 a, Vector3 b, Color color)
    {
        _boxLineBuffer[edgeIdx * 2] = new VertexPositionColor(a, color);
        _boxLineBuffer[edgeIdx * 2 + 1] = new VertexPositionColor(b, color);
    }

    private static VertexPositionColor[] BuildGridVertices(int halfLines, float spacing)
    {
        var list = new List<VertexPositionColor>();
        float extent = halfLines * spacing;
        Color minor = new(52, 58, 72);
        Color major = new(78, 86, 106);

        for (int i = -halfLines; i <= halfLines; i++)
        {
            float coord = i * spacing;
            if (i == 0)
            {
                // X ekseni (Kırmızı) ve Z ekseni (Mavi)
                list.Add(new VertexPositionColor(new(-extent, 0, 0), new Color(210, 65, 65)));
                list.Add(new VertexPositionColor(new(extent, 0, 0), new Color(210, 65, 65)));
                list.Add(new VertexPositionColor(new(0, 0, -extent), new Color(65, 135, 225)));
                list.Add(new VertexPositionColor(new(0, 0, extent), new Color(65, 135, 225)));
            }
            else
            {
                Color c = (i % 5 == 0) ? major : minor;
                list.Add(new VertexPositionColor(new(coord, 0, -extent), c));
                list.Add(new VertexPositionColor(new(coord, 0, extent), c));
                list.Add(new VertexPositionColor(new(-extent, 0, coord), c));
                list.Add(new VertexPositionColor(new(extent, 0, coord), c));
            }
        }

        return list.ToArray();
    }

    private void DrawProceduralSky(Camera3D camera)
    {
        _lineEffect.World = Matrix.CreateTranslation(camera.Position);
        _lineEffect.View = camera.ViewMatrix;
        _lineEffect.Projection = camera.ProjectionMatrix;

        var prevDepth = _graphicsDevice.DepthStencilState;
        var prevRaster = _graphicsDevice.RasterizerState;

        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _skyVertices, 0, 12);
        }

        _graphicsDevice.DepthStencilState = prevDepth;
        _graphicsDevice.RasterizerState = prevRaster;
    }

    private VertexPositionColor[] BuildSkyboxVertices(float size)
    {
        float s = size;
        var zenith = Quality.SkyZenithColor;
        var horizon = Quality.SkyHorizonColor;
        var ground = Quality.SkyGroundColor;

        Vector3 p0 = new(-s, -s, -s);
        Vector3 p1 = new( s, -s, -s);
        Vector3 p2 = new( s, -s,  s);
        Vector3 p3 = new(-s, -s,  s);
        Vector3 p4 = new(-s,  s, -s);
        Vector3 p5 = new( s,  s, -s);
        Vector3 p6 = new( s,  s,  s);
        Vector3 p7 = new(-s,  s,  s);

        var verts = new VertexPositionColor[36];
        int i = 0;

        void AddTri(Vector3 a, Color ca, Vector3 b, Color cb, Vector3 c, Color cc)
        {
            verts[i++] = new VertexPositionColor(a, ca);
            verts[i++] = new VertexPositionColor(b, cb);
            verts[i++] = new VertexPositionColor(c, cc);
        }

        // Üst Yüz (Zenith Gökyüzü)
        AddTri(p4, zenith, p5, zenith, p6, zenith);
        AddTri(p4, zenith, p6, zenith, p7, zenith);

        // Alt Yüz (Zemin / Ufuk Tabanı)
        AddTri(p0, ground, p2, ground, p1, ground);
        AddTri(p0, ground, p3, ground, p2, ground);

        // Ön Yüz (-Z)
        AddTri(p0, horizon, p5, zenith, p1, horizon);
        AddTri(p0, horizon, p4, zenith, p5, zenith);

        // Arka Yüz (+Z)
        AddTri(p2, horizon, p7, zenith, p3, horizon);
        AddTri(p2, horizon, p6, zenith, p7, zenith);

        // Sol Yüz (-X)
        AddTri(p3, horizon, p4, zenith, p0, horizon);
        AddTri(p3, horizon, p7, zenith, p4, zenith);

        // Sağ Yüz (+X)
        AddTri(p1, horizon, p6, zenith, p2, horizon);
        AddTri(p1, horizon, p5, zenith, p6, zenith);

        return verts;
    }

    public void Dispose()
    {
        foreach (var m in _primitiveCache.Values) m.Dispose();
        _primitiveCache.Clear();

        foreach (var m in _modelCache.Values) m.Dispose();
        _modelCache.Clear();

        ShadowMap.Dispose();
        PostProcessor.Dispose();
        _litEffect.Dispose();
        _lineEffect.Dispose();
        _solidRasterizer.Dispose();
        _wireframeRasterizer.Dispose();
    }
}
