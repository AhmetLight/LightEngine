using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering3D;

/// <summary>
/// Statik ışıklandırma (Half-Life 2 / Source Engine tarzı) için önceden pişirilmiş (baked)
/// 2D ışık haritası dokusu (Lightmap) verisi ve üreticisi.
/// Düşük GPU maliyetle yumuşak gölge, ortam tıkanıklığı (AO) ve renk sıçraması sağlar.
/// </summary>
public static class LightmapBaker3D
{
    /// <summary>
    /// Verilen sahnedeki statik 3D ışıkları ve nesne gölgelerini örnekleyerek
    /// zemin/statik yüzeyler için yumuşak bir baked Lightmap Texture2D üretir.
    /// </summary>
    public static Texture2D BakePlanarLightmap(
        GraphicsDevice graphicsDevice,
        IScene scene,
        BoundingBox areaBounds,
        int resolution = 128,
        Color? ambientColor = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(scene);

        int res = Math.Clamp(resolution, 16, 1024);
        var pixels = new Color[res * res];
        Vector3 ambient = (ambientColor ?? new Color(68, 74, 88)).ToVector3();

        // Sahnedeki ışık ve engelleyici kutuları topla
        var lights = new List<(Light3D Light, Vector3 Pos)>();
        var occluders = new List<BoundingBox>();

        foreach (var e in scene.Entities)
        {
            if (!e.IsActive || e.IsDestroyed) continue;
            var t3d = e.GetComponent<Transform3D>();
            var l = e.GetComponent<Light3D>();
            if (l != null && l.IsEnabled)
            {
                lights.Add((l, t3d?.WorldPosition ?? new Vector3(0, 6, 0)));
            }

            var mr = e.GetComponent<MeshRenderer3D>();
            if (mr != null && mr.IsEnabled && mr.CastShadows && mr.Mesh != null && mr.Mesh.PrimitiveType != PrimitiveType3D.Plane)
            {
                occluders.Add(mr.GetWorldBoundingBox());
            }
        }

        if (lights.Count == 0)
        {
            var fallbackLight = new Light3D { LightType = LightType3D.Directional, Color = new Color(255, 245, 228), Intensity = 1.0f };
            lights.Add((fallbackLight, new Vector3(4, 10, 4)));
        }

        float sizeX = Math.Max(0.01f, areaBounds.Max.X - areaBounds.Min.X);
        float sizeZ = Math.Max(0.01f, areaBounds.Max.Z - areaBounds.Min.Z);
        float ySurface = areaBounds.Min.Y + 0.02f;

        for (int py = 0; py < res; py++)
        {
            float v = (py + 0.5f) / res;
            float worldZ = areaBounds.Min.Z + v * sizeZ;

            for (int px = 0; px < res; px++)
            {
                float u = (px + 0.5f) / res;
                float worldX = areaBounds.Min.X + u * sizeX;
                Vector3 samplePos = new(worldX, ySurface, worldZ);

                Vector3 accumLight = ambient;

                for (int li = 0; li < lights.Count; li++)
                {
                    var (light, lightPos) = lights[li];
                    Vector3 toLight;
                    float attenuation = 1f;
                    float maxDist = 200f;

                    if (light.LightType == LightType3D.Directional)
                    {
                        toLight = -light.Direction;
                        if (toLight.LengthSquared() < 0.001f) toLight = Vector3.Up;
                        toLight.Normalize();
                    }
                    else
                    {
                        Vector3 diff = lightPos - samplePos;
                        float dist = diff.Length();
                        if (dist > light.Range || dist < 0.001f) continue;
                        toLight = diff / dist;
                        maxDist = dist;
                        attenuation = 1f - (dist / light.Range);
                        attenuation *= attenuation;
                    }

                    float ndotl = Math.Max(0f, Vector3.Dot(Vector3.Up, toLight));
                    if (ndotl <= 0f) continue;

                    // Ray-AABB gölge testi
                    bool inShadow = false;
                    Ray shadowRay = new(samplePos, toLight);
                    for (int oi = 0; oi < occluders.Count; oi++)
                    {
                        float? hit = shadowRay.Intersects(occluders[oi]);
                        if (hit.HasValue && hit.Value > 0.02f && hit.Value < maxDist)
                        {
                            inShadow = true;
                            break;
                        }
                    }

                    float visibility = inShadow ? 0.25f : 1.0f;
                    accumLight += light.Color.ToVector3() * (light.Intensity * ndotl * attenuation * visibility);
                }

                // Yumuşak ton sıkıştırma (Reinhard tone map)
                accumLight = new Vector3(
                    accumLight.X / (1f + accumLight.X * 0.4f),
                    accumLight.Y / (1f + accumLight.Y * 0.4f),
                    accumLight.Z / (1f + accumLight.Z * 0.4f));

                pixels[py * res + px] = new Color(
                    Math.Clamp(accumLight.X, 0f, 1f),
                    Math.Clamp(accumLight.Y, 0f, 1f),
                    Math.Clamp(accumLight.Z, 0f, 1f),
                    1f);
            }
        }

        // 3x3 kutu bulanıklaştırma ile yumuşak gölge kenarları (HL2 radiosity hissi)
        var blurred = BoxBlur3x3(pixels, res);
        var tex = new Texture2D(graphicsDevice, res, res);
        tex.SetData(blurred);
        return tex;
    }

    private static Color[] BoxBlur3x3(Color[] src, int res)
    {
        var dst = new Color[src.Length];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                int r = 0, g = 0, b = 0, count = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = Math.Clamp(y + dy, 0, res - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Math.Clamp(x + dx, 0, res - 1);
                        var c = src[ny * res + nx];
                        r += c.R; g += c.G; b += c.B;
                        count++;
                    }
                }
                dst[y * res + x] = new Color(r / count, g / count, b / count, 255);
            }
        }
        return dst;
    }
}
