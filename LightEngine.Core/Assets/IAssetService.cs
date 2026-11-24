using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Assets;

/// <summary>
/// Doku (Texture), ses ve yazı tipi (Font) gibi varlıkların önbellekli (cached)
/// yüklenmesini ve yönetilmesini sağlayan servis arayüzü.
/// </summary>
public interface IAssetService : IDisposable
{
    /// <summary>
    /// Diskten doğrudan (PNG, JPG) veya MonoGame Content Pipeline üzerinden doku yükler ve önbelleğe alır.
    /// </summary>
    Texture2D LoadTexture(string path);

    /// <summary>
    /// Şekil çizimleri ve düz renkler için 1x1 beyaz piksel dokusu döndürür.
    /// </summary>
    Texture2D GetWhitePixel();

    /// <summary>
    /// Belirtilen boyut ve renkte prosedürel düz renk dokusu üretir ve önbelleğe alır.
    /// </summary>
    Texture2D CreateSolidTexture(int width, int height, Color color, string? name = null);

    /// <summary>
    /// Prosedürel daire dokusu üretir ve önbelleğe alır.
    /// </summary>
    Texture2D CreateCircleTexture(int radius, Color color, string? name = null);

    /// <summary>
    /// Ses efekti yükler ve önbelleğe alır.
    /// </summary>
    SoundEffect? LoadSound(string path);

    /// <summary>
    /// Yazı tipi (SpriteFont) yükler ve önbelleğe alır.
    /// </summary>
    SpriteFont? LoadFont(string path);

    /// <summary>
    /// MonoGame ContentManager aracılığıyla genel varlık yükler.
    /// </summary>
    T Load<T>(string assetName);

    /// <summary>
    /// Önbelleğe alınmış tüm varlıkları temizler ve kaynakları serbest bırakır.
    /// </summary>
    void UnloadAll();
}
