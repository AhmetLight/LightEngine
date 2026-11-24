using Microsoft.Xna.Framework;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Entegre ekran kartlarından güçlü sistemlere kadar ölçeklenebilen 3D kalite profilleri.
/// </summary>
public enum QualityPreset
{
    Low,
    Medium,
    High
}

/// <summary>
/// 3D render hattı için ışıklandırma, sis (fog) ve performans kalite ayarları.
/// </summary>
public class QualitySettings3D
{
    public QualityPreset Preset { get; private set; } = QualityPreset.Medium;
    public bool PreferPerPixelLighting { get; set; } = true;
    public bool FogEnabled { get; set; } = true;
    public float FogStart { get; set; } = 35f;
    public float FogEnd { get; set; } = 140f;
    public Color FogColor { get; set; } = new(24, 28, 36);
    public Color AmbientLightColor { get; set; } = new(55, 62, 76);

    public bool SkyboxEnabled { get; set; } = true;
    public Color SkyZenithColor { get; set; } = new(26, 44, 72);
    public Color SkyHorizonColor { get; set; } = new(65, 84, 108);
    public Color SkyGroundColor { get; set; } = new(18, 20, 26);


    /// <summary>
    /// Hazır kalite profilini (Düşük / Orta / Yüksek) tek seferde uygular.
    /// </summary>
    public void ApplyPreset(QualityPreset preset)
    {
        Preset = preset;
        switch (preset)
        {
            case QualityPreset.Low:
                PreferPerPixelLighting = false;
                FogEnabled = false;
                break;
            case QualityPreset.Medium:
                PreferPerPixelLighting = true;
                FogEnabled = true;
                FogStart = 35f;
                FogEnd = 130f;
                break;
            case QualityPreset.High:
                PreferPerPixelLighting = true;
                FogEnabled = true;
                FogStart = 45f;
                FogEnd = 180f;
                break;
        }
    }
}
