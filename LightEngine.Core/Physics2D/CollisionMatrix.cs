namespace LightEngine.Core.Physics2D;

/// <summary>
/// Hangi katmanların birbiriyle çarpışacağını belirleyen 2D çarpışma matrisi.
/// </summary>
public class CollisionMatrix
{
    private readonly Dictionary<CollisionLayer, CollisionLayer> _matrix = new();

    public CollisionMatrix()
    {
        // Varsayılan olarak her katman birbiriyle çarpışabilir
        SetCollision(CollisionLayer.Player, CollisionLayer.Obstacle, true);
        SetCollision(CollisionLayer.Player, CollisionLayer.Enemy, true);
        SetCollision(CollisionLayer.Player, CollisionLayer.Trigger, true);
        SetCollision(CollisionLayer.Enemy, CollisionLayer.Obstacle, true);
        SetCollision(CollisionLayer.Projectile, CollisionLayer.Obstacle, true);
        SetCollision(CollisionLayer.Projectile, CollisionLayer.Enemy, true);
    }

    public void SetCollision(CollisionLayer layerA, CollisionLayer layerB, bool canCollide)
    {
        if (canCollide)
        {
            _matrix[layerA] = (_matrix.TryGetValue(layerA, out var maskA) ? maskA : CollisionLayer.None) | layerB;
            _matrix[layerB] = (_matrix.TryGetValue(layerB, out var maskB) ? maskB : CollisionLayer.None) | layerA;
        }
        else
        {
            if (_matrix.TryGetValue(layerA, out var maskA)) _matrix[layerA] = maskA & ~layerB;
            if (_matrix.TryGetValue(layerB, out var maskB)) _matrix[layerB] = maskB & ~layerA;
        }
    }

    public bool CanCollide(CollisionLayer layerA, CollisionLayer layerB)
    {
        if (_matrix.TryGetValue(layerA, out var mask))
        {
            return (mask & layerB) != 0;
        }
        return true; // Kayıt yoksa varsayılan olarak izin ver
    }
}
