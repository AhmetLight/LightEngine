using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Bir Entity'ye 3D uzayda konum, döndürme (Euler derece / Quaternion) ve ölçek bilgisi kazandıran bileşen.
/// Mevcut 2D Transform2D sistemini bozmadan 3D sahnelerde hiyerarşik dönüşüm (Parent/Child) sağlar.
/// </summary>
public class Transform3D : Component
{
    private Vector3 _localPosition = Vector3.Zero;
    private Vector3 _localRotationDegrees = Vector3.Zero;
    private Vector3 _localScale = Vector3.One;
    private Transform3D? _parent;

    private Matrix _worldMatrix = Matrix.Identity;
    private bool _isDirty = true;

    /// <summary>
    /// Yerel 3D konum (X, Y, Z).
    /// </summary>
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            if (_localPosition != value)
            {
                _localPosition = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Yerel 3D döndürme (Pitch/X, Yaw/Y, Roll/Z — derece cinsinden).
    /// </summary>
    public Vector3 LocalRotationDegrees
    {
        get => _localRotationDegrees;
        set
        {
            if (_localRotationDegrees != value)
            {
                _localRotationDegrees = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Yerel 3D döndürmenin Quaternion karşılığı.
    /// </summary>
    public Quaternion LocalRotationQuaternion => Quaternion.CreateFromYawPitchRoll(
        MathHelper.ToRadians(_localRotationDegrees.Y),
        MathHelper.ToRadians(_localRotationDegrees.X),
        MathHelper.ToRadians(_localRotationDegrees.Z));

    /// <summary>
    /// Yerel 3D ölçek (X, Y, Z).
    /// </summary>
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            if (_localScale != value)
            {
                _localScale = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Üst 3D dönüşüm bileşeni (Parent).
    /// </summary>
    public Transform3D? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value && value != this)
            {
                _parent = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Dünya uzayındaki 4x4 dönüşüm matrisi (Ölçek * Döndürme * Taşıma * Parent).
    /// </summary>
    public Matrix WorldMatrix
    {
        get
        {
            if (_isDirty || (_parent != null && _parent._isDirty))
            {
                RecalculateWorldMatrix();
            }
            return _worldMatrix;
        }
    }

    /// <summary>
    /// Dünya uzayındaki konum.
    /// </summary>
    public Vector3 WorldPosition => WorldMatrix.Translation;

    /// <summary>
    /// Nesnenin ileri (Forward) yön vektörü.
    /// </summary>
    public Vector3 Forward => Vector3.Normalize(WorldMatrix.Forward);

    /// <summary>
    /// Nesnenin sağ (Right) yön vektörü.
    /// </summary>
    public Vector3 Right => Vector3.Normalize(WorldMatrix.Right);

    /// <summary>
    /// Nesnenin yukarı (Up) yön vektörü.
    /// </summary>
    public Vector3 Up => Vector3.Normalize(WorldMatrix.Up);

    /// <summary>
    /// Nesneyi yerel eksende öteleyerek taşır.
    /// </summary>
    public void Translate(Vector3 delta)
    {
        LocalPosition += delta;
    }

    /// <summary>
    /// Nesneyi Euler derece cinsinden döndürür.
    /// </summary>
    public void Rotate(Vector3 deltaDegrees)
    {
        LocalRotationDegrees += deltaDegrees;
    }

    private void MarkDirty()
    {
        _isDirty = true;
    }

    private void RecalculateWorldMatrix()
    {
        Matrix scale = Matrix.CreateScale(_localScale);
        Matrix rot = Matrix.CreateFromQuaternion(LocalRotationQuaternion);
        Matrix trans = Matrix.CreateTranslation(_localPosition);

        Matrix local = scale * rot * trans;
        _worldMatrix = _parent != null ? local * _parent.WorldMatrix : local;
        _isDirty = false;
    }
}
