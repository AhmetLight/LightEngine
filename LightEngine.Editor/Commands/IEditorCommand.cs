using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Editor.Commands;

/// <summary>
/// Editörde geri alınabilir (Undo) ve yeniden yapılabilir (Redo) her türlü işlemi temsil eden arayüz.
/// </summary>
public interface IEditorCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Bir Entity'nin Transform (Pozisyon, Rotasyon, Ölçek) değişikliklerini geri/ileri alan komut.
/// </summary>
public class TransformCommand : IEditorCommand
{
    private readonly IEntity _entity;
    private readonly Vector2 _oldPos;
    private readonly float _oldRot;
    private readonly Vector2 _oldScale;

    private readonly Vector2 _newPos;
    private readonly float _newRot;
    private readonly Vector2 _newScale;

    public string Description => $"Transform '{_entity.Name}'";

    public TransformCommand(
        IEntity entity,
        Vector2 oldPos, float oldRot, Vector2 oldScale,
        Vector2 newPos, float newRot, Vector2 newScale)
    {
        _entity = entity;
        _oldPos = oldPos;
        _oldRot = oldRot;
        _oldScale = oldScale;

        _newPos = newPos;
        _newRot = newRot;
        _newScale = newScale;
    }

    public void Execute()
    {
        if (_entity.IsDestroyed) return;
        _entity.Transform.LocalPosition = _newPos;
        _entity.Transform.LocalRotation = _newRot;
        _entity.Transform.LocalScale = _newScale;
    }

    public void Undo()
    {
        if (_entity.IsDestroyed) return;
        _entity.Transform.LocalPosition = _oldPos;
        _entity.Transform.LocalRotation = _oldRot;
        _entity.Transform.LocalScale = _oldScale;
    }
}

/// <summary>
/// Bir Entity'nin 3D Transform (Pozisyon, Rotasyon, Ölçek) değişikliklerini geri/ileri alan komut.
/// </summary>
public class Transform3DCommand : IEditorCommand
{
    private readonly LightEngine.Core.Rendering3D.Transform3D _transform3D;
    private readonly string _entityName;
    private readonly Vector3 _oldPos;
    private readonly Vector3 _oldRot;
    private readonly Vector3 _oldScale;
    private readonly Vector3 _newPos;
    private readonly Vector3 _newRot;
    private readonly Vector3 _newScale;

    public string Description => $"Transform3D '{_entityName}'";

    public Transform3DCommand(
        LightEngine.Core.Rendering3D.Transform3D transform3D,
        Vector3 oldPos, Vector3 oldRot, Vector3 oldScale,
        Vector3 newPos, Vector3 newRot, Vector3 newScale)
    {
        _transform3D = transform3D;
        _entityName = transform3D.Entity?.Name ?? "Entity";
        _oldPos = oldPos;
        _oldRot = oldRot;
        _oldScale = oldScale;
        _newPos = newPos;
        _newRot = newRot;
        _newScale = newScale;
    }

    public void Execute()
    {
        if (_transform3D.Entity == null || _transform3D.Entity.IsDestroyed) return;
        _transform3D.LocalPosition = _newPos;
        _transform3D.LocalRotationDegrees = _newRot;
        _transform3D.LocalScale = _newScale;
    }

    public void Undo()
    {
        if (_transform3D.Entity == null || _transform3D.Entity.IsDestroyed) return;
        _transform3D.LocalPosition = _oldPos;
        _transform3D.LocalRotationDegrees = _oldRot;
        _transform3D.LocalScale = _oldScale;
    }
}
