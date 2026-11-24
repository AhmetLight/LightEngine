using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Tilemaps;

/// <summary>
/// TiledMap verisini ekrana çizen, kamera görünüm alanı dışındaki karoları
/// hesaplamayan (Frustum Culling) yüksek performanslı harita çizim bileşeni.
/// </summary>
public class TilemapRenderer : Component
{
    private TiledMap? _map;

    public TiledMap? Map
    {
        get => _map;
        set => _map = value;
    }

    public Color TintColor { get; set; } = Color.White;

    public TilemapRenderer() { }

    public TilemapRenderer(TiledMap map)
    {
        _map = map;
    }

    public override void Draw(SpriteBatchRenderer renderer)
    {
        if (_map == null || Entity == null || !IsEnabled) return;

        var worldPos = Entity.Transform.WorldPosition;
        var scene = Entity.Scene;

        // Görünür dünya sınırlarını al (Frustum Culling)
        Rectangle visibleBounds;
        if (scene != null && scene.Camera.Origin != Vector2.Zero)
        {
            var vp = renderer.InnerBatch.GraphicsDevice.Viewport;
            visibleBounds = scene.Camera.GetVisibleWorldBounds(vp);
        }
        else
        {
            visibleBounds = new Rectangle(
                (int)worldPos.X,
                (int)worldPos.Y,
                _map.TotalWidthPixels,
                _map.TotalHeightPixels);
        }

        int startTileX = Math.Max(0, (visibleBounds.Left - (int)worldPos.X) / _map.TileWidth);
        int endTileX = Math.Min(_map.Width - 1, (visibleBounds.Right - (int)worldPos.X) / _map.TileWidth + 1);

        int startTileY = Math.Max(0, (visibleBounds.Top - (int)worldPos.Y) / _map.TileHeight);
        int endTileY = Math.Min(_map.Height - 1, (visibleBounds.Bottom - (int)worldPos.Y) / _map.TileHeight + 1);

        // Katmanları sırayla çiz
        for (int l = 0; l < _map.Layers.Count; l++)
        {
            var layer = _map.Layers[l];
            if (!layer.IsVisible || layer.Opacity <= 0f) continue;

            Color layerColor = TintColor * layer.Opacity;

            for (int y = startTileY; y <= endTileY; y++)
            {
                for (int x = startTileX; x <= endTileX; x++)
                {
                    int gid = layer.GetGid(x, y);
                    if (gid == 0) continue; // Boş karo

                    var tileset = _map.FindTilesetForGid(gid);
                    if (tileset?.Texture == null) continue;

                    var srcRect = tileset.GetSourceRect(gid);
                    Vector2 drawPos = worldPos + new Vector2(x * _map.TileWidth, y * _map.TileHeight);

                    renderer.DrawSprite(
                        texture: tileset.Texture,
                        position: drawPos,
                        sourceRectangle: srcRect,
                        color: layerColor,
                        rotation: 0f,
                        origin: Vector2.Zero,
                        scale: Vector2.One,
                        effects: SpriteEffects.None,
                        layerDepth: 0.1f * l);
                }
            }
        }
    }
}
