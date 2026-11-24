using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Sahnedeki oyun nesnelerinin somut uygulaması.
/// Bileşenleri barındırır, yaşam döngüsünü yürütür ve dönüşüm hiyerarşisine sahiptir.
/// </summary>
public class Entity : IEntity
{
    private readonly List<IComponent> _components = new(8);
    private readonly List<IComponent> _pendingComponents = new(4);
    private readonly List<IComponent> _componentsToRemove = new(4);

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Entity";
    public string Tag { get; set; } = "Untagged";
    public bool IsActive { get; set; } = true;
    public bool IsDestroyed { get; private set; }

    public IScene? Scene { get; internal set; }
    public Transform2D Transform { get; } = new();

    public IReadOnlyList<IComponent> Components => _components;

    public Entity() { }

    public Entity(string name)
    {
        Name = name;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T();
        return AddComponent(component);
    }

    public T AddComponent<T>(T component) where T : IComponent
    {
        ArgumentNullException.ThrowIfNull(component);

        component.Attach(this);
        _pendingComponents.Add(component);
        return component;
    }

    public T? GetComponent<T>() where T : class, IComponent
    {
        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T typedComponent)
            {
                return typedComponent;
            }
        }

        for (int i = 0; i < _pendingComponents.Count; i++)
        {
            if (_pendingComponents[i] is T pendingTyped)
            {
                return pendingTyped;
            }
        }

        return null;
    }

    public bool TryGetComponent<T>(out T? component) where T : class, IComponent
    {
        component = GetComponent<T>();
        return component != null;
    }

    public IEnumerable<T> GetComponents<T>() where T : class, IComponent
    {
        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T typedComponent)
            {
                yield return typedComponent;
            }
        }
    }

    public bool RemoveComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.Contains(component) && !_componentsToRemove.Contains(component))
        {
            _componentsToRemove.Add(component);
            return true;
        }

        return _pendingComponents.Remove(component);
    }

    public bool RemoveComponent<T>() where T : class, IComponent
    {
        var comp = GetComponent<T>();
        if (comp != null)
        {
            return RemoveComponent(comp);
        }
        return false;
    }

    public bool HasComponent<T>() where T : class, IComponent
    {
        return GetComponent<T>() != null;
    }

    public virtual void Initialize()
    {
        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            _components[i].Initialize();
        }
    }

    public virtual void Update(float deltaTime)
    {
        if (!IsActive || IsDestroyed) return;

        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            var comp = _components[i];
            if (comp.IsEnabled)
            {
                comp.Update(deltaTime);
            }
        }
    }

    public virtual void FixedUpdate(float fixedDeltaTime)
    {
        if (!IsActive || IsDestroyed) return;

        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            var comp = _components[i];
            if (comp.IsEnabled)
            {
                comp.FixedUpdate(fixedDeltaTime);
            }
        }
    }

    public virtual void Draw(SpriteBatchRenderer renderer)
    {
        if (!IsActive || IsDestroyed) return;

        for (int i = 0; i < _components.Count; i++)
        {
            var comp = _components[i];
            if (comp.IsEnabled)
            {
                comp.Draw(renderer);
            }
        }
    }

    public virtual void Destroy()
    {
        if (IsDestroyed) return;

        IsDestroyed = true;
        IsActive = false;

        ProcessPendingComponents();

        for (int i = 0; i < _components.Count; i++)
        {
            _components[i].Destroy();
            _components[i].Detach();
        }

        _components.Clear();
        _pendingComponents.Clear();
        _componentsToRemove.Clear();
    }

    private bool _isProcessingComponents;

    private void ProcessPendingComponents()
    {
        if (_isProcessingComponents) return;
        _isProcessingComponents = true;

        try
        {
            while (_componentsToRemove.Count > 0 || _pendingComponents.Count > 0)
            {
                if (_componentsToRemove.Count > 0)
                {
                    var toRemove = _componentsToRemove.ToArray();
                    _componentsToRemove.Clear();
                    for (int i = 0; i < toRemove.Length; i++)
                    {
                        var comp = toRemove[i];
                        comp.Destroy();
                        comp.Detach();
                        _components.Remove(comp);
                    }
                }

                if (_pendingComponents.Count > 0)
                {
                    var pending = _pendingComponents.ToArray();
                    _pendingComponents.Clear();
                    for (int i = 0; i < pending.Length; i++)
                    {
                        var comp = pending[i];
                        _components.Add(comp);
                        comp.Initialize();
                    }
                }
            }
        }
        finally
        {
            _isProcessingComponents = false;
        }
    }
}
