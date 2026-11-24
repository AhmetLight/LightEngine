using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// 3D ışık kaynaklarının türleri.
/// </summary>
public enum LightType3D
{
    Directional,
    Point,
    Spot
}

/// <summary>
/// 3D sahneleri aydınlatan ışık kaynağı bileşeni (Yönlü Güneş Işığı / Nokta Işık / Spot).
/// </summary>
public class Light3D : Component
{
    public LightType3D LightType { get; set; } = LightType3D.Directional;
    public Color Color { get; set; } = new(255, 245, 230);
    public Color SpecularColor { get; set; } = new(255, 255, 255);
    public float Intensity { get; set; } = 1.0f;
    public float Range { get; set; } = 25f;

    /// <summary>
    /// Yönlü ışık için varsayılan yön (Transform3D varsa Forward vektörünü kullanır).
    /// </summary>
    public Vector3 Direction
    {
        get
        {
            var t3d = Entity?.GetComponent<Transform3D>();
            if (t3d != null)
            {
                return t3d.Forward;
            }
            return Vector3.Normalize(new Vector3(-0.5f, -0.8f, -0.4f));
        }
    }
}
