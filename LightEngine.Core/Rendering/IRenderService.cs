using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// Motorun 2D ve ileride eklenecek 3D çizimlerini yöneten render servisi arayüzü.
/// </summary>
public interface IRenderService : IDisposable
{
    GraphicsDevice GraphicsDevice { get; }
    SpriteBatchRenderer SpriteRenderer { get; }
    Camera2D? CurrentCamera { get; set; }

    /// <summary>
    /// Ekranı belirtilen renk ile temizler.
    /// </summary>
    void Clear(Color color);

    /// <summary>
    /// Dünya koordinatlarında, kamera matrisini kullanarak çizim başlatır.
    /// </summary>
    void BeginWorld(Camera2D camera);

    /// <summary>
    /// Ekran (UI, HUD, Debug) koordinatlarında, kamera dönüşümü olmadan çizim başlatır.
    /// </summary>
    void BeginScreen();

    /// <summary>
    /// Aktif çizim partisini sonlandırır.
    /// </summary>
    void End();
}
