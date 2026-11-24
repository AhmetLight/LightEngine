using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// Bir Entity'ye 2D doku (Sprite) çizme yeteneği kazandıran görsel bileşen.
/// </summary>
public class SpriteRenderer : Component
{
    private Texture2D? _texture;

    public Texture2D? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            if (CenterOrigin && _texture != null)
            {
                Origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
            }
        }
    }

    public Color Color { get; set; } = Color.White;
    public Rectangle? SourceRectangle { get; set; }
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public Vector2 Scale { get; set; } = Vector2.One;
    public SpriteEffects Effects { get; set; } = SpriteEffects.None;
    public float LayerDepth { get; set; } = 0f;

    /// <summary>
    /// True ise dokunun merkezi otomatik olarak pivot noktası (Origin) seçilir.
    /// </summary>
    public bool CenterOrigin { get; set; } = true;

    public SpriteRenderer() { }

    public SpriteRenderer(Texture2D texture, bool centerOrigin = true)
    {
        CenterOrigin = centerOrigin;
        Texture = texture;
    }

    public override void Draw(SpriteBatchRenderer renderer)
    {
        if (Texture == null || !IsEnabled || Entity == null) return;

        var transform = Entity.Transform;
        Vector2 position = transform.WorldPosition;
        float rotation = transform.WorldRotation;
        Vector2 scale = transform.WorldScale * Scale;

        renderer.DrawSprite(
            texture: Texture,
            position: position,
            sourceRectangle: SourceRectangle,
            color: Color,
            rotation: rotation,
            origin: Origin,
            scale: scale,
            effects: Effects,
            layerDepth: LayerDepth);
    }
}
