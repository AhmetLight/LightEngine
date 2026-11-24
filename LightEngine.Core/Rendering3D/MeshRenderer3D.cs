using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Bir Entity'ye 3D mesh ve materyal çizme yeteneği kazandıran bileşen.
/// Frustum Culling ve 3D Raycast seçimi için dünya uzayı sınır kutusunu hesaplar.
/// </summary>
public class MeshRenderer3D : Component
{
    public Mesh3D? Mesh { get; set; }
    public Material3D Material { get; set; } = new();
    public bool CastShadows { get; set; } = true;
    public bool ReceiveShadows { get; set; } = true;

    public MeshRenderer3D() { }

    public MeshRenderer3D(Mesh3D mesh, Material3D? material = null)
    {
        Mesh = mesh;
        if (material != null) Material = material;
    }

    /// <summary>
    /// Mesh'in Transform3D ile dönüştürülmüş dünya uzayı sınır kutusunu (AABB) döndürür.
    /// </summary>
    public BoundingBox GetWorldBoundingBox()
    {
        var local = Mesh?.LocalBounds ?? new BoundingBox(new Vector3(-0.5f), new Vector3(0.5f));
        var t3d = Entity?.GetComponent<Transform3D>();
        if (t3d == null) return local;

        Matrix world = t3d.WorldMatrix;
        Vector3[] corners = local.GetCorners();

        Vector3 min = Vector3.Transform(corners[0], world);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 transformed = Vector3.Transform(corners[i], world);
            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }

        return new BoundingBox(min, max);
    }
}
