using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Oyun sahnelerinin somut uygulaması.
/// Sahnedeki varlıkların listesini tutar, ekleme/silme kuyruklarını yönetir ve döngülerini çalıştırır.
/// </summary>
public class Scene : IScene
{
    private readonly List<IEntity> _entities = new(128);
    private readonly List<IEntity> _pendingEntities = new(32);
    private readonly List<IEntity> _entitiesToRemove = new(32);

    public string Name { get; set; } = "Scene";
    public IReadOnlyList<IEntity> Entities
    {
        get
        {
            ProcessPendingEntities();
            return _entities;
        }
    }
    public Camera2D Camera { get; } = new();
    public Camera3D Camera3D { get; } = new();

    public Scene(string name = "Scene")
    {
        Name = name;
    }

    public virtual IEntity CreateEntity(string name = "Entity")
    {
        var entity = new Entity(name);
        AddEntity(entity);
        return entity;
    }

    public virtual void AddEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity is Entity concreteEntity)
        {
            concreteEntity.Scene = this;
        }

        _pendingEntities.Add(entity);
    }

    public virtual bool RemoveEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entities.Contains(entity) && !_entitiesToRemove.Contains(entity))
        {
            _entitiesToRemove.Add(entity);
            return true;
        }

        return _pendingEntities.Remove(entity);
    }

    public virtual IEntity? FindEntityByName(string name)
    {
        ProcessPendingEntities();

        for (int i = 0; i < _entities.Count; i++)
        {
            if (_entities[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return _entities[i];
            }
        }
        return null;
    }

    public virtual IEnumerable<IEntity> FindEntitiesByTag(string tag)
    {
        ProcessPendingEntities();

        for (int i = 0; i < _entities.Count; i++)
        {
            if (_entities[i].Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                yield return _entities[i];
            }
        }
    }

    public virtual void Initialize()
    {
        ProcessPendingEntities();

        for (int i = 0; i < _entities.Count; i++)
        {
            _entities[i].Initialize();
        }
    }

    public virtual void LoadContent() { }

    public virtual void UnloadContent()
    {
        Clear();
    }

    public virtual void Update(float deltaTime)
    {
        ProcessPendingEntities();

        Camera.Update(deltaTime);

        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.IsActive && !entity.IsDestroyed)
            {
                entity.Update(deltaTime);
            }
        }
    }

    public virtual void FixedUpdate(float fixedDeltaTime)
    {
        ProcessPendingEntities();

        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.IsActive && !entity.IsDestroyed)
            {
                entity.FixedUpdate(fixedDeltaTime);
            }
        }
    }

    public virtual void Draw(SpriteBatchRenderer renderer)
    {
        ProcessPendingEntities();

        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (entity.IsActive && !entity.IsDestroyed)
            {
                entity.Draw(renderer);
            }
        }
    }

    public virtual void Clear()
    {
        for (int i = 0; i < _entities.Count; i++)
        {
            _entities[i].Destroy();
        }

        _entities.Clear();
        _pendingEntities.Clear();
        _entitiesToRemove.Clear();
    }

    private bool _isProcessingEntities;

    private void ProcessPendingEntities()
    {
        if (_isProcessingEntities) return;
        _isProcessingEntities = true;

        try
        {
            while (_entitiesToRemove.Count > 0 || _pendingEntities.Count > 0)
            {
                if (_entitiesToRemove.Count > 0)
                {
                    var toRemove = _entitiesToRemove.ToArray();
                    _entitiesToRemove.Clear();
                    for (int i = 0; i < toRemove.Length; i++)
                    {
                        var entity = toRemove[i];
                        entity.Destroy();
                        _entities.Remove(entity);
                    }
                }

                if (_pendingEntities.Count > 0)
                {
                    var pending = _pendingEntities.ToArray();
                    _pendingEntities.Clear();
                    for (int i = 0; i < pending.Length; i++)
                    {
                        var entity = pending[i];
                        _entities.Add(entity);
                        entity.Initialize();
                    }
                }
            }
        }
        finally
        {
            _isProcessingEntities = false;
        }
    }
}
