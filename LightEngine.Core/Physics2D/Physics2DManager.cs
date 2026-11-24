using Microsoft.Xna.Framework;

namespace LightEngine.Core.Physics2D;

/// <summary>
/// 2D AABB ve dairesel çarpışmaları, tetikleyicileri (Triggers) ve katmanları
/// sabit zaman adımı ile işleyen somut 2D fizik motoru servisi.
/// </summary>
public class Physics2DManager : IPhysics2DService
{
    private readonly List<Collider2D> _colliders = new(256);
    private readonly HashSet<(int, int)> _activeCollisions = new();
    private readonly HashSet<(int, int)> _activeTriggers = new();

    public CollisionMatrix Matrix { get; } = new();

    public void RegisterCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        if (!_colliders.Contains(collider))
        {
            _colliders.Add(collider);
        }
    }

    public void UnregisterCollider(Collider2D collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        _colliders.Remove(collider);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        var currentCollisions = new HashSet<(int, int)>();
        var currentTriggers = new HashSet<(int, int)>();

        for (int i = 0; i < _colliders.Count; i++)
        {
            var colA = _colliders[i];
            if (colA.Entity == null || !colA.Entity.IsActive || colA.Entity.IsDestroyed || !colA.IsEnabled)
                continue;

            for (int j = i + 1; j < _colliders.Count; j++)
            {
                var colB = _colliders[j];
                if (colB.Entity == null || !colB.Entity.IsActive || colB.Entity.IsDestroyed || !colB.IsEnabled)
                    continue;

                // 1. Katman filtresi
                if (!Matrix.CanCollide(colA.Layer, colB.Layer))
                    continue;

                // 2. Çarpışma kontrolü
                if (CheckCollision(colA, colB, out Vector2 normal, out float depth))
                {
                    var pair = (colA.GetHashCode(), colB.GetHashCode());

                    if (colA.IsTrigger || colB.IsTrigger)
                    {
                        currentTriggers.Add(pair);
                        if (!_activeTriggers.Contains(pair))
                        {
                            colA.NotifyTriggerEnter(colB);
                            colB.NotifyTriggerEnter(colA);
                        }
                        else
                        {
                            colA.NotifyTriggerStay(colB);
                            colB.NotifyTriggerStay(colA);
                        }
                    }
                    else
                    {
                        currentCollisions.Add(pair);
                        var colInfoA = new Collision2D(colA, colB, normal, depth);
                        var colInfoB = new Collision2D(colB, colA, -normal, depth);

                        if (!_activeCollisions.Contains(pair))
                        {
                            colA.NotifyCollisionEnter(colInfoA);
                            colB.NotifyCollisionEnter(colInfoB);
                        }
                        else
                        {
                            colA.NotifyCollisionStay(colInfoA);
                            colB.NotifyCollisionStay(colInfoB);
                        }

                        // Fiziksel itme ve ayırma (Resolution / Pushback)
                        ResolvePenetration(colA, colB, normal, depth);
                    }
                }
            }
        }

        // Çarpışması/Tetikleyicisi biten nesneler için Exit olayları
        foreach (var pair in _activeTriggers)
        {
            if (!currentTriggers.Contains(pair))
            {
                var (colA, colB) = FindColliderPair(pair);
                colA?.NotifyTriggerExit(colB!);
                colB?.NotifyTriggerExit(colA!);
            }
        }

        foreach (var pair in _activeCollisions)
        {
            if (!currentCollisions.Contains(pair))
            {
                var (colA, colB) = FindColliderPair(pair);
                if (colA != null && colB != null)
                {
                    colA.NotifyCollisionExit(new Collision2D(colA, colB, Vector2.Zero, 0));
                    colB.NotifyCollisionExit(new Collision2D(colB, colA, Vector2.Zero, 0));
                }
            }
        }

        _activeTriggers.Clear();
        _activeTriggers.UnionWith(currentTriggers);

        _activeCollisions.Clear();
        _activeCollisions.UnionWith(currentCollisions);
    }

    private (Collider2D?, Collider2D?) FindColliderPair((int, int) pair)
    {
        Collider2D? a = null, b = null;
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i].GetHashCode() == pair.Item1) a = _colliders[i];
            if (_colliders[i].GetHashCode() == pair.Item2) b = _colliders[i];
            if (a != null && b != null) break;
        }
        return (a, b);
    }

    private static bool CheckCollision(Collider2D a, Collider2D b, out Vector2 normal, out float depth)
    {
        normal = Vector2.Zero;
        depth = 0f;

        if (a is BoxCollider2D boxA && b is BoxCollider2D boxB)
        {
            return CheckBoxBox(boxA, boxB, out normal, out depth);
        }
        if (a is CircleCollider2D circleA && b is CircleCollider2D circleB)
        {
            return CheckCircleCircle(circleA, circleB, out normal, out depth);
        }
        if (a is BoxCollider2D box && b is CircleCollider2D circle)
        {
            bool hit = CheckBoxCircle(box, circle, out normal, out depth);
            return hit;
        }
        if (a is CircleCollider2D c && b is BoxCollider2D bx)
        {
            bool hit = CheckBoxCircle(bx, c, out normal, out depth);
            normal = -normal;
            return hit;
        }

        // Genel AABB yedek kontrolü
        return CheckAABB(a.GetAABB(), b.GetAABB(), out normal, out depth);
    }

    private static bool CheckBoxBox(BoxCollider2D a, BoxCollider2D b, out Vector2 normal, out float depth)
    {
        return CheckAABB(a.GetAABB(), b.GetAABB(), out normal, out depth);
    }

    private static bool CheckAABB(Rectangle a, Rectangle b, out Vector2 normal, out float depth)
    {
        normal = Vector2.Zero;
        depth = 0f;

        float dx = (a.Center.X) - (b.Center.X);
        float px = (a.Width / 2f + b.Width / 2f) - MathF.Abs(dx);
        if (px <= 0) return false;

        float dy = (a.Center.Y) - (b.Center.Y);
        float py = (a.Height / 2f + b.Height / 2f) - MathF.Abs(dy);
        if (py <= 0) return false;

        if (px < py)
        {
            depth = px;
            normal = new Vector2(dx < 0 ? -1f : 1f, 0f);
        }
        else
        {
            depth = py;
            normal = new Vector2(0f, dy < 0 ? -1f : 1f);
        }

        return true;
    }

    private static bool CheckCircleCircle(CircleCollider2D a, CircleCollider2D b, out Vector2 normal, out float depth)
    {
        normal = Vector2.Zero;
        depth = 0f;

        Vector2 diff = b.Center - a.Center;
        float distSq = diff.LengthSquared();
        float rSum = a.ScaledRadius + b.ScaledRadius;

        if (distSq >= rSum * rSum) return false;

        float dist = MathF.Sqrt(distSq);
        if (dist > 0.0001f)
        {
            normal = diff / dist;
            depth = rSum - dist;
        }
        else
        {
            normal = Vector2.UnitX;
            depth = rSum;
        }

        return true;
    }

    private static bool CheckBoxCircle(BoxCollider2D box, CircleCollider2D circle, out Vector2 normal, out float depth)
    {
        normal = Vector2.Zero;
        depth = 0f;

        var aabb = box.GetAABB();
        Vector2 circleCenter = circle.Center;

        float closestX = Math.Clamp(circleCenter.X, aabb.Left, aabb.Right);
        float closestY = Math.Clamp(circleCenter.Y, aabb.Top, aabb.Bottom);

        Vector2 diff = circleCenter - new Vector2(closestX, closestY);
        float distSq = diff.LengthSquared();
        float radius = circle.ScaledRadius;

        if (distSq > radius * radius) return false;

        float dist = MathF.Sqrt(distSq);
        if (dist > 0.0001f)
        {
            normal = diff / dist;
            depth = radius - dist;
        }
        else
        {
            normal = Vector2.UnitY;
            depth = radius;
        }

        return true;
    }

    private static void ResolvePenetration(Collider2D a, Collider2D b, Vector2 normal, float depth)
    {
        if (a.Entity == null || b.Entity == null) return;

        bool aStatic = a.IsStatic;
        bool bStatic = b.IsStatic;

        if (aStatic && bStatic) return;

        Vector2 separation = normal * depth;

        if (aStatic)
        {
            b.Entity.Transform.Translate(separation);
        }
        else if (bStatic)
        {
            a.Entity.Transform.Translate(-separation);
        }
        else
        {
            // Her iki nesne de dinamikse %50-%50 ayrılır
            a.Entity.Transform.Translate(-separation * 0.5f);
            b.Entity.Transform.Translate(separation * 0.5f);
        }
    }

    public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out RaycastHit2D hit, CollisionLayer layerMask = CollisionLayer.All)
    {
        hit = default;
        float closestDist = maxDistance;
        bool foundHit = false;

        if (direction.LengthSquared() > 0.0001f)
        {
            direction.Normalize();
        }

        for (int i = 0; i < _colliders.Count; i++)
        {
            var col = _colliders[i];
            if (col.Entity == null || !col.Entity.IsActive || !col.IsEnabled) continue;
            if ((col.Layer & layerMask) == 0) continue;

            var aabb = col.GetAABB();
            if (RayIntersectsAABB(origin, direction, aabb, out float dist, out Vector2 norm))
            {
                if (dist < closestDist && dist >= 0)
                {
                    closestDist = dist;
                    hit.Collider = col;
                    hit.Distance = dist;
                    hit.Point = origin + direction * dist;
                    hit.Normal = norm;
                    foundHit = true;
                }
            }
        }

        return foundHit;
    }

    private static bool RayIntersectsAABB(Vector2 origin, Vector2 dir, Rectangle aabb, out float dist, out Vector2 normal)
    {
        dist = -1f;
        normal = Vector2.Zero;

        float tmin = (aabb.Left - origin.X) / (dir.X != 0 ? dir.X : 0.00001f);
        float tmax = (aabb.Right - origin.X) / (dir.X != 0 ? dir.X : 0.00001f);
        if (tmin > tmax) (tmin, tmax) = (tmax, tmin);

        float tymin = (aabb.Top - origin.Y) / (dir.Y != 0 ? dir.Y : 0.00001f);
        float tymax = (aabb.Bottom - origin.Y) / (dir.Y != 0 ? dir.Y : 0.00001f);
        if (tymin > tymax) (tymin, tymax) = (tymax, tymin);

        if (tmin > tymax || tymin > tmax) return false;

        float tNear = MathF.Max(tmin, tymin);
        float tFar = MathF.Min(tmax, tymax);

        if (tFar < 0) return false;

        dist = tNear >= 0 ? tNear : tFar;
        normal = MathF.Abs(tmin - dist) < 0.001f ? new Vector2(-MathF.Sign(dir.X), 0) : new Vector2(0, -MathF.Sign(dir.Y));
        return true;
    }
}
