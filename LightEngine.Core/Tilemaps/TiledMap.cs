using System.Xml.Linq;
using LightEngine.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Tilemaps;

/// <summary>
/// Tiled .tmx formatındaki tileset bilgisini temsil eder.
/// </summary>
public class TiledTileset
{
    public int FirstGid { get; set; } = 1;
    public string Name { get; set; } = "";
    public int TileWidth { get; set; } = 32;
    public int TileHeight { get; set; } = 32;
    public int Columns { get; set; } = 1;
    public int TileCount { get; set; } = 1;
    public string ImageSource { get; set; } = "";
    public Texture2D? Texture { get; set; }

    public Rectangle GetSourceRect(int gid)
    {
        int localId = gid - FirstGid;
        if (localId < 0 || Columns <= 0) return Rectangle.Empty;

        int col = localId % Columns;
        int row = localId / Columns;
        return new Rectangle(col * TileWidth, row * TileHeight, TileWidth, TileHeight);
    }
}

/// <summary>
/// Tiled harita katmanını temsil eder.
/// </summary>
public class TiledLayer
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int[] Data { get; set; } = Array.Empty<int>();
    public bool IsVisible { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;

    public int GetGid(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
        return Data[y * Width + x];
    }

    public void SetGid(int x, int y, int gid)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            Data[y * Width + x] = gid;
        }
    }
}

/// <summary>
/// Tiled (.tmx) XML harita dosyalarını okuyan ve veri modelini tutan sınıf.
/// </summary>
public class TiledMap
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileWidth { get; set; } = 32;
    public int TileHeight { get; set; } = 32;

    public List<TiledTileset> Tilesets { get; } = new();
    public List<TiledLayer> Layers { get; } = new();

    public int TotalWidthPixels => Width * TileWidth;
    public int TotalHeightPixels => Height * TileHeight;

    /// <summary>
    /// Diskten doğrudan .tmx dosyasını okur ve ilgili tileset dokularını yükler.
    /// </summary>
    public static TiledMap LoadFromFile(string filePath, IAssetService assetService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"[LightEngine] Tilemap dosyası bulunamadı: {filePath}");
        }

        string xmlContent = File.ReadAllText(filePath);
        string? basePath = Path.GetDirectoryName(filePath);
        return LoadFromXml(xmlContent, assetService, basePath);
    }

    /// <summary>
    /// XML metninden Tiled .tmx harita modelini ayrıştırır (Parse eder).
    /// </summary>
    public static TiledMap LoadFromXml(string xmlContent, IAssetService assetService, string? basePath = null)
    {
        var doc = XDocument.Parse(xmlContent);
        var mapElement = doc.Root ?? throw new InvalidOperationException("Geçersiz TMX XML içeriği.");

        var map = new TiledMap
        {
            Width = int.Parse(mapElement.Attribute("width")?.Value ?? "10"),
            Height = int.Parse(mapElement.Attribute("height")?.Value ?? "10"),
            TileWidth = int.Parse(mapElement.Attribute("tilewidth")?.Value ?? "32"),
            TileHeight = int.Parse(mapElement.Attribute("tileheight")?.Value ?? "32")
        };

        // 1. Tileset'leri ayrıştır
        foreach (var tsElem in mapElement.Elements("tileset"))
        {
            var tileset = new TiledTileset
            {
                FirstGid = int.Parse(tsElem.Attribute("firstgid")?.Value ?? "1"),
                Name = tsElem.Attribute("name")?.Value ?? "",
                TileWidth = int.Parse(tsElem.Attribute("tilewidth")?.Value ?? map.TileWidth.ToString()),
                TileHeight = int.Parse(tsElem.Attribute("tileheight")?.Value ?? map.TileHeight.ToString()),
                Columns = int.Parse(tsElem.Attribute("columns")?.Value ?? "1"),
                TileCount = int.Parse(tsElem.Attribute("tilecount")?.Value ?? "1")
            };

            var imgElem = tsElem.Element("image");
            if (imgElem != null)
            {
                string source = imgElem.Attribute("source")?.Value ?? "";
                tileset.ImageSource = source;

                string fullPath = !string.IsNullOrEmpty(basePath) ? Path.Combine(basePath, source) : source;
                tileset.Texture = assetService.LoadTexture(fullPath);
                if (tileset.Texture != null && tileset.TileWidth > 0 && tileset.Columns <= 1)
                {
                    tileset.Columns = Math.Max(1, tileset.Texture.Width / tileset.TileWidth);
                }
            }

            map.Tilesets.Add(tileset);
        }

        // 2. Katmanları (Layers) ayrıştır
        foreach (var layerElem in mapElement.Elements("layer"))
        {
            int layerWidth = int.Parse(layerElem.Attribute("width")?.Value ?? map.Width.ToString());
            int layerHeight = int.Parse(layerElem.Attribute("height")?.Value ?? map.Height.ToString());

            var layer = new TiledLayer
            {
                Name = layerElem.Attribute("name")?.Value ?? "",
                Width = layerWidth,
                Height = layerHeight,
                IsVisible = layerElem.Attribute("visible")?.Value != "0",
                Opacity = float.Parse(layerElem.Attribute("opacity")?.Value ?? "1.0")
            };

            var dataElem = layerElem.Element("data");
            if (dataElem != null)
            {
                string encoding = dataElem.Attribute("encoding")?.Value ?? "csv";
                if (encoding == "csv")
                {
                    string rawCsv = dataElem.Value.Trim();
                    var parts = rawCsv.Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    layer.Data = new int[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        layer.Data[i] = int.Parse(parts[i]);
                    }
                }
            }

            map.Layers.Add(layer);
        }

        return map;
    }

    public TiledTileset? FindTilesetForGid(int gid)
    {
        for (int i = Tilesets.Count - 1; i >= 0; i--)
        {
            if (gid >= Tilesets[i].FirstGid)
            {
                return Tilesets[i];
            }
        }
        return null;
    }
}
