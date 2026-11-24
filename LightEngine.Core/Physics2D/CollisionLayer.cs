namespace LightEngine.Core.Physics2D;

/// <summary>
/// Çarpışma filtreleme katmanları (Bitmask olarak kullanılır).
/// </summary>
[Flags]
public enum CollisionLayer
{
    None = 0,
    Default = 1 << 0,     // 1
    Player = 1 << 1,      // 2
    Enemy = 1 << 2,       // 4
    Obstacle = 1 << 3,    // 8
    Trigger = 1 << 4,     // 16
    Projectile = 1 << 5,  // 32
    All = ~0
}
