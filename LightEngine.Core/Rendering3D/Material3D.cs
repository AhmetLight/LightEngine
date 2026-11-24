using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// 3D yüzeylerin ışıkla etkileşimini (Blinn-Phong / PBR hazırlığı) tanımlayan materyal sınıfı.
/// </summary>
public class Material3D
{
    /// <summary>
    /// Materyalin temel yüzey (Diffuse/Albedo) rengi.
    /// </summary>
    public Color DiffuseColor { get; set; } = new(195, 205, 220);

    /// <summary>
    /// Parlama (Specular) rengi.
    /// </summary>
    public Color SpecularColor { get; set; } = new(240, 240, 240);

    /// <summary>
    /// Parlama keskinliği (Blinn-Phong shininess / specular power).
    /// </summary>
    public float SpecularPower { get; set; } = 32f;

    /// <summary>
    /// Kendi kendine ışık yayma (Emissive) rengi.
    /// </summary>
    public Color EmissiveColor { get; set; } = Color.Black;

    /// <summary>
    /// Opaklık değeri (0.0 - 1.0).
    /// </summary>
    public float Alpha { get; set; } = 1.0f;

    /// <summary>
    /// İsteğe bağlı Diffuse dokusu.
    /// </summary>
    public Texture2D? DiffuseTexture { get; set; }

    /// <summary>
    /// İsteğe bağlı Normal Map dokusu (ileri düzey shader'lar için).
    /// </summary>
    public Texture2D? NormalMapTexture { get; set; }

    /// <summary>
    /// İsteğe bağlı Specular Map dokusu.
    /// </summary>
    public Texture2D? SpecularMapTexture { get; set; }

    /// <summary>
    /// Statik ışıklandırma için pişirilmiş Lightmap dokusu.
    /// </summary>
    public Texture2D? LightmapTexture { get; set; }

    /// <summary>
    /// Hafif PBR pürüzlülük değeri (0 = ayna, 1 = mat).
    /// </summary>
    public float Roughness { get; set; } = 0.45f;

    /// <summary>
    /// Hafif PBR metaliklik değeri (0 = dielektrik, 1 = metal).
    /// </summary>
    public float Metallic { get; set; } = 0.0f;

    /// <summary>
    /// Işıklandırma hesabı aktif mi?
    /// </summary>
    public bool EnableLighting { get; set; } = true;

    /// <summary>
    /// Tel kafes (Wireframe) modunda mı çizilsin?
    /// </summary>
    public bool Wireframe { get; set; } = false;

    /// <summary>
    /// Materyalin kopyasını oluşturur.
    /// </summary>
    public Material3D Clone() => new()
    {
        DiffuseColor = DiffuseColor,
        SpecularColor = SpecularColor,
        SpecularPower = SpecularPower,
        EmissiveColor = EmissiveColor,
        Alpha = Alpha,
        DiffuseTexture = DiffuseTexture,
        NormalMapTexture = NormalMapTexture,
        SpecularMapTexture = SpecularMapTexture,
        LightmapTexture = LightmapTexture,
        Roughness = Roughness,
        Metallic = Metallic,
        EnableLighting = EnableLighting,
        Wireframe = Wireframe
    };
}
