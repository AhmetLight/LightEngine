using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Animation;

/// <summary>
/// Bir 2D sprite animasyonunun karelerini, süresini ve döngü durumunu temsil eden animasyon klibi sınıfı.
/// </summary>
public class AnimationClip
{
    public string Name { get; set; } = "Default";
    public Texture2D? Texture { get; set; }
    public List<Rectangle> Frames { get; } = new();
    public float FrameDuration { get; set; } = 0.1f; // Varsayılan 10 FPS (100 ms per frame)
    public bool IsLooping { get; set; } = true;

    /// <summary>
    /// Animasyonun toplam süresi (saniye cinsinden).
    /// </summary>
    public float TotalDuration => Frames.Count * FrameDuration;

    public AnimationClip() { }

    public AnimationClip(string name, Texture2D texture, float frameDuration = 0.1f, bool isLooping = true)
    {
        Name = name;
        Texture = texture;
        FrameDuration = frameDuration;
        IsLooping = isLooping;
    }

    public AnimationClip AddFrame(Rectangle frame)
    {
        Frames.Add(frame);
        return this;
    }

    public AnimationClip AddFrames(IEnumerable<Rectangle> frames)
    {
        Frames.AddRange(frames);
        return this;
    }

    public Rectangle GetFrame(int index)
    {
        if (Frames.Count == 0) return Rectangle.Empty;
        int clamped = Math.Clamp(index, 0, Frames.Count - 1);
        return Frames[clamped];
    }
}
