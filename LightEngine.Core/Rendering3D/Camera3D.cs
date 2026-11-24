using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// 3D sahneleri görüntüleyen, serbest uçuş (FPS/Fly) ve yörünge (Orbit) kontrolü,
/// Frustum Culling ve ekran-ışın (Raycast Picking) dönüşümü sağlayan perspektif 3D kamera.
/// </summary>
public class Camera3D
{
    private Vector3 _position = new(0f, 4f, 10f);
    private float _yaw = 0f;     // Radyan (Y ekseni etrafında dönüş)
    private float _pitch = -0.35f; // Radyan (X ekseni etrafında eğim)
    private float _fieldOfView = MathHelper.ToRadians(60f);
    private float _aspectRatio = 16f / 9f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 500f;

    private Matrix _viewMatrix = Matrix.Identity;
    private Matrix _projectionMatrix = Matrix.Identity;
    private BoundingFrustum _frustum = new(Matrix.Identity);
    private bool _isDirty = true;

    public Vector3 Position
    {
        get => _position;
        set { _position = value; _isDirty = true; }
    }

    public float Yaw
    {
        get => _yaw;
        set { _yaw = value; _isDirty = true; }
    }

    public float Pitch
    {
        get => _pitch;
        set { _pitch = Math.Clamp(value, -MathHelper.PiOver2 + 0.05f, MathHelper.PiOver2 - 0.05f); _isDirty = true; }
    }

    public float FieldOfViewDegrees
    {
        get => MathHelper.ToDegrees(_fieldOfView);
        set { _fieldOfView = MathHelper.ToRadians(Math.Clamp(value, 20f, 120f)); _isDirty = true; }
    }

    public float AspectRatio
    {
        get => _aspectRatio;
        set
        {
            if (value > 0.01f && MathF.Abs(_aspectRatio - value) > 0.0001f)
            {
                _aspectRatio = value;
                _isDirty = true;
            }
        }
    }

    public float NearPlane
    {
        get => _nearPlane;
        set { _nearPlane = Math.Max(0.01f, value); _isDirty = true; }
    }

    public float FarPlane
    {
        get => _farPlane;
        set { _farPlane = Math.Max(_nearPlane + 1f, value); _isDirty = true; }
    }

    public Vector3 Forward
    {
        get
        {
            Matrix rot = Matrix.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
            return Vector3.Normalize(Vector3.Transform(Vector3.Forward, rot));
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));

    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    public Matrix ViewMatrix
    {
        get
        {
            EnsureMatrices();
            return _viewMatrix;
        }
    }

    public Matrix ProjectionMatrix
    {
        get
        {
            EnsureMatrices();
            return _projectionMatrix;
        }
    }

    /// <summary>
    /// Kamera görüş konisi (Frustum Culling için).
    /// </summary>
    public BoundingFrustum Frustum
    {
        get
        {
            EnsureMatrices();
            return _frustum;
        }
    }

    /// <summary>
    /// Kamerayı belirli bir hedef noktaya baktırır ve konumlandırır.
    /// </summary>
    public void FocusOn(Vector3 target, float distance = 6f)
    {
        Vector3 dir = Forward;
        if (dir.LengthSquared() < 0.001f) dir = new Vector3(0, -0.35f, -1f);
        dir.Normalize();
        Position = target - dir * distance;
    }

    /// <summary>
    /// Kamerayı belirli bir hedefe doğru çevirir.
    /// </summary>
    public void LookAt(Vector3 target)
    {
        Vector3 dir = target - _position;
        if (dir.LengthSquared() < 0.0001f) return;
        dir.Normalize();

        Yaw = MathF.Atan2(-dir.X, -dir.Z);
        Pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
    }

    /// <summary>
    /// Ekran piksel koordinatını (RenderTarget boyutuna göre) 3D dünya ışınına (Ray) dönüştürür.
    /// 3D nesne seçimi (Picking) ve sürükleme için kullanılır.
    /// </summary>
    public Ray ScreenPointToRay(Vector2 screenPoint, int viewportWidth, int viewportHeight)
    {
        EnsureMatrices();
        var vp = new Viewport(0, 0, Math.Max(1, viewportWidth), Math.Max(1, viewportHeight));

        Vector3 nearPoint = vp.Unproject(new Vector3(screenPoint.X, screenPoint.Y, 0f), _projectionMatrix, _viewMatrix, Matrix.Identity);
        Vector3 farPoint = vp.Unproject(new Vector3(screenPoint.X, screenPoint.Y, 1f), _projectionMatrix, _viewMatrix, Matrix.Identity);

        Vector3 direction = farPoint - nearPoint;
        if (direction.LengthSquared() > 0.000001f)
        {
            direction.Normalize();
        }
        else
        {
            direction = Forward;
        }

        return new Ray(nearPoint, direction);
    }

    /// <summary>
    /// Bir 3D dünya noktasını 2D ekran koordinatına projekte eder.
    /// </summary>
    public Vector3 WorldToScreen(Vector3 worldPoint, int viewportWidth, int viewportHeight)
    {
        EnsureMatrices();
        var vp = new Viewport(0, 0, Math.Max(1, viewportWidth), Math.Max(1, viewportHeight));
        return vp.Project(worldPoint, _projectionMatrix, _viewMatrix, Matrix.Identity);
    }

    private void EnsureMatrices()
    {
        if (!_isDirty) return;

        Vector3 forward = Forward;
        _viewMatrix = Matrix.CreateLookAt(_position, _position + forward, Vector3.Up);
        _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(_fieldOfView, _aspectRatio, _nearPlane, _farPlane);
        _frustum.Matrix = _viewMatrix * _projectionMatrix;
        _isDirty = false;
    }
}
