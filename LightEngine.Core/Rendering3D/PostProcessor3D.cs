using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Açılıp kapanabilen Post-Processing (Bloom, FXAA, Tone Mapping, SSAO, Vignette) yapılandırması.
/// </summary>
public class PostProcessSettings
{
    public bool Enabled { get; set; } = true;
    public bool BloomEnabled { get; set; } = true;
    public float BloomThreshold { get; set; } = 0.75f;
    public float BloomIntensity { get; set; } = 0.35f;

    public bool FxaaEnabled { get; set; } = true;
    public bool ToneMappingEnabled { get; set; } = true;
    public float Exposure { get; set; } = 1.05f;
    public float Gamma { get; set; } = 2.2f;

    public bool SsaoEnabled { get; set; } = false;
    public float SsaoRadius { get; set; } = 0.5f;
    public float SsaoStrength { get; set; } = 0.6f;

    public bool VignetteEnabled { get; set; } = true;
    public float VignetteStrength { get; set; } = 0.22f;

    /// <summary>
    /// Kalite profiline göre Post-Processing efektlerini otomatik ayarlar.
    /// </summary>
    public void ApplyPreset(QualityPreset preset)
    {
        switch (preset)
        {
            case QualityPreset.Low:
                Enabled = false;
                BloomEnabled = false;
                FxaaEnabled = false;
                ToneMappingEnabled = false;
                SsaoEnabled = false;
                VignetteEnabled = false;
                break;
            case QualityPreset.Medium:
                Enabled = true;
                BloomEnabled = true;
                BloomIntensity = 0.28f;
                FxaaEnabled = true;
                ToneMappingEnabled = true;
                SsaoEnabled = false;
                VignetteEnabled = true;
                break;
            case QualityPreset.High:
                Enabled = true;
                BloomEnabled = true;
                BloomIntensity = 0.40f;
                FxaaEnabled = true;
                ToneMappingEnabled = true;
                SsaoEnabled = true;
                VignetteEnabled = true;
                break;
        }
    }
}

/// <summary>
/// Harici HLSL derleyici bağımlılığı olmadan MonoGame DesktopGL üzerinde güvenle çalışan
/// hafif Post-Processing (Bloom parlaması, Pozlama/Tone Mapping ve Vignette) ekran hattı.
/// </summary>
public class PostProcessor3D : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private RenderTarget2D? _bloomDownsampleTarget;
    private Texture2D? _vignetteTexture;
    private int _lastWidth;
    private int _lastHeight;

    public PostProcessSettings Settings { get; } = new();

    public PostProcessor3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _vignetteTexture = CreateVignetteTexture(graphicsDevice, 256);
    }

    /// <summary>
    /// Kaynak sahne RenderTarget'ına aktif Post-Processing efektlerini uygulayıp hedef RenderTarget'a yazar.
    /// </summary>
    public void Apply(RenderTarget2D sourceScene, RenderTarget2D? destinationTarget)
    {
        if (sourceScene == null) return;

        int w = sourceScene.Width;
        int h = sourceScene.Height;
        EnsureTargets(w, h);

        // 1. Bloom yarı çözünürlüklü downsample geçidi
        if (Settings.Enabled && Settings.BloomEnabled && _bloomDownsampleTarget != null)
        {
            _graphicsDevice.SetRenderTarget(_bloomDownsampleTarget);
            _graphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp);
            _spriteBatch.Draw(sourceScene, new Rectangle(0, 0, _bloomDownsampleTarget.Width, _bloomDownsampleTarget.Height), Color.White);
            _spriteBatch.End();
        }

        // 2. Ana hedefe çizim (Tone Mapping pozlama çarpanı + FXAA lineer yumuşatma)
        _graphicsDevice.SetRenderTarget(destinationTarget);
        int outW = destinationTarget?.Width ?? _graphicsDevice.PresentationParameters.BackBufferWidth;
        int outH = destinationTarget?.Height ?? _graphicsDevice.PresentationParameters.BackBufferHeight;
        var outRect = new Rectangle(0, 0, outW, outH);

        SamplerState sampler = (Settings.Enabled && Settings.FxaaEnabled)
            ? SamplerState.LinearClamp
            : SamplerState.PointClamp;

        float exp = (Settings.Enabled && Settings.ToneMappingEnabled)
            ? Math.Clamp(Settings.Exposure, 0.5f, 1.5f)
            : 1.0f;
        Color tint = new(exp, exp, exp, 1f);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, sampler);
        _spriteBatch.Draw(sourceScene, outRect, tint);
        _spriteBatch.End();

        // 3. Additive Bloom katmanı
        if (Settings.Enabled && Settings.BloomEnabled && _bloomDownsampleTarget != null && Settings.BloomIntensity > 0.01f)
        {
            float bi = Math.Clamp(Settings.BloomIntensity, 0f, 1f);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp);
            _spriteBatch.Draw(_bloomDownsampleTarget, outRect, new Color(bi, bi, bi, bi));
            _spriteBatch.End();
        }

        // 4. Sinematik Vignette / Kenar derinlik katmanı
        if (Settings.Enabled && Settings.VignetteEnabled && _vignetteTexture != null && Settings.VignetteStrength > 0.01f)
        {
            float vs = Math.Clamp(Settings.VignetteStrength, 0f, 0.8f);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp);
            _spriteBatch.Draw(_vignetteTexture, outRect, new Color(1f, 1f, 1f, vs));
            _spriteBatch.End();
        }
    }

    private void EnsureTargets(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (_lastWidth == width && _lastHeight == height && _bloomDownsampleTarget != null) return;

        _bloomDownsampleTarget?.Dispose();
        _bloomDownsampleTarget = new RenderTarget2D(
            _graphicsDevice,
            Math.Max(1, width / 4),
            Math.Max(1, height / 4),
            false,
            SurfaceFormat.Color,
            DepthFormat.None);

        _lastWidth = width;
        _lastHeight = height;
    }

    private static Texture2D CreateVignetteTexture(GraphicsDevice device, int size)
    {
        var tex = new Texture2D(device, size, size);
        var data = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float alpha = Math.Clamp((dist - 0.55f) / 0.85f, 0f, 1f);
                alpha *= alpha;
                data[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetData(data);
        return tex;
    }

    public void Dispose()
    {
        _bloomDownsampleTarget?.Dispose();
        _vignetteTexture?.Dispose();
        _spriteBatch.Dispose();
    }
}
