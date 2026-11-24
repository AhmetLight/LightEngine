using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Game.Components;

/// <summary>
/// Nesne havuzundan (ObjectPool) alınan ve sıfır bellek tahsisi (zero-allocation) ile
/// çalışan mermi/lazer bileşeni. Süresi dolunca havuza geri döner.
/// </summary>
public class PooledLaser : Component, IPoolable
{
    public Vector2 Direction { get; set; } = Vector2.UnitX;
    public float Speed { get; set; } = 750f;
    public float MaxLifetime { get; set; } = 2.0f;
    public float CurrentTimer { get; set; }

    public Action<Entity>? OnLifetimeExpired { get; set; }

    public void Reset()
    {
        Direction = Vector2.UnitX;
        Speed = 750f;
        MaxLifetime = 2.0f;
        CurrentTimer = 0f;
    }

    public override void Update(float deltaTime)
    {
        if (Entity == null || !Entity.IsActive) return;

        // Mermiyi yönünde ilerlet
        Entity.Transform.Translate(Direction * (Speed * deltaTime));

        CurrentTimer += deltaTime;
        if (CurrentTimer >= MaxLifetime)
        {
            if (Entity is Entity concreteEntity)
            {
                OnLifetimeExpired?.Invoke(concreteEntity);
            }
        }
    }
}
