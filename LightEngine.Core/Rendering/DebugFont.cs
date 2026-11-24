using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core.Rendering;

/// <summary>
/// Harici yazı tipi (.xnb) dosyasına ihtiyaç duymadan FPS, hata ayıklama ve performans
/// metinlerini çizmek için sıfır bağımlılıklı, gömülü 8x8 piksel yazı tipi.
/// </summary>
public class DebugFont : IDisposable
{
    private readonly Texture2D _fontAtlas;
    private readonly int _charWidth = 8;
    private readonly int _charHeight = 8;
    private readonly int _columns = 16;

    public Texture2D Atlas => _fontAtlas;
    public int CharWidth => _charWidth;
    public int CharHeight => _charHeight;

    public DebugFont(GraphicsDevice graphicsDevice)
    {
        _fontAtlas = GenerateAtlas(graphicsDevice);
    }

    /// <summary>
    /// Belirtilen metni SpriteBatch üzerinden çizer.
    /// </summary>
    public void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text)) return;

        Vector2 currentPos = position;
        float stepX = _charWidth * scale;
        float stepY = _charHeight * scale;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                currentPos.X = position.X;
                currentPos.Y += stepY;
                continue;
            }

            Rectangle srcRect = GetCharRect(c);
            spriteBatch.Draw(_fontAtlas, currentPos, srcRect, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            currentPos.X += stepX;
        }
    }

    private Rectangle GetCharRect(char c)
    {
        int charCode = (int)c;
        if (charCode < 32 || charCode > 126) charCode = 63; // '?'

        int index = charCode - 32;
        int col = index % _columns;
        int row = index / _columns;

        return new Rectangle(col * _charWidth, row * _charHeight, _charWidth, _charHeight);
    }

    private Texture2D GenerateAtlas(GraphicsDevice device)
    {
        int atlasWidth = _columns * _charWidth;
        int atlasHeight = 8 * _charHeight;
        var texture = new Texture2D(device, atlasWidth, atlasHeight);
        var pixels = new Color[atlasWidth * atlasHeight];
        Array.Fill(pixels, Color.Transparent);

        for (int charCode = 32; charCode <= 126; charCode++)
        {
            int index = charCode - 32;
            int col = index % _columns;
            int row = index / _columns;
            int startX = col * _charWidth;
            int startY = row * _charHeight;

            byte[] glyph = GetGlyphData((char)charCode);

            for (int y = 0; y < 7; y++)
            {
                byte rowBits = y < glyph.Length ? glyph[y] : (byte)0;
                for (int x = 0; x < 5; x++)
                {
                    if ((rowBits & (1 << (4 - x))) != 0)
                    {
                        int px = startX + x + 1;
                        int py = startY + y;
                        pixels[(py * atlasWidth) + px] = Color.White;
                    }
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static byte[] GetGlyphData(char c)
    {
        // 5x7 kompakt bit desenleri (her byte bir satırın 5 bitini temsil eder)
        return c switch
        {
            ' ' => new byte[] { 0, 0, 0, 0, 0, 0, 0 },
            '!' => new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100, 0 },
            '"' => new byte[] { 0b01010, 0b01010, 0, 0, 0, 0, 0 },
            '#' => new byte[] { 0b01010, 0b11111, 0b01010, 0b11111, 0b01010, 0, 0 },
            '$' => new byte[] { 0b00100, 0b01111, 0b10100, 0b01110, 0b00101, 0b11110, 0b00100 },
            '%' => new byte[] { 0b11001, 0b11010, 0b00100, 0b01000, 0b01011, 0b10011, 0 },
            '&' => new byte[] { 0b01100, 0b10010, 0b01100, 0b10010, 0b10001, 0b01111, 0 },
            '\'' => new byte[] { 0b00100, 0b00100, 0, 0, 0, 0, 0 },
            '(' => new byte[] { 0b00010, 0b00100, 0b01000, 0b01000, 0b00100, 0b00010, 0 },
            ')' => new byte[] { 0b01000, 0b00100, 0b00010, 0b00010, 0b00100, 0b01000, 0 },
            '*' => new byte[] { 0b00000, 0b01010, 0b00100, 0b11111, 0b00100, 0b01010, 0 },
            '+' => new byte[] { 0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0 },
            ',' => new byte[] { 0, 0, 0, 0, 0b00100, 0b00100, 0b01000 },
            '-' => new byte[] { 0, 0, 0, 0b11111, 0, 0, 0 },
            '.' => new byte[] { 0, 0, 0, 0, 0, 0b00100, 0 },
            '/' => new byte[] { 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0, 0 },

            '0' => new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
            '1' => new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            '2' => new byte[] { 0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111 },
            '3' => new byte[] { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 },
            '4' => new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
            '5' => new byte[] { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 },
            '6' => new byte[] { 0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
            '7' => new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
            '8' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
            '9' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100 },

            ':' => new byte[] { 0, 0b00100, 0, 0, 0b00100, 0, 0 },
            ';' => new byte[] { 0, 0b00100, 0, 0, 0b00100, 0b01000, 0 },
            '<' => new byte[] { 0b00010, 0b00100, 0b01000, 0b00100, 0b00010, 0, 0 },
            '=' => new byte[] { 0, 0b11111, 0, 0b11111, 0, 0, 0 },
            '>' => new byte[] { 0b01000, 0b00100, 0b00010, 0b00100, 0b01000, 0, 0 },
            '?' => new byte[] { 0b01110, 0b10001, 0b00010, 0b00100, 0b00100, 0b00000, 0b00100 },
            '@' => new byte[] { 0b01110, 0b10001, 0b00001, 0b01101, 0b10101, 0b10110, 0b01100 },

            'A' => new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            'B' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
            'C' => new byte[] { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 },
            'D' => new byte[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 },
            'E' => new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            'F' => new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
            'G' => new byte[] { 0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111 },
            'H' => new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            'I' => new byte[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            'J' => new byte[] { 0b00001, 0b00001, 0b00001, 0b00001, 0b10001, 0b10001, 0b01110 },
            'K' => new byte[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
            'L' => new byte[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
            'M' => new byte[] { 0b10001, 0b11011, 0b10101, 0b10001, 0b10001, 0b10001, 0b10001 },
            'N' => new byte[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
            'O' => new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            'P' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
            'Q' => new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
            'R' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
            'S' => new byte[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
            'T' => new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            'U' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            'V' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            'W' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001 },
            'X' => new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
            'Y' => new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
            'Z' => new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },

            '[' => new byte[] { 0b01110, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01110 },
            '\\' => new byte[] { 0b10000, 0b01000, 0b00100, 0b00010, 0b00001, 0, 0 },
            ']' => new byte[] { 0b01110, 0b00010, 0b00010, 0b00010, 0b00010, 0b00010, 0b01110 },
            '^' => new byte[] { 0b00100, 0b01010, 0b10001, 0, 0, 0, 0 },
            '_' => new byte[] { 0, 0, 0, 0, 0, 0, 0b11111 },
            '`' => new byte[] { 0b01000, 0b00100, 0, 0, 0, 0, 0 },

            // Küçük harfler (aynı font stiliyle kolay okunur)
            'a' => new byte[] { 0, 0, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111 },
            'b' => new byte[] { 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b11110 },
            'c' => new byte[] { 0, 0, 0b01110, 0b10000, 0b10000, 0b10001, 0b01110 },
            'd' => new byte[] { 0b00001, 0b00001, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111 },
            'e' => new byte[] { 0, 0, 0b01110, 0b10001, 0b11111, 0b10000, 0b01110 },
            'f' => new byte[] { 0b00110, 0b01000, 0b11110, 0b01000, 0b01000, 0b01000, 0b01000 },
            'g' => new byte[] { 0, 0, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110 },
            'h' => new byte[] { 0b10000, 0b10000, 0b10110, 0b11001, 0b10001, 0b10001, 0b10001 },
            'i' => new byte[] { 0b00100, 0, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110 },
            'j' => new byte[] { 0b00010, 0, 0b00110, 0b00010, 0b00010, 0b10010, 0b01100 },
            'k' => new byte[] { 0b10000, 0b10000, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010 },
            'l' => new byte[] { 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            'm' => new byte[] { 0, 0, 0b11010, 0b10101, 0b10101, 0b10101, 0b10101 },
            'n' => new byte[] { 0, 0, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001 },
            'o' => new byte[] { 0, 0, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110 },
            'p' => new byte[] { 0, 0, 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000 },
            'q' => new byte[] { 0, 0, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001 },
            'r' => new byte[] { 0, 0, 0b10110, 0b11001, 0b10000, 0b10000, 0b10000 },
            's' => new byte[] { 0, 0, 0b01110, 0b10000, 0b01110, 0b00001, 0b11110 },
            't' => new byte[] { 0b01000, 0b01000, 0b11110, 0b01000, 0b01000, 0b01000, 0b00110 },
            'u' => new byte[] { 0, 0, 0b10001, 0b10001, 0b10001, 0b10011, 0b01101 },
            'v' => new byte[] { 0, 0, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            'w' => new byte[] { 0, 0, 0b10001, 0b10001, 0b10101, 0b10101, 0b01010 },
            'x' => new byte[] { 0, 0, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001 },
            'y' => new byte[] { 0, 0, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110 },
            'z' => new byte[] { 0, 0, 0b11111, 0b00010, 0b00100, 0b01000, 0b11111 },

            '{' => new byte[] { 0b00011, 0b00010, 0b00010, 0b00100, 0b00010, 0b00010, 0b00011 },
            '|' => new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            '}' => new byte[] { 0b11000, 0b01000, 0b01000, 0b00100, 0b01000, 0b01000, 0b11000 },
            '~' => new byte[] { 0b01000, 0b10101, 0b00010, 0, 0, 0, 0 },

            _ => new byte[] { 0b11111, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11111 }
        };
    }

    public void Dispose()
    {
        _fontAtlas.Dispose();
    }
}
