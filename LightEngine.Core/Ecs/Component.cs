using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Tüm özel oyun bileşenlerinin türeyeceği temel soyut sınıf.
/// Yaşam döngüsü metotları için varsayılan boş uygulamalar sunar.
/// </summary>
public abstract class Component : IComponent
{
    public IEntity? Entity { get; private set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Varlığın Transform bileşenine hızlı erişim sağlayan yardımcı özellik.
    /// </summary>
    public Transform2D? Transform => Entity?.Transform;

    public virtual void Attach(IEntity entity)
    {
        Entity = entity;
    }

    public virtual void Detach()
    {
        Entity = null;
    }

    public virtual void Initialize() { }

    public virtual void Update(float deltaTime) { }

    public virtual void FixedUpdate(float fixedDeltaTime) { }

    public virtual void Draw(SpriteBatchRenderer renderer) { }

    public virtual void Destroy() { }
}
