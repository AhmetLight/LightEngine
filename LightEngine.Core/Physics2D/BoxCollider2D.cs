using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// Eksenlere hizalı dikdörtgen (AABB) 2D çarpışma geometrisi bileşeni.
/// </summary>
public class BoxCollider2D : Collider2D
{
    public float Width { get; set; } = 32f;
    public float Height { get; set; } = 32f;

    public BoxCollider2D() { }

    public BoxCollider2D(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public override Rectangle GetAABB()
    {
        if (Entity == null) return Rectangle.Empty;

        var center = Entity.Transform.WorldPosition + Offset;
        var scale = Entity.Transform.WorldScale;

        int w = (int)(Width * scale.X);
        int h = (int)(Height * scale.Y);

        return new Rectangle((int)center.X - w / 2, (int)center.Y - h / 2, w, h);
    }
}
