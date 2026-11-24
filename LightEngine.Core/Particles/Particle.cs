using Microsoft.Xna.Framework;

namespace LightEngine.Core.Particles;

/// <summary>
/// Tek bir parçacığın (Particle) konum, hız, ömür, renk ve boyut verisini tutan yapı.
/// </summary>
public struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Acceleration;

    public Color StartColor;
    public Color EndColor;
    public Color CurrentColor;

    public float StartSize;
    public float EndSize;
    public float CurrentSize;

    public float Rotation;
    public float AngularVelocity;

    public float Lifetime;
    public float MaxLifetime;

    public bool IsAlive => Lifetime < MaxLifetime;

    public void Update(float deltaTime)
    {
        if (!IsAlive) return;

        Lifetime += deltaTime;
        float progress = Math.Clamp(Lifetime / MaxLifetime, 0f, 1f);

        Velocity += Acceleration * deltaTime;
        Position += Velocity * deltaTime;
        Rotation += AngularVelocity * deltaTime;

        // Boyut ve renk interpolasyonu
        CurrentSize = MathHelper.Lerp(StartSize, EndSize, progress);
        CurrentColor = Color.Lerp(StartColor, EndColor, progress);
    }
}
