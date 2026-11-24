using LightEngine.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// MonoGame SpriteBatch üzerinde sprite, ilkel şekiller (çizgi, dikdörtgen, daire)
/// ve metin çizimlerini yöneten yüksek seviyeli 2D render sınıfı.
/// </summary>
public class SpriteBatchRenderer : IDisposable
{
    private readonly SpriteBatch _spriteBatch;
    private readonly IAssetService _assetService;
    private readonly DebugFont _debugFont;
    private bool _hasBegun;

    /// <summary>
    /// Bu karede (frame) yapılan toplam çizim çağrısı (Draw Call) sayısı.
    /// </summary>
    public int DrawCallCount { get; private set; }

    public SpriteBatch InnerBatch => _spriteBatch;
    public DebugFont DebugFont => _debugFont;

    public SpriteBatchRenderer(GraphicsDevice graphicsDevice, IAssetService assetService)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assetService);

        _spriteBatch = new SpriteBatch(graphicsDevice);
        _assetService = assetService;
        _debugFont = new DebugFont(graphicsDevice);
    }

    /// <summary>
    /// Çizim partisini (batch) başlatır.
    /// </summary>
    public void Begin(Matrix? transformMatrix = null,
                      SpriteSortMode sortMode = SpriteSortMode.Deferred,
                      BlendState? blendState = null,
                      SamplerState? samplerState = null)
    {
        if (_hasBegun)
        {
            throw new InvalidOperationException("[LightEngine] SpriteBatchRenderer zaten Begin durumunda.");
        }

        _spriteBatch.Begin(
            sortMode: sortMode,
            blendState: blendState ?? BlendState.AlphaBlend,
            samplerState: samplerState ?? SamplerState.PointClamp, // Pixel-art ve 2D için net kenarlar
            depthStencilState: DepthStencilState.None,
            rasterizerState: RasterizerState.CullNone,
            effect: null,
            transformMatrix: transformMatrix);

        _hasBegun = true;
    }

    /// <summary>
    /// Çizim partisini sonlandırır ve ekrana gönderir.
    /// </summary>
    public void End()
    {
        if (!_hasBegun) return;

        _spriteBatch.End();
        _hasBegun = false;
    }

    /// <summary>
    /// Her kare başında çizim istatistiklerini sıfırlar.
    /// </summary>
    public void ResetStats()
    {
        DrawCallCount = 0;
    }

    #region Sprite Çizimleri

    public void DrawSprite(Texture2D texture, Vector2 position, Color color)
    {
        _spriteBatch.Draw(texture, position, color);
        DrawCallCount++;
    }

    public void DrawSprite(Texture2D texture,
                           Vector2 position,
                           Rectangle? sourceRectangle,
                           Color color,
                           float rotation,
                           Vector2 origin,
                           Vector2 scale,
                           SpriteEffects effects = SpriteEffects.None,
                           float layerDepth = 0f)
    {
        _spriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        DrawCallCount++;
    }

    #endregion

    #region Şekil Çizimleri

    public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 1f)
    {
        Vector2 delta = end - start;
        float angle = MathF.Atan2(delta.Y, delta.X);
        float length = delta.Length();

        Texture2D whitePixel = _assetService.GetWhitePixel();
        _spriteBatch.Draw(whitePixel,
                          start,
                          null,
                          color,
                          angle,
                          new Vector2(0f, 0.5f),
                          new Vector2(length, thickness),
                          SpriteEffects.None,
                          0f);
        DrawCallCount++;
    }

    public void DrawRectangle(Rectangle rectangle, Color color, float thickness = 1f)
    {
        Vector2 topLeft = new(rectangle.Left, rectangle.Top);
        Vector2 topRight = new(rectangle.Right, rectangle.Top);
        Vector2 bottomLeft = new(rectangle.Left, rectangle.Bottom);
        Vector2 bottomRight = new(rectangle.Right, rectangle.Bottom);

        DrawLine(topLeft, topRight, color, thickness);
        DrawLine(topRight, bottomRight, color, thickness);
        DrawLine(bottomRight, bottomLeft, color, thickness);
        DrawLine(bottomLeft, topLeft, color, thickness);
    }

    public void DrawFilledRectangle(Rectangle rectangle, Color color)
    {
        Texture2D whitePixel = _assetService.GetWhitePixel();
        _spriteBatch.Draw(whitePixel, rectangle, color);
        DrawCallCount++;
    }

    public void DrawCircle(Vector2 center, float radius, Color color, int segments = 24, float thickness = 1f)
    {
        if (segments < 3) segments = 3;

        float angleStep = MathHelper.TwoPi / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector2 currentPoint = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            DrawLine(prevPoint, currentPoint, color, thickness);
            prevPoint = currentPoint;
        }
    }

    public void DrawFilledCircle(Vector2 center, float radius, Color color, int segments = 24)
    {
        Texture2D circleTex = _assetService.CreateCircleTexture((int)radius, Color.White);
        Vector2 origin = new(radius, radius);
        _spriteBatch.Draw(circleTex, center, null, color, 0f, origin, Vector2.One, SpriteEffects.None, 0f);
        DrawCallCount++;
    }

    #endregion

    #region Metin Çizimleri

    public void DrawString(SpriteFont font, string text, Vector2 position, Color color)
    {
        _spriteBatch.DrawString(font, text, position, color);
        DrawCallCount++;
    }

    public void DrawDebugText(string text, Vector2 position, Color color, float scale = 1f)
    {
        _debugFont.DrawText(_spriteBatch, text, position, color, scale);
        DrawCallCount++;
    }

    public void DrawDebugTextWithShadow(string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1f)
    {
        DrawDebugText(text, position + new Vector2(1f, 1f), shadowColor, scale);
        DrawDebugText(text, position, textColor, scale);
    }

    #endregion

    public void Dispose()
    {
        _spriteBatch.Dispose();
        _debugFont.Dispose();
    }
}
