using LightEngine.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// Çizim işlemlerini koordine eden, SpriteBatchRenderer ve grafik cihazını yöneten somut servis.
/// </summary>
public class RenderManager : IRenderService
{
    public GraphicsDevice GraphicsDevice { get; }
    public SpriteBatchRenderer SpriteRenderer { get; }
    public Camera2D? CurrentCamera { get; set; }

    public RenderManager(GraphicsDevice graphicsDevice, IAssetService assetService)
    {
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        SpriteRenderer = new SpriteBatchRenderer(graphicsDevice, assetService);
    }

    public void Clear(Color color)
    {
        GraphicsDevice.Clear(color);
        SpriteRenderer.ResetStats();
    }

    public void BeginWorld(Camera2D camera)
    {
        CurrentCamera = camera;
        SpriteRenderer.Begin(transformMatrix: camera.ViewMatrix);
    }

    public void BeginScreen()
    {
        SpriteRenderer.Begin(transformMatrix: null);
    }

    public void End()
    {
        SpriteRenderer.End();
    }

    public void Dispose()
    {
        SpriteRenderer.Dispose();
    }
}
