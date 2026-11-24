using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Tek bir Cascaded Shadow Map (CSM) kademesinin derinlik haritasını ve ışık uzayı matrislerini tutar.
/// </summary>
public class ShadowCascadeSlice : IDisposable
{
    public float SplitNear { get; set; }
    public float SplitFar { get; set; }
    public Matrix LightView { get; set; } = Matrix.Identity;
    public Matrix LightProjection { get; set; } = Matrix.Identity;
    public Matrix LightViewProjection => LightView * LightProjection;
    public RenderTarget2D? DepthMap { get; set; }

    public void Dispose()
    {
        DepthMap?.Dispose();
        DepthMap = null;
    }
}

/// <summary>
/// Entegre ekran kartlarında dahi yüksek performanslı çalışan hafif Cascaded Shadow Map (CSM) yöneticisi.
/// Kalite ayarına göre 1, 2 veya 3 kademe (512 / 1024 / 2048 px) kullanır ve görüş konisini pratik logaritmik bölme ile ayırır.
/// </summary>
public class CascadedShadowMap3D : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _shadowDepthEffect;
    private ShadowCascadeSlice[] _cascades = Array.Empty<ShadowCascadeSlice>();

    public bool IsEnabled { get; set; } = true;
    public int CascadeCount { get; private set; } = 2;
    public int MapResolution { get; private set; } = 1024;
    public float ShadowDistance { get; set; } = 60f;
    public float SplitLambda { get; set; } = 0.65f;
    public float DepthBias { get; set; } = 0.0015f;
    public IReadOnlyList<ShadowCascadeSlice> Cascades => _cascades;

    public CascadedShadowMap3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _shadowDepthEffect = new BasicEffect(_graphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = false
        };

        ConfigureForQuality(QualityPreset.Medium);
    }

    /// <summary>
    /// Kalite profiline göre cascade sayısını ve gölge haritası çözünürlüğünü ayarlar.
    /// </summary>
    public void ConfigureForQuality(QualityPreset preset)
    {
        switch (preset)
        {
            case QualityPreset.Low:
                IsEnabled = false;
                AllocateCascades(1, 512);
                ShadowDistance = 35f;
                break;
            case QualityPreset.Medium:
                IsEnabled = true;
                AllocateCascades(2, 1024);
                ShadowDistance = 60f;
                break;
            case QualityPreset.High:
                IsEnabled = true;
                AllocateCascades(3, 1024);
                ShadowDistance = 95f;
                break;
        }
    }

    private void AllocateCascades(int count, int resolution)
    {
        if (CascadeCount == count && MapResolution == resolution && _cascades.Length == count) return;

        foreach (var c in _cascades) c.Dispose();

        CascadeCount = count;
        MapResolution = resolution;
        _cascades = new ShadowCascadeSlice[count];

        for (int i = 0; i < count; i++)
        {
            _cascades[i] = new ShadowCascadeSlice
            {
                DepthMap = new RenderTarget2D(
                    _graphicsDevice,
                    resolution,
                    resolution,
                    false,
                    SurfaceFormat.Single,
                    DepthFormat.Depth24,
                    0,
                    RenderTargetUsage.DiscardContents)
            };
        }
    }

    /// <summary>
    /// Kamera görüş konisine ve ana yönlü ışığa göre her cascade kademesinin ışık uzayı (Light View-Projection) matrislerini hesaplar.
    /// </summary>
    public void UpdateCascades(Camera3D camera, Vector3 lightDirection)
    {
        if (!IsEnabled || _cascades.Length == 0) return;

        Vector3 dir = lightDirection.LengthSquared() > 0.0001f
            ? Vector3.Normalize(lightDirection)
            : Vector3.Normalize(new Vector3(-0.5f, -0.85f, -0.45f));

        float near = camera.NearPlane;
        float far = Math.Min(camera.FarPlane, ShadowDistance);
        float range = far - near;
        float ratio = far / near;

        float prevSplit = near;
        for (int i = 0; i < _cascades.Length; i++)
        {
            float p = (i + 1) / (float)_cascades.Length;
            float logSplit = near * MathF.Pow(ratio, p);
            float uniformSplit = near + range * p;
            float splitDist = MathHelper.Lerp(uniformSplit, logSplit, SplitLambda);

            var slice = _cascades[i];
            slice.SplitNear = prevSplit;
            slice.SplitFar = splitDist;

            // Kademe merkezini kamera boyunca hesapla
            float midDist = (prevSplit + splitDist) * 0.5f;
            float radius = (splitDist - prevSplit) * 0.75f + 4f;
            Vector3 center = camera.Position + camera.Forward * midDist;

            // Piksel titremesini (shadow swimming) azaltmak için dünya ızgarasına hizala
            float texelSize = (radius * 2f) / MapResolution;
            center.X = MathF.Floor(center.X / texelSize) * texelSize;
            center.Y = MathF.Floor(center.Y / texelSize) * texelSize;
            center.Z = MathF.Floor(center.Z / texelSize) * texelSize;

            Vector3 up = MathF.Abs(Vector3.Dot(dir, Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
            slice.LightView = Matrix.CreateLookAt(center - dir * (radius * 2f), center, up);
            slice.LightProjection = Matrix.CreateOrthographic(radius * 2f, radius * 2f, 0.5f, radius * 4.5f);

            prevSplit = splitDist;
        }
    }

    /// <summary>
    /// Gölge düşüren (CastShadows=true) 3D nesneleri cascade derinlik haritalarına çizer.
    /// </summary>
    public void RenderShadowMaps(IScene scene, Camera3D camera, Vector3 lightDirection)
    {
        if (!IsEnabled || _cascades.Length == 0) return;

        UpdateCascades(camera, lightDirection);

        var prevTargets = _graphicsDevice.GetRenderTargets();
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;

        var entities = scene.Entities;
        for (int c = 0; c < _cascades.Length; c++)
        {
            var slice = _cascades[c];
            if (slice.DepthMap == null) continue;

            _graphicsDevice.SetRenderTarget(slice.DepthMap);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);

            _shadowDepthEffect.View = slice.LightView;
            _shadowDepthEffect.Projection = slice.LightProjection;

            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!e.IsActive || e.IsDestroyed) continue;

                var mr = e.GetComponent<MeshRenderer3D>();
                if (mr == null || !mr.IsEnabled || !mr.CastShadows || mr.Mesh == null) continue;

                var t3d = e.GetComponent<Transform3D>();
                _shadowDepthEffect.World = t3d != null ? t3d.WorldMatrix : Matrix.Identity;

                _graphicsDevice.SetVertexBuffer(mr.Mesh.VertexBuffer);
                _graphicsDevice.Indices = mr.Mesh.IndexBuffer;

                foreach (var pass in _shadowDepthEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mr.Mesh.PrimitiveCount);
                }
            }
        }

        _graphicsDevice.SetRenderTargets(prevTargets);
    }

    public void Dispose()
    {
        foreach (var c in _cascades) c.Dispose();
        _cascades = Array.Empty<ShadowCascadeSlice>();
        _shadowDepthEffect.Dispose();
    }
}
