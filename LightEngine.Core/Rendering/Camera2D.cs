using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// 2D oyun dünyasını ekrana yansıtan, hedef takip (Follow), yakınlaştırma (Zoom)
/// ve sarsıntı (Shake) özelliklerine sahip 2D kamera sınıfı.
/// </summary>
public class Camera2D
{
    private Vector2 _position = Vector2.Zero;
    private float _zoom = 1.0f;
    private float _rotation = 0f;
    private Vector2 _origin = Vector2.Zero;

    private Matrix _viewMatrix = Matrix.Identity;
    private Matrix _inverseViewMatrix = Matrix.Identity;
    private bool _isDirty = true;

    // Sarsıntı (Shake) değişkenleri
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private Vector2 _shakeOffset = Vector2.Zero;
    private float _shakeRotation;
    private readonly Random _random = new();

    /// <summary>
    /// Kameranın dünya koordinatlarındaki merkezi konumu.
    /// </summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                _isDirty = true;
            }
        }
    }

    /// <summary>
    /// Kamera yakınlaştırma katsayısı (1.0 = normal, 2.0 = 2x yakın, 0.5 = 2x uzak).
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            float clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (Math.Abs(_zoom - clamped) > 0.0001f)
            {
                _zoom = clamped;
                _isDirty = true;
            }
        }
    }

    public float MinZoom { get; set; } = 0.1f;
    public float MaxZoom { get; set; } = 10.0f;

    /// <summary>
    /// Kameranın dönme açısı (radyan cinsinden).
    /// </summary>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (Math.Abs(_rotation - value) > 0.0001f)
            {
                _rotation = value;
                _isDirty = true;
            }
        }
    }

    /// <summary>
    /// Ekranın dönüş ve ölçekleme merkez noktası (genellikle ekranın tam ortası).
    /// </summary>
    public Vector2 Origin
    {
        get => _origin;
        set
        {
            if (_origin != value)
            {
                _origin = value;
                _isDirty = true;
            }
        }
    }

    /// <summary>
    /// Kameranın sınırlandığı dünya dikdörtgeni (null ise sınırsız).
    /// </summary>
    public Rectangle? Limits { get; set; }

    /// <summary>
    /// 2D SpriteBatch.Begin çağrısına verilecek görünüm (View) matrisi.
    /// </summary>
    public Matrix ViewMatrix
    {
        get
        {
            UpdateMatricesIfNeeded();
            return _viewMatrix;
        }
    }

    /// <summary>
    /// Ekran koordinatlarını dünya koordinatlarına dönüştürmek için ters görünüm matrisi.
    /// </summary>
    public Matrix InverseViewMatrix
    {
        get
        {
            UpdateMatricesIfNeeded();
            return _inverseViewMatrix;
        }
    }

    /// <summary>
    /// Belirtilen hedef konumu yumuşak bir şekilde takip eder (Smooth Follow / Lerp).
    /// </summary>
    public void Follow(Vector2 targetPosition, float smoothSpeed, float deltaTime)
    {
        if (smoothSpeed <= 0f)
        {
            Position = targetPosition;
        }
        else
        {
            // Kare hızından bağımsız yumuşak takip formülü
            float t = 1.0f - MathF.Exp(-smoothSpeed * deltaTime);
            Position = Vector2.Lerp(Position, targetPosition, t);
        }
    }

    /// <summary>
    /// Belirli bir süre ve şiddette kamera sarsıntısı (Trauma/Shake) başlatır.
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    /// <summary>
    /// Her karede kameranın sarsıntısını ve sınırlarını günceller.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= deltaTime;
            float progress = Math.Clamp(_shakeTimer / _shakeDuration, 0f, 1f);
            // Sarsıntı zamanla azalarak söner (quadratic decay)
            float currentIntensity = _shakeIntensity * (progress * progress);

            float offsetX = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
            float offsetY = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
            _shakeOffset = new Vector2(offsetX, offsetY);
            _shakeRotation = ((float)_random.NextDouble() * 2f - 1f) * (currentIntensity * 0.002f);

            _isDirty = true;
        }
        else
        {
            if (_shakeOffset != Vector2.Zero || _shakeRotation != 0f)
            {
                _shakeOffset = Vector2.Zero;
                _shakeRotation = 0f;
                _isDirty = true;
            }
        }

        ClampToLimits();
    }

    /// <summary>
    /// Ekran koordinatlarını (örn: fare tıklaması) dünya koordinatlarına çevirir.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return Vector2.Transform(screenPosition, InverseViewMatrix);
    }

    /// <summary>
    /// Dünya koordinatlarını ekran üzerindeki piksel koordinatlarına çevirir.
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, ViewMatrix);
    }

    /// <summary>
    /// Kameranın şu an gördüğü dünya sınırlarını hesaplar (Frustum culling için).
    /// </summary>
    public Rectangle GetVisibleWorldBounds(Viewport viewport)
    {
        Vector2 topLeft = ScreenToWorld(Vector2.Zero);
        Vector2 bottomRight = ScreenToWorld(new Vector2(viewport.Width, viewport.Height));

        int minX = (int)MathF.Min(topLeft.X, bottomRight.X);
        int minY = (int)MathF.Min(topLeft.Y, bottomRight.Y);
        int width = (int)MathF.Abs(bottomRight.X - topLeft.X);
        int height = (int)MathF.Abs(bottomRight.Y - topLeft.Y);

        return new Rectangle(minX, minY, width, height);
    }

    private void ClampToLimits()
    {
        if (!Limits.HasValue) return;

        var limits = Limits.Value;
        float clampedX = Math.Clamp(_position.X, limits.Left, limits.Right);
        float clampedY = Math.Clamp(_position.Y, limits.Top, limits.Bottom);

        if (clampedX != _position.X || clampedY != _position.Y)
        {
            _position = new Vector2(clampedX, clampedY);
            _isDirty = true;
        }
    }

    private void UpdateMatricesIfNeeded()
    {
        if (!_isDirty) return;

        Vector2 effectivePosition = _position + _shakeOffset;
        float effectiveRotation = _rotation + _shakeRotation;

        _viewMatrix = Matrix.CreateTranslation(new Vector3(-effectivePosition, 0f)) *
                      Matrix.CreateRotationZ(effectiveRotation) *
                      Matrix.CreateScale(new Vector3(_zoom, _zoom, 1f)) *
                      Matrix.CreateTranslation(new Vector3(_origin, 0f));

        _inverseViewMatrix = Matrix.Invert(_viewMatrix);
        _isDirty = false;
    }
}
