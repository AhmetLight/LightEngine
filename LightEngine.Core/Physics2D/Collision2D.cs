using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// 2D çarpışma detaylarını (çarpan nesne, normal vektör, derinlik) barındıran veri yapısı.
/// </summary>
public struct Collision2D
{
    public Collider2D Collider { get; }
    public Collider2D OtherCollider { get; }
    public Vector2 Normal { get; }
    public float PenetrationDepth { get; }

    public Collision2D(Collider2D collider, Collider2D otherCollider, Vector2 normal, float penetrationDepth)
    {
        Collider = collider;
        OtherCollider = otherCollider;
        Normal = normal;
        PenetrationDepth = penetrationDepth;
    }
}
