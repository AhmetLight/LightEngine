using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Küp, Küre, Düzlem, Silindir, Koni ve Piramit gibi temel 3D mesh'leri
/// doğru normal ve UV koordinatlarıyla prosedürel olarak üreten fabrika sınıfı.
/// </summary>
public static class ProceduralMeshFactory
{
    public static Mesh3D CreatePrimitive(GraphicsDevice graphicsDevice, PrimitiveType3D type)
    {
        return type switch
        {
            PrimitiveType3D.Cube => CreateCube(graphicsDevice),
            PrimitiveType3D.Sphere => CreateSphere(graphicsDevice),
            PrimitiveType3D.Plane => CreatePlane(graphicsDevice),
            PrimitiveType3D.Cylinder => CreateCylinder(graphicsDevice),
            PrimitiveType3D.Cone => CreateCone(graphicsDevice),
            PrimitiveType3D.Pyramid => CreatePyramid(graphicsDevice),
            _ => CreateCube(graphicsDevice)
        };
    }

    public static Mesh3D CreateCube(GraphicsDevice graphicsDevice, float size = 1f)
    {
        float h = size * 0.5f;
        var vertices = new List<VertexPositionNormalTexture>(24);
        var indices = new List<int>(36);

        void AddFace(Vector3 normal, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int start = vertices.Count;
            vertices.Add(new VertexPositionNormalTexture(p0, normal, new Vector2(0, 1)));
            vertices.Add(new VertexPositionNormalTexture(p1, normal, new Vector2(0, 0)));
            vertices.Add(new VertexPositionNormalTexture(p2, normal, new Vector2(1, 0)));
            vertices.Add(new VertexPositionNormalTexture(p3, normal, new Vector2(1, 1)));

            indices.Add(start + 0); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start + 0); indices.Add(start + 2); indices.Add(start + 3);
        }

        // Ön (+Z)
        AddFace(Vector3.Backward, new(-h, -h, h), new(-h, h, h), new(h, h, h), new(h, -h, h));
        // Arka (-Z)
        AddFace(Vector3.Forward, new(h, -h, -h), new(h, h, -h), new(-h, h, -h), new(-h, -h, -h));
        // Üst (+Y)
        AddFace(Vector3.Up, new(-h, h, h), new(-h, h, -h), new(h, h, -h), new(h, h, h));
        // Alt (-Y)
        AddFace(Vector3.Down, new(-h, -h, -h), new(-h, -h, h), new(h, -h, h), new(h, -h, -h));
        // Sağ (+X)
        AddFace(Vector3.Right, new(h, -h, h), new(h, h, h), new(h, h, -h), new(h, -h, -h));
        // Sol (-X)
        AddFace(Vector3.Left, new(-h, -h, -h), new(-h, h, -h), new(-h, h, h), new(-h, -h, h));

