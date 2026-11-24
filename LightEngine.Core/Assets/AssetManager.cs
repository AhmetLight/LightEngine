using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Assets;

/// <summary>
/// Varlıkları (Doku, Ses, Yazı tipi) önbellekleyen ve diskten ya da Content Pipeline'dan yükleyen somut servis sınıfı.
/// </summary>
public class AssetManager : IAssetService
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentManager _content;

    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteFont> _fonts = new(StringComparer.OrdinalIgnoreCase);

    private Texture2D? _whitePixel;

    public AssetManager(GraphicsDevice graphicsDevice, ContentManager content)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public Texture2D GetWhitePixel()
    {
        if (_whitePixel == null || _whitePixel.IsDisposed)
        {
            _whitePixel = new Texture2D(_graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
        }
        return _whitePixel;
    }

    public Texture2D LoadTexture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_textures.TryGetValue(path, out var cached) && !cached.IsDisposed)
        {
            return cached;
        }

        Texture2D? texture = null;

        // 1. Önce doğrudan diskteki dosya yolunu dene (PNG, JPG, BMP)
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                texture = Texture2D.FromStream(_graphicsDevice, stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LightEngine] Diskten doku yükleme hatası ({path}): {ex.Message}");
            }
        }

        // 2. Disk yolunda yoksa ContentManager üzerinden dene
        if (texture == null)
        {
            try
            {
                texture = _content.Load<Texture2D>(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LightEngine] ContentManager doku yükleme uyarısı ({path}): {ex.Message}");
            }
        }

        // 3. Bulunamazsa çökmesini önlemek için pembe uyarı dokusu oluştur (Missing texture)
        if (texture == null)
        {
            texture = CreateSolidTexture(32, 32, Color.Magenta, $"fallback_{path}");
        }

        _textures[path] = texture;
        return texture;
    }

    public Texture2D CreateSolidTexture(int width, int height, Color color, string? name = null)
    {
        string key = name ?? $"solid_{width}_{height}_{color.PackedValue}";
        if (_textures.TryGetValue(key, out var cached) && !cached.IsDisposed)
        {
            return cached;
        }

        var texture = new Texture2D(_graphicsDevice, width, height);
        var data = new Color[width * height];
        Array.Fill(data, color);
        texture.SetData(data);

        _textures[key] = texture;
        return texture;
    }

    public Texture2D CreateCircleTexture(int radius, Color color, string? name = null)
    {
        string key = name ?? $"circle_{radius}_{color.PackedValue}";
        if (_textures.TryGetValue(key, out var cached) && !cached.IsDisposed)
        {
            return cached;
        }

        int diameter = radius * 2;
        var texture = new Texture2D(_graphicsDevice, diameter, diameter);
        var data = new Color[diameter * diameter];
        float radiusSq = radius * radius;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float distSq = (dx * dx) + (dy * dy);

                if (distSq <= radiusSq)
                {
                    data[(y * diameter) + x] = color;
                }
                else
                {
                    data[(y * diameter) + x] = Color.Transparent;
                }
            }
        }

        texture.SetData(data);
        _textures[key] = texture;
        return texture;
    }

    public SoundEffect? LoadSound(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_sounds.TryGetValue(path, out var cached) && !cached.IsDisposed)
        {
            return cached;
        }

        SoundEffect? sound = null;

        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                sound = SoundEffect.FromStream(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LightEngine] Diskten ses yükleme hatası ({path}): {ex.Message}");
            }
        }

        if (sound == null)
        {
            try
            {
                sound = _content.Load<SoundEffect>(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LightEngine] ContentManager ses yükleme uyarısı ({path}): {ex.Message}");
            }
        }

        if (sound != null)
        {
            _sounds[path] = sound;
        }

        return sound;
    }

    public SpriteFont? LoadFont(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_fonts.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var font = _content.Load<SpriteFont>(path);
            _fonts[path] = font;
            return font;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Yazı tipi yüklenemedi ({path}): {ex.Message}");
            return null;
        }
    }

    public T Load<T>(string assetName)
    {
        return _content.Load<T>(assetName);
    }

    public void UnloadAll()
    {
        foreach (var tex in _textures.Values)
        {
            if (!tex.IsDisposed) tex.Dispose();
        }
        _textures.Clear();

        foreach (var snd in _sounds.Values)
        {
            if (!snd.IsDisposed) snd.Dispose();
        }
        _sounds.Clear();

        _fonts.Clear();

        if (_whitePixel != null && !_whitePixel.IsDisposed)
        {
            _whitePixel.Dispose();
            _whitePixel = null;
        }
    }

    public void Dispose()
    {
        UnloadAll();
    }
}
