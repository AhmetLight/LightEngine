using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Animation;

/// <summary>
/// Çoklu animasyon karelerini barındıran sprite sheet görselini parçalayan ve klipler üreten yardımcı sınıf.
/// </summary>
public class SpriteSheet
{
    private readonly Texture2D _texture;
    private readonly List<Rectangle> _allFrames = new();

    public Texture2D Texture => _texture;
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int TotalFrames => _allFrames.Count;

    public SpriteSheet(Texture2D texture, int frameWidth, int frameHeight, int margin = 0, int spacing = 0)
    {
        _texture = texture ?? throw new ArgumentNullException(nameof(texture));
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;

        Columns = (texture.Width - margin * 2 + spacing) / (frameWidth + spacing);
        Rows = (texture.Height - margin * 2 + spacing) / (frameHeight + spacing);

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                int x = margin + c * (frameWidth + spacing);
                int y = margin + r * (frameHeight + spacing);
                _allFrames.Add(new Rectangle(x, y, frameWidth, frameHeight));
            }
        }
    }

    /// <summary>
    /// Belirtilen indis aralığındaki karelerden yeni bir animasyon klibi (AnimationClip) oluşturur.
    /// </summary>
    public AnimationClip CreateClip(string name, int startIndex, int count, float frameDuration = 0.1f, bool isLooping = true)
    {
        var clip = new AnimationClip(name, _texture, frameDuration, isLooping);
        int maxIndex = Math.Min(startIndex + count, _allFrames.Count);

        for (int i = startIndex; i < maxIndex; i++)
        {
            clip.AddFrame(_allFrames[i]);
        }

        return clip;
    }

    /// <summary>
    /// Belirli bir satırdaki tüm kareleri bir klip olarak döndürür.
    /// </summary>
    public AnimationClip CreateRowClip(string name, int rowIndex, float frameDuration = 0.1f, bool isLooping = true)
    {
        int startIndex = rowIndex * Columns;
        return CreateClip(name, startIndex, Columns, frameDuration, isLooping);
    }

    public Rectangle GetFrameRect(int index)
    {
        if (index >= 0 && index < _allFrames.Count)
        {
            return _allFrames[index];
        }
        return Rectangle.Empty;
    }
}
