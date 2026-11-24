using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Harici bağımlılık (native DLL) gerektirmeden Wavefront (.obj) ve glTF 2.0 (.gltf / .glb)
/// 3D modellerini doğrudan okuyup GPU Mesh3D nesnesine dönüştüren hafif model yükleyici.
/// </summary>
public static class Model3DLoader
{
    /// <summary>
    /// Dosya uzantısına göre (.obj, .gltf, .glb) 3D modeli yükler.
    /// </summary>
    public static Mesh3D Load(GraphicsDevice graphicsDevice, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"3D model dosyası bulunamadı: {filePath}");
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".obj" => LoadObj(graphicsDevice, filePath),
            ".gltf" => LoadGltf(graphicsDevice, filePath),
            ".glb" => LoadGlb(graphicsDevice, filePath),
            _ => throw new NotSupportedException($"Desteklenmeyen 3D model formatı: {ext}")
        };
    }

    /// <summary>
    /// Wavefront .obj dosyasını (v, vn, vt, f) okuyarak üçgenlenmiş Mesh3D üretir.
    /// </summary>
    public static Mesh3D LoadObj(GraphicsDevice graphicsDevice, string filePath)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        var finalVertices = new List<VertexPositionNormalTexture>();
        var finalIndices = new List<int>();
        var vertexLookup = new Dictionary<string, int>();

        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                    break;
                case "vn" when parts.Length >= 4:
                    normals.Add(Vector3.Normalize(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3]))));
                    break;
                case "vt" when parts.Length >= 3:
                    uvs.Add(new Vector2(ParseFloat(parts[1]), 1f - ParseFloat(parts[2])));
                    break;
                case "f" when parts.Length >= 4:
                {
                    // Çokgenleri üçgen yelpazesine (triangle fan) böl
                    int[] faceIndices = new int[parts.Length - 1];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string key = parts[i];
                        if (!vertexLookup.TryGetValue(key, out int idx))
                        {
                            var tokens = key.Split('/');
                            int vIdx = ResolveObjIndex(tokens[0], positions.Count);
                            int vtIdx = tokens.Length > 1 && !string.IsNullOrEmpty(tokens[1]) ? ResolveObjIndex(tokens[1], uvs.Count) : -1;
                            int vnIdx = tokens.Length > 2 && !string.IsNullOrEmpty(tokens[2]) ? ResolveObjIndex(tokens[2], normals.Count) : -1;

                            Vector3 pos = (vIdx >= 0 && vIdx < positions.Count) ? positions[vIdx] : Vector3.Zero;
                            Vector2 uv = (vtIdx >= 0 && vtIdx < uvs.Count) ? uvs[vtIdx] : Vector2.Zero;
                            Vector3 norm = (vnIdx >= 0 && vnIdx < normals.Count) ? normals[vnIdx] : Vector3.Up;

                            idx = finalVertices.Count;
                            finalVertices.Add(new VertexPositionNormalTexture(pos, norm, uv));
                            vertexLookup[key] = idx;
                        }
                        faceIndices[i - 1] = idx;
                    }

                    for (int i = 1; i < faceIndices.Length - 1; i++)
                    {
                        finalIndices.Add(faceIndices[0]);
                        finalIndices.Add(faceIndices[i]);
                        finalIndices.Add(faceIndices[i + 1]);
                    }
                    break;
                }
            }
        }

        if (normals.Count == 0 && finalVertices.Count > 0)
        {
            ComputeFlatNormals(finalVertices, finalIndices);
        }

        return new Mesh3D(
            graphicsDevice,
            Path.GetFileNameWithoutExtension(filePath),
            finalVertices.ToArray(),
            finalIndices.ToArray(),
            PrimitiveType3D.Custom,
            filePath);
    }

    /// <summary>
    /// glTF 2.0 JSON (.gltf) dosyasının ilk mesh primitifini okur (harici .bin veya base64 veri URI destekli).
    /// </summary>
    public static Mesh3D LoadGltf(GraphicsDevice graphicsDevice, string filePath)
    {
        string json = File.ReadAllText(filePath);
        string baseDir = Path.GetDirectoryName(filePath) ?? "";
        return ParseGltfJson(graphicsDevice, json, null, baseDir, filePath);
    }

    /// <summary>
    /// İkili glTF 2.0 (.glb) kapsayıcı dosyasını doğrudan okur.
    /// </summary>
    public static Mesh3D LoadGlb(GraphicsDevice graphicsDevice, string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67) // "glTF"
        {
            throw new InvalidDataException("Geçersiz GLB dosya başlığı.");
        }

        int offset = 12;
        string? jsonChunk = null;
        byte[]? binChunk = null;

        while (offset + 8 <= bytes.Length)
        {
            int chunkLen = BitConverter.ToInt32(bytes, offset);
            uint chunkType = BitConverter.ToUInt32(bytes, offset + 4);
            offset += 8;

            if (offset + chunkLen > bytes.Length) break;

            if (chunkType == 0x4E4F534A) // JSON
            {
                jsonChunk = Encoding.UTF8.GetString(bytes, offset, chunkLen);
            }
            else if (chunkType == 0x004E4942) // BIN
            {
                binChunk = new byte[chunkLen];
                Buffer.BlockCopy(bytes, offset, binChunk, 0, chunkLen);
            }

            offset += chunkLen;
        }

        if (string.IsNullOrEmpty(jsonChunk))
        {
            throw new InvalidDataException("GLB içinde JSON bloğu bulunamadı.");
        }

        return ParseGltfJson(graphicsDevice, jsonChunk, binChunk, Path.GetDirectoryName(filePath) ?? "", filePath);
    }

    private static Mesh3D ParseGltfJson(GraphicsDevice graphicsDevice, string json, byte[]? embeddedBin, string baseDir, string filePath)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Buffer'ları yükle
        var buffers = new List<byte[]>();
        if (root.TryGetProperty("buffers", out var buffersElem))
        {
            foreach (var buf in buffersElem.EnumerateArray())
            {
                if (buf.TryGetProperty("uri", out var uriProp))
                {
                    string uri = uriProp.GetString() ?? "";
                    if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = uri.IndexOf(',');
                        buffers.Add(Convert.FromBase64String(uri[(comma + 1)..]));
                    }
                    else
                    {
                        string cleanUri = uri.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                        buffers.Add(File.ReadAllBytes(Path.Combine(baseDir, cleanUri)));
                    }
                }
                else if (embeddedBin != null)
                {
                    buffers.Add(embeddedBin);
                }
            }
        }

        var bufferViews = root.GetProperty("bufferViews");
        var accessors = root.GetProperty("accessors");
        var meshes = root.GetProperty("meshes");
        var firstPrimitive = meshes[0].GetProperty("primitives")[0];
        var attrs = firstPrimitive.GetProperty("attributes");

        Vector3[] positions = ReadVec3Accessor(accessors[attrs.GetProperty("POSITION").GetInt32()], bufferViews, buffers);
        Vector3[] normals = attrs.TryGetProperty("NORMAL", out var nIdx)
            ? ReadVec3Accessor(accessors[nIdx.GetInt32()], bufferViews, buffers)
            : new Vector3[positions.Length];
        Vector2[] uvs = attrs.TryGetProperty("TEXCOORD_0", out var uvIdx)
            ? ReadVec2Accessor(accessors[uvIdx.GetInt32()], bufferViews, buffers)
            : new Vector2[positions.Length];

        int[] indices;
        if (firstPrimitive.TryGetProperty("indices", out var indIdx))
        {
            indices = ReadIndexAccessor(accessors[indIdx.GetInt32()], bufferViews, buffers);
        }
        else
        {
            indices = new int[positions.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
        }

        var vertices = new List<VertexPositionNormalTexture>(positions.Length);
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 n = (i < normals.Length && normals[i].LengthSquared() > 0.001f) ? normals[i] : Vector3.Up;
            Vector2 uv = i < uvs.Length ? uvs[i] : Vector2.Zero;
            vertices.Add(new VertexPositionNormalTexture(positions[i], n, uv));
        }

        if (!attrs.TryGetProperty("NORMAL", out _))
        {
            ComputeFlatNormals(vertices, indices);
        }

        return new Mesh3D(
            graphicsDevice,
            Path.GetFileNameWithoutExtension(filePath),
            vertices.ToArray(),
            indices,
            PrimitiveType3D.Custom,
            filePath);
    }

    private static Vector3[] ReadVec3Accessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        int bvIndex = accessor.GetProperty("bufferView").GetInt32();
        int count = accessor.GetProperty("count").GetInt32();
        int accOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var bv = bufferViews[bvIndex];
        int bufIndex = bv.GetProperty("buffer").GetInt32();
        int bvOffset = bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
        int stride = bv.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 12;

        byte[] data = buffers[bufIndex];
        var result = new Vector3[count];
        int start = bvOffset + accOffset;
        for (int i = 0; i < count; i++)
        {
            int pos = start + i * stride;
            result[i] = new Vector3(
                BitConverter.ToSingle(data, pos),
                BitConverter.ToSingle(data, pos + 4),
                BitConverter.ToSingle(data, pos + 8));
        }
        return result;
    }

    private static Vector2[] ReadVec2Accessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        int bvIndex = accessor.GetProperty("bufferView").GetInt32();
        int count = accessor.GetProperty("count").GetInt32();
        int accOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var bv = bufferViews[bvIndex];
        int bufIndex = bv.GetProperty("buffer").GetInt32();
        int bvOffset = bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
        int stride = bv.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 8;

        byte[] data = buffers[bufIndex];
        var result = new Vector2[count];
        int start = bvOffset + accOffset;
        for (int i = 0; i < count; i++)
        {
            int pos = start + i * stride;
            result[i] = new Vector2(
                BitConverter.ToSingle(data, pos),
                BitConverter.ToSingle(data, pos + 4));
        }
        return result;
    }

    private static int[] ReadIndexAccessor(JsonElement accessor, JsonElement bufferViews, List<byte[]> buffers)
    {
        int bvIndex = accessor.GetProperty("bufferView").GetInt32();
        int count = accessor.GetProperty("count").GetInt32();
        int compType = accessor.GetProperty("componentType").GetInt32();
        int accOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;

        var bv = bufferViews[bvIndex];
        int bufIndex = bv.GetProperty("buffer").GetInt32();
        int bvOffset = bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;

        byte[] data = buffers[bufIndex];
        var result = new int[count];
        int start = bvOffset + accOffset;

        for (int i = 0; i < count; i++)
        {
            result[i] = compType switch
            {
                5121 => data[start + i],                             // UNSIGNED_BYTE
                5123 => BitConverter.ToUInt16(data, start + i * 2),  // UNSIGNED_SHORT
                5125 => (int)BitConverter.ToUInt32(data, start + i * 4), // UNSIGNED_INT
                _ => 0
            };
        }
        return result;
    }

    private static void ComputeFlatNormals(List<VertexPositionNormalTexture> vertices, IList<int> indices)
    {
        var accum = new Vector3[vertices.Count];
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];
            Vector3 p0 = vertices[i0].Position;
            Vector3 p1 = vertices[i1].Position;
            Vector3 p2 = vertices[i2].Position;
            Vector3 n = Vector3.Cross(p2 - p0, p1 - p0);
            if (n.LengthSquared() > 0.000001f) n.Normalize();
            accum[i0] += n;
            accum[i1] += n;
            accum[i2] += n;
        }
        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            Vector3 n = accum[i].LengthSquared() > 0.000001f ? Vector3.Normalize(accum[i]) : Vector3.Up;
            vertices[i] = new VertexPositionNormalTexture(v.Position, n, v.TextureCoordinate);
        }
    }

    private static int ResolveObjIndex(string token, int count)
    {
        int raw = int.Parse(token, CultureInfo.InvariantCulture);
        return raw < 0 ? count + raw : raw - 1;
    }

    private static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