        return new Mesh3D(graphicsDevice, "Cube", vertices.ToArray(), indices.ToArray(), PrimitiveType3D.Cube);
    }

    public static Mesh3D CreateSphere(GraphicsDevice graphicsDevice, float radius = 0.5f, int slices = 24, int stacks = 16)
    {
        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<int>();

        for (int stack = 0; stack <= stacks; stack++)
        {
            float phi = MathHelper.Pi * stack / stacks;
            float y = MathF.Cos(phi);
            float r = MathF.Sin(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                float theta = MathHelper.TwoPi * slice / slices;
                float x = r * MathF.Sin(theta);
                float z = r * MathF.Cos(theta);

                var normal = Vector3.Normalize(new Vector3(x, y, z));
                var pos = normal * radius;
                var uv = new Vector2((float)slice / slices, (float)stack / stacks);
                vertices.Add(new VertexPositionNormalTexture(pos, normal, uv));
            }
        }

        int stride = slices + 1;
        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int a = stack * stride + slice;
                int b = a + 1;
                int c = (stack + 1) * stride + slice;
                int d = c + 1;

                indices.Add(a); indices.Add(c); indices.Add(b);
                indices.Add(b); indices.Add(c); indices.Add(d);
            }
        }

        return new Mesh3D(graphicsDevice, "Sphere", vertices.ToArray(), indices.ToArray(), PrimitiveType3D.Sphere);
    }

    public static Mesh3D CreatePlane(GraphicsDevice graphicsDevice, float width = 10f, float depth = 10f)
    {
        float hw = width * 0.5f;
        float hd = depth * 0.5f;

        var vertices = new[]
        {
            new VertexPositionNormalTexture(new(-hw, 0, hd), Vector3.Up, new(0, 1)),
            new VertexPositionNormalTexture(new(-hw, 0, -hd), Vector3.Up, new(0, 0)),
            new VertexPositionNormalTexture(new(hw, 0, -hd), Vector3.Up, new(1, 0)),
            new VertexPositionNormalTexture(new(hw, 0, hd), Vector3.Up, new(1, 1))
        };

        var indices = new[] { 0, 1, 2, 0, 2, 3 };
        return new Mesh3D(graphicsDevice, "Plane", vertices, indices, PrimitiveType3D.Plane);
    }

    public static Mesh3D CreateCylinder(GraphicsDevice graphicsDevice, float radius = 0.5f, float height = 1.5f, int segments = 24)
    {
        float hh = height * 0.5f;
        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<int>();

        // Yan yüzey
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            var normal = new Vector3(cos, 0, sin);
            float u = (float)i / segments;

            vertices.Add(new VertexPositionNormalTexture(new(cos * radius, -hh, sin * radius), normal, new(u, 1)));
            vertices.Add(new VertexPositionNormalTexture(new(cos * radius, hh, sin * radius), normal, new(u, 0)));
        }

        for (int i = 0; i < segments; i++)
        {
            int baseIdx = i * 2;
            indices.Add(baseIdx); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 2); indices.Add(baseIdx + 1); indices.Add(baseIdx + 3);
        }

        // Üst kapak
        int topCenterIdx = vertices.Count;
        vertices.Add(new VertexPositionNormalTexture(new(0, hh, 0), Vector3.Up, new(0.5f, 0.5f)));
        int topRingStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            vertices.Add(new VertexPositionNormalTexture(new(cos * radius, hh, sin * radius), Vector3.Up, new(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f)));
        }
        for (int i = 0; i < segments; i++)
        {
            indices.Add(topCenterIdx);
            indices.Add(topRingStart + i + 1);
            indices.Add(topRingStart + i);
        }

        // Alt kapak
        int botCenterIdx = vertices.Count;
        vertices.Add(new VertexPositionNormalTexture(new(0, -hh, 0), Vector3.Down, new(0.5f, 0.5f)));
        int botRingStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            vertices.Add(new VertexPositionNormalTexture(new(cos * radius, -hh, sin * radius), Vector3.Down, new(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f)));
        }
        for (int i = 0; i < segments; i++)
        {
            indices.Add(botCenterIdx);
            indices.Add(botRingStart + i);
            indices.Add(botRingStart + i + 1);
        }

        return new Mesh3D(graphicsDevice, "Cylinder", vertices.ToArray(), indices.ToArray(), PrimitiveType3D.Cylinder);
    }

    public static Mesh3D CreateCone(GraphicsDevice graphicsDevice, float radius = 0.5f, float height = 1.4f, int segments = 24)
    {
        float hh = height * 0.5f;
        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<int>();

        Vector3 apex = new(0, hh, 0);
        float slope = radius / height;

        for (int i = 0; i < segments; i++)
        {
            float a0 = MathHelper.TwoPi * i / segments;
            float a1 = MathHelper.TwoPi * (i + 1) / segments;

            Vector3 p0 = new(MathF.Cos(a0) * radius, -hh, MathF.Sin(a0) * radius);
            Vector3 p1 = new(MathF.Cos(a1) * radius, -hh, MathF.Sin(a1) * radius);

            Vector3 n0 = Vector3.Normalize(new Vector3(MathF.Cos(a0), slope, MathF.Sin(a0)));
            Vector3 n1 = Vector3.Normalize(new Vector3(MathF.Cos(a1), slope, MathF.Sin(a1)));
            Vector3 nApex = Vector3.Normalize((n0 + n1) * 0.5f);

            int start = vertices.Count;
            vertices.Add(new VertexPositionNormalTexture(apex, nApex, new(0.5f, 0f)));
            vertices.Add(new VertexPositionNormalTexture(p1, n1, new(1f, 1f)));
            vertices.Add(new VertexPositionNormalTexture(p0, n0, new(0f, 1f)));

            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        // Taban kapağı
        int botCenter = vertices.Count;
        vertices.Add(new VertexPositionNormalTexture(new(0, -hh, 0), Vector3.Down, new(0.5f, 0.5f)));
        int ringStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float a = MathHelper.TwoPi * i / segments;
            vertices.Add(new VertexPositionNormalTexture(new(MathF.Cos(a) * radius, -hh, MathF.Sin(a) * radius), Vector3.Down, new(0.5f, 0.5f)));
        }
        for (int i = 0; i < segments; i++)
        {
            indices.Add(botCenter);
            indices.Add(ringStart + i);
            indices.Add(ringStart + i + 1);
        }

        return new Mesh3D(graphicsDevice, "Cone", vertices.ToArray(), indices.ToArray(), PrimitiveType3D.Cone);
    }

    public static Mesh3D CreatePyramid(GraphicsDevice graphicsDevice, float size = 1f, float height = 1.2f)
    {
        float h = size * 0.5f;
        float hh = height * 0.5f;

        Vector3 apex = new(0, hh, 0);
        Vector3 fl = new(-h, -hh, h);
        Vector3 fr = new(h, -hh, h);
        Vector3 br = new(h, -hh, -h);
        Vector3 bl = new(-h, -hh, -h);

        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Normalize(Vector3.Cross(c - a, b - a));
            int s = vertices.Count;
            vertices.Add(new VertexPositionNormalTexture(a, n, new(0.5f, 0f)));
            vertices.Add(new VertexPositionNormalTexture(b, n, new(0f, 1f)));
            vertices.Add(new VertexPositionNormalTexture(c, n, new(1f, 1f)));
            indices.Add(s); indices.Add(s + 1); indices.Add(s + 2);
        }

        AddTri(apex, fl, fr);
        AddTri(apex, fr, br);
        AddTri(apex, br, bl);
        AddTri(apex, bl, fl);

        // Taban
        int bs = vertices.Count;
        vertices.Add(new VertexPositionNormalTexture(bl, Vector3.Down, new(0, 0)));
        vertices.Add(new VertexPositionNormalTexture(br, Vector3.Down, new(1, 0)));
        vertices.Add(new VertexPositionNormalTexture(fr, Vector3.Down, new(1, 1)));
        vertices.Add(new VertexPositionNormalTexture(fl, Vector3.Down, new(0, 1)));
        indices.Add(bs); indices.Add(bs + 1); indices.Add(bs + 2);
        indices.Add(bs); indices.Add(bs + 2); indices.Add(bs + 3);

        return new Mesh3D(graphicsDevice, "Pyramid", vertices.ToArray(), indices.ToArray(), PrimitiveType3D.Pyramid);
    }
}
