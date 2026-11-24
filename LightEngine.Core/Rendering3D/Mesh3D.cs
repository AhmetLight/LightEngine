using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// GPU belleğinde (VertexBuffer + IndexBuffer) tutulan 3D ağ (Mesh) verisi.
/// Yerel sınır kutusu (BoundingBox) ile hızlı Frustum Culling ve Raycast Picking sağlar.
/// </summary>
public class Mesh3D : IDisposable
{
    public string Name { get; }
    public PrimitiveType3D PrimitiveType { get; }
    public string? SourcePath { get; }

    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public int VertexCount { get; }
    public int PrimitiveCount { get; }
    public BoundingBox LocalBounds { get; }

    private bool _disposed;

    public Mesh3D(
        GraphicsDevice graphicsDevice,
        string name,
        VertexPositionNormalTexture[] vertices,
        int[] indices,
        PrimitiveType3D primitiveType = PrimitiveType3D.Custom,
        string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

        Name = name;
        PrimitiveType = primitiveType;
        SourcePath = sourcePath;
        VertexCount = vertices.Length;
        PrimitiveCount = indices.Length / 3;

        VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices);

        IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);

        // Yerel sınır kutusunu (AABB) hesapla
        if (vertices.Length > 0)
        {
            Vector3 min = vertices[0].Position;
            Vector3 max = vertices[0].Position;
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i].Position);
                max = Vector3.Max(max, vertices[i].Position);
            }
            LocalBounds = new BoundingBox(min, max);
        }
        else
        {
            LocalBounds = new BoundingBox(new Vector3(-0.5f), new Vector3(0.5f));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        _disposed = true;
    }
}
