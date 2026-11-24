using Microsoft.Xna.Framework;

namespace LightEngine.Core.Ecs;

/// <summary>
/// 2D dünyadaki konum, döndürme ve ölçeklendirme hiyerarşisini yöneten sınıf.
/// Ebeveyn-çocuk (parent-child) dönüşüm matrislerini otomatik hesaplar.
/// </summary>
public class Transform2D
{
    private Vector2 _localPosition = Vector2.Zero;
    private float _localRotation = 0f;
    private Vector2 _localScale = Vector2.One;

    private Transform2D? _parent;
    private readonly List<Transform2D> _children = new();

    private Matrix _localMatrix = Matrix.Identity;
    private Matrix _worldMatrix = Matrix.Identity;
    private bool _isDirty = true;

    /// <summary>
    /// Ebeveyne göre yerel konum.
    /// </summary>
    public Vector2 LocalPosition
    {
        get => _localPosition;
        set
        {
            if (_localPosition != value)
            {
                _localPosition = value;
                SetDirty();
            }
        }
    }

    /// <summary>
    /// Ebeveyne göre yerel döndürme açısı (radyan cinsinden).
    /// </summary>
    public float LocalRotation
    {
        get => _localRotation;
        set
        {
            if (_localRotation != value)
            {
                _localRotation = value;
                SetDirty();
            }
        }
    }

    /// <summary>
    /// Ebeveyne göre yerel ölçek.
    /// </summary>
    public Vector2 LocalScale
    {
        get => _localScale;
        set
        {
            if (_localScale != value)
            {
                _localScale = value;
                SetDirty();
            }
        }
    }

    /// <summary>
    /// Dünya uzayındaki mutlak konum.
    /// </summary>
    public Vector2 WorldPosition
    {
        get
        {
            UpdateMatricesIfNeeded();
            return new Vector2(_worldMatrix.Translation.X, _worldMatrix.Translation.Y);
        }
        set
        {
            if (_parent == null)
            {
                LocalPosition = value;
            }
            else
            {
                // Dünya konumunu ebeveynin ters matrisi ile yerel konuma çevir
                var invParent = Matrix.Invert(_parent.WorldMatrix);
                var transformed = Vector2.Transform(value, invParent);
                LocalPosition = transformed;
            }
        }
    }

    /// <summary>
    /// Dünya uzayındaki toplam döndürme açısı (radyan cinsinden).
    /// </summary>
    public float WorldRotation
    {
        get
        {
            if (_parent == null) return _localRotation;
            return _parent.WorldRotation + _localRotation;
        }
        set
        {
            if (_parent == null)
            {
                LocalRotation = value;
            }
            else
            {
                LocalRotation = value - _parent.WorldRotation;
            }
        }
    }

    /// <summary>
    /// Dünya uzayındaki toplam ölçek.
    /// </summary>
    public Vector2 WorldScale
    {
        get
        {
            if (_parent == null) return _localScale;
            return _parent.WorldScale * _localScale;
        }
        set
        {
            if (_parent == null)
            {
                LocalScale = value;
            }
            else
            {
                var parentScale = _parent.WorldScale;
                LocalScale = new Vector2(
                    parentScale.X != 0 ? value.X / parentScale.X : 0,
                    parentScale.Y != 0 ? value.Y / parentScale.Y : 0);
            }
        }
    }

    /// <summary>
    /// Hiyerarşik dünya dönüşüm matrisi.
    /// </summary>
    public Matrix WorldMatrix
    {
        get
        {
            UpdateMatricesIfNeeded();
            return _worldMatrix;
        }
    }

    /// <summary>
    /// Sağa bakan birim yön vektörü.
    /// </summary>
    public Vector2 Right
    {
        get
        {
            float rot = WorldRotation;
            return new Vector2(MathF.Cos(rot), MathF.Sin(rot));
        }
    }

    /// <summary>
    /// Yukarı bakan birim yön vektörü (2D Y ekseni aşağı arttığı için -Sin, Cos).
    /// </summary>
    public Vector2 Up
    {
        get
        {
            float rot = WorldRotation;
            return new Vector2(MathF.Sin(rot), -MathF.Cos(rot));
        }
    }

    public Transform2D? Parent => _parent;
    public IReadOnlyList<Transform2D> Children => _children;

    public void SetParent(Transform2D? parent, bool keepWorldTransform = true)
    {
        if (_parent == parent) return;

        Vector2 savedWorldPos = WorldPosition;
        float savedWorldRot = WorldRotation;
        Vector2 savedWorldScale = WorldScale;

        _parent?._children.Remove(this);
        _parent = parent;
        _parent?._children.Add(this);

        if (keepWorldTransform && parent != null)
        {
            WorldPosition = savedWorldPos;
            WorldRotation = savedWorldRot;
            WorldScale = savedWorldScale;
        }
        else
        {
            SetDirty();
        }
    }

    /// <summary>
    /// Konumu öteler (Translate).
    /// </summary>
    public void Translate(Vector2 delta)
    {
        LocalPosition += delta;
    }

    /// <summary>
    /// Döndürme açısını artırır (Rotate).
    /// </summary>
    public void Rotate(float deltaRadians)
    {
        LocalRotation += deltaRadians;
    }

    private void SetDirty()
    {
        _isDirty = true;
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].SetDirty();
        }
    }

    private void UpdateMatricesIfNeeded()
    {
        if (!_isDirty) return;

        _localMatrix = Matrix.CreateScale(new Vector3(_localScale, 1f)) *
                       Matrix.CreateRotationZ(_localRotation) *
                       Matrix.CreateTranslation(new Vector3(_localPosition, 0f));

        if (_parent != null)
        {
            _worldMatrix = _localMatrix * _parent.WorldMatrix;
        }
        else
        {
            _worldMatrix = _localMatrix;
        }

        _isDirty = false;
    }
}
