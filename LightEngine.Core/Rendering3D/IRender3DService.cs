using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// 3D sahne çizim, Frustum Culling, prosedürel mesh önbelleği, model yükleme
/// ve 3D editör gizmo/ızgara çizim servisini tanımlayan arayüz.
/// </summary>
public interface IRender3DService : IDisposable
{
    /// <summary>
    /// Grafik aygıtı referansı.
    /// </summary>
    Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    /// 3D kalite ve atmosfer ayarları.
    /// </summary>
    QualitySettings3D Quality { get; }

    /// <summary>
    /// Hafif Cascaded Shadow Map yöneticisi.
    /// </summary>
    CascadedShadowMap3D ShadowMap { get; }

    /// <summary>
    /// Açılıp kapanabilen Post-Processing (Bloom, FXAA, Tone Mapping, SSAO) yöneticisi.
    /// </summary>
    PostProcessor3D PostProcessor { get; }

    /// <summary>
    /// Son karede Frustum içine girip çizilen mesh sayısı.
    /// </summary>
    int RenderedMeshCount { get; }

    /// <summary>
    /// Son karede görüş alanı dışında kaldığı için elenen (culled) mesh sayısı.
    /// </summary>
    int CulledMeshCount { get; }

    /// <summary>
    /// Son karede çizilen toplam üçgen sayısı.
    /// </summary>
    int RenderedTriangleCount { get; }

    /// <summary>
    /// Sahnedeki statik yüzeyler için baked Lightmap dokusunu pişirip uygular.
    /// </summary>
    void BakeStaticLightmaps(IScene scene, int resolution = 128);

    /// <summary>
    /// Kalite profilini (Düşük / Orta / Yüksek) ışık, gölge ve post-process'e birlikte uygular.
    /// </summary>
    void ApplyQualityPreset(QualityPreset preset);

    /// <summary>
    /// Önbellekten yerleşik bir 3D ilkel şekil (Cube, Sphere, Plane, Cylinder, Cone, Pyramid) döndürür.
    /// </summary>
    Mesh3D GetPrimitiveMesh(PrimitiveType3D type);

    /// <summary>
    /// Diskten .obj, .gltf veya .glb 3D modelini yükler ve önbelleğe alır.
    /// </summary>
    Mesh3D LoadModel(string filePath);

    /// <summary>
    /// Verilen sahnedeki tüm aktif 3D nesneleri ve ışıkları belirtilen 3D kamera ile çizer.
    /// </summary>
    void RenderScene(IScene scene, Camera3D camera, bool drawEditorGrid = false, IEntity? selectedEntity = null);

    /// <summary>
    /// 3D uzayda tek bir çizgi çizer (hata ayıklama ve gizmo için).
    /// </summary>
    void DrawLine3D(Camera3D camera, Vector3 start, Vector3 end, Color color);

    /// <summary>
    /// 3D uzayda tel kafes sınır kutusu (BoundingBox) çizer.
    /// </summary>
    void DrawBoundingBox(Camera3D camera, BoundingBox box, Color color);
}
