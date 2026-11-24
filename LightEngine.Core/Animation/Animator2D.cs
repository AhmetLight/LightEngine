using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Animation;

/// <summary>
/// Bir Entity'nin SpriteRenderer bileşeni üzerinde 2D sprite animasyonlarını oynatan animatör bileşeni.
/// </summary>
public class Animator2D : Component
{
    private readonly Dictionary<string, AnimationClip> _clips = new(StringComparer.OrdinalIgnoreCase);
    private SpriteRenderer? _spriteRenderer;

    private float _frameTimer;
    private int _currentFrameIndex;
    private bool _isPlaying = true;

    public AnimationClip? CurrentClip { get; private set; }
    public int CurrentFrameIndex => _currentFrameIndex;
    public bool IsPlaying => _isPlaying;
    public float PlaybackSpeed { get; set; } = 1.0f;

    public bool FlipX { get; set; }
    public bool FlipY { get; set; }

    public event Action<string>? OnAnimationCompleted;

    public Animator2D() { }

    public override void Initialize()
    {
        base.Initialize();
        _spriteRenderer = Entity?.GetComponent<SpriteRenderer>();
    }

    public Animator2D AddClip(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        _clips[clip.Name] = clip;

        if (CurrentClip == null)
        {
            Play(clip.Name);
        }

        return this;
    }

    public void Play(string clipName, bool restartIfSame = false)
    {
        if (_clips.TryGetValue(clipName, out var clip))
        {
            if (CurrentClip == clip && !restartIfSame) return;

            CurrentClip = clip;
            _currentFrameIndex = 0;
            _frameTimer = 0f;
            _isPlaying = true;

            ApplyCurrentFrame();
        }
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Resume()
    {
        _isPlaying = true;
    }

    public void Stop()
    {
        _isPlaying = false;
        _currentFrameIndex = 0;
        _frameTimer = 0f;
        ApplyCurrentFrame();
    }

    public override void Update(float deltaTime)
    {
        if (!_isPlaying || CurrentClip == null || CurrentClip.Frames.Count == 0) return;

        _frameTimer += deltaTime * PlaybackSpeed;

        if (_frameTimer >= CurrentClip.FrameDuration)
        {
            _frameTimer -= CurrentClip.FrameDuration;
            _currentFrameIndex++;

            if (_currentFrameIndex >= CurrentClip.Frames.Count)
            {
                if (CurrentClip.IsLooping)
                {
                    _currentFrameIndex = 0;
                }
                else
                {
                    _currentFrameIndex = CurrentClip.Frames.Count - 1;
                    _isPlaying = false;
                }

                OnAnimationCompleted?.Invoke(CurrentClip.Name);
            }

            ApplyCurrentFrame();
        }
    }

    private void ApplyCurrentFrame()
    {
        if (_spriteRenderer == null && Entity != null)
        {
            _spriteRenderer = Entity.GetComponent<SpriteRenderer>();
        }

        if (_spriteRenderer != null && CurrentClip != null && CurrentClip.Frames.Count > 0)
        {
            if (CurrentClip.Texture != null)
            {
                _spriteRenderer.Texture = CurrentClip.Texture;
            }

            var frameRect = CurrentClip.GetFrame(_currentFrameIndex);
            _spriteRenderer.SourceRectangle = frameRect;

            if (_spriteRenderer.CenterOrigin)
            {
                _spriteRenderer.Origin = new Microsoft.Xna.Framework.Vector2(frameRect.Width / 2f, frameRect.Height / 2f);
            }

            SpriteEffects effects = SpriteEffects.None;
            if (FlipX) effects |= SpriteEffects.FlipHorizontally;
            if (FlipY) effects |= SpriteEffects.FlipVertically;
            _spriteRenderer.Effects = effects;
        }
    }
}
