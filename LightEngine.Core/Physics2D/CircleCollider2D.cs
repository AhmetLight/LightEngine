using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// Dairesel 2D çarpışma geometrisi bileşeni.
/// </summary>
public class CircleCollider2D : Collider2D
{
    public float Radius { get; set; } = 16f;

    public CircleCollider2D() { }

    public CircleCollider2D(float radius)
    {
        Radius = radius;
    }

    public Vector2 Center => Entity != null ? Entity.Transform.WorldPosition + Offset : Offset;

    public float ScaledRadius
    {
        get
        {
            if (Entity == null) return Radius;
            var scale = Entity.Transform.WorldScale;
            return Radius * MathF.Max(scale.X, scale.Y);
        }
    }

    public override Rectangle GetAABB()
    {
        var center = Center;
        float r = ScaledRadius;
        int d = (int)(r * 2);

        return new Rectangle((int)(center.X - r), (int)(center.Y - r), d, d);
    }
}
