using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;

namespace LightEngine.Editor;

/// <summary>
/// MonoGame DesktopGL üzerinde ImGui.NET arayüzünü çizen ve fare/klavye girdilerini aktaran renderer sınıfı.
/// </summary>
public class ImGuiRenderer : IDisposable
{
    private readonly Game _game;
    private readonly GraphicsDevice _graphicsDevice;
    private BasicEffect _effect = null!;

    // Doku yönetimi
    private Texture2D? _fontTexture;
    private IntPtr _fontTextureId;
    private readonly Dictionary<IntPtr, Texture2D> _loadedTextures = new();
    private int _textureIdCounter = 100;

    // Çizim arabellekleri
    private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[1024];
    private int[] _indexBuffer = new int[2048];

    // Önceki giriş durumları
    private int _prevScrollWheel;

    private static readonly ushort[] s_turkishGlyphRanges = new ushort[]
    {
        0x0020, 0x00FF, // Basic Latin + Latin Supplement (ç, Ç, ö, Ö, ü, Ü dahil)
        0x011E, 0x011F, // Ğ, ğ
        0x0130, 0x0131, // İ, ı
        0x015E, 0x015F, // Ş, ş
        0
    };
    private static GCHandle s_glyphRangesHandle;

    public ImGuiRenderer(Game game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _graphicsDevice = game.GraphicsDevice;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        InitializeEffect();
        RebuildFontAtlas();
        SetupDarkTheme();

        _game.Window.TextInput += OnTextInput;
    }

    private void InitializeEffect()
    {
        _effect = new BasicEffect(_graphicsDevice)
        {
            World = Matrix.Identity,
            View = Matrix.Identity,
            TextureEnabled = true,
            VertexColorEnabled = true
        };
    }

    public unsafe void RebuildFontAtlas()
    {
        var io = ImGui.GetIO();
        io.Fonts.Clear();

        string? fontPath = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string segoe = Path.Combine(winDir, "Fonts", "segoeui.ttf");
            string arial = Path.Combine(winDir, "Fonts", "arial.ttf");
            if (File.Exists(segoe)) fontPath = segoe;
            else if (File.Exists(arial)) fontPath = arial;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] linuxFonts = new[]
            {
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
                "/usr/share/fonts/truetype/ubuntu/Ubuntu-R.ttf"
            };
            foreach (var lf in linuxFonts)
            {
                if (File.Exists(lf)) { fontPath = lf; break; }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string macFont = "/System/Library/Fonts/SFNS.ttf";
            if (!File.Exists(macFont)) macFont = "/Library/Fonts/Arial.ttf";
            if (File.Exists(macFont)) fontPath = macFont;
        }

        if (!string.IsNullOrEmpty(fontPath) && File.Exists(fontPath))
        {
            if (!s_glyphRangesHandle.IsAllocated)
            {
                s_glyphRangesHandle = GCHandle.Alloc(s_turkishGlyphRanges, GCHandleType.Pinned);
            }

            var fontConfig = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig())
            {
                OversampleH = 3,
                OversampleV = 2,
                PixelSnapH = true
            };

            io.Fonts.AddFontFromFileTTF(fontPath, 17.0f, fontConfig, s_glyphRangesHandle.AddrOfPinnedObject());
        }
        else
        {
            io.Fonts.AddFontDefault();
        }

        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

        var pixelData = new byte[width * height * bytesPerPixel];
        Marshal.Copy(pixels, pixelData, 0, pixelData.Length);

        _fontTexture?.Dispose();
        _fontTexture = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
        _fontTexture.SetData(pixelData);

        _fontTextureId = BindTexture(_fontTexture);
        io.Fonts.SetTexID(_fontTextureId);
        io.Fonts.ClearTexData();
    }

    public IntPtr BindTexture(Texture2D texture)
    {
        IntPtr id = new(_textureIdCounter++);
        _loadedTextures[id] = texture;
        return id;
    }

    public void UnbindTexture(IntPtr id)
    {
        _loadedTextures.Remove(id);
    }

    public void Update(GameTime gameTime)
    {
        var io = ImGui.GetIO();
        var viewport = _graphicsDevice.Viewport;

        io.DisplaySize = new System.Numerics.Vector2(viewport.Width, viewport.Height);
        io.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (io.DeltaTime <= 0f) io.DeltaTime = 1f / 60f;

        UpdateInput(io);
    }

    private void UpdateInput(ImGuiIOPtr io)
    {
        var mouse = Mouse.GetState();
        io.MousePos = new System.Numerics.Vector2(mouse.X, mouse.Y);
        io.MouseDown[0] = mouse.LeftButton == ButtonState.Pressed;
        io.MouseDown[1] = mouse.RightButton == ButtonState.Pressed;
        io.MouseDown[2] = mouse.MiddleButton == ButtonState.Pressed;

        int scrollDelta = mouse.ScrollWheelValue - _prevScrollWheel;
        _prevScrollWheel = mouse.ScrollWheelValue;
        io.MouseWheel = scrollDelta > 0 ? 1f : (scrollDelta < 0 ? -1f : 0f);

        var kb = Keyboard.GetState();
        io.KeyCtrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        io.KeyShift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        io.KeyAlt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);

        // Temel tuşlar
        io.AddKeyEvent(ImGuiKey.Tab, kb.IsKeyDown(Keys.Tab));
        io.AddKeyEvent(ImGuiKey.LeftArrow, kb.IsKeyDown(Keys.Left));
        io.AddKeyEvent(ImGuiKey.RightArrow, kb.IsKeyDown(Keys.Right));
        io.AddKeyEvent(ImGuiKey.UpArrow, kb.IsKeyDown(Keys.Up));
        io.AddKeyEvent(ImGuiKey.DownArrow, kb.IsKeyDown(Keys.Down));
        io.AddKeyEvent(ImGuiKey.PageUp, kb.IsKeyDown(Keys.PageUp));
        io.AddKeyEvent(ImGuiKey.PageDown, kb.IsKeyDown(Keys.PageDown));
        io.AddKeyEvent(ImGuiKey.Home, kb.IsKeyDown(Keys.Home));
        io.AddKeyEvent(ImGuiKey.End, kb.IsKeyDown(Keys.End));
        io.AddKeyEvent(ImGuiKey.Delete, kb.IsKeyDown(Keys.Delete));
        io.AddKeyEvent(ImGuiKey.Backspace, kb.IsKeyDown(Keys.Back));
        io.AddKeyEvent(ImGuiKey.Enter, kb.IsKeyDown(Keys.Enter));
        io.AddKeyEvent(ImGuiKey.Escape, kb.IsKeyDown(Keys.Escape));
        io.AddKeyEvent(ImGuiKey.Space, kb.IsKeyDown(Keys.Space));
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Character >= 32 || e.Character == '\t' || e.Character == '\n')
        {
            ImGui.GetIO().AddInputCharacter(e.Character);
        }
    }

    public void BeforeLayout()
    {
        ImGui.NewFrame();
    }

    public void AfterLayout()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        var io = ImGui.GetIO();
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, io.DisplaySize.X, io.DisplaySize.Y, 0, -1, 1);

        var prevRasterizer = _graphicsDevice.RasterizerState;
        var prevDepth = _graphicsDevice.DepthStencilState;
        var prevBlend = _graphicsDevice.BlendState;
        var prevSampler = _graphicsDevice.SamplerStates[0];

        _graphicsDevice.RasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
            DepthClipEnable = false
        };
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

        var viewport = _graphicsDevice.Viewport;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            // Gerekirse apeks arabelleğini büyüt
            if (cmdList.VtxBuffer.Size > _vertexBuffer.Length)
            {
                Array.Resize(ref _vertexBuffer, Math.Max(cmdList.VtxBuffer.Size, _vertexBuffer.Length * 2));
            }

            // Gerekirse indis arabelleğini büyüt
            if (cmdList.IdxBuffer.Size > _indexBuffer.Length)
            {
                Array.Resize(ref _indexBuffer, Math.Max(cmdList.IdxBuffer.Size, _indexBuffer.Length * 2));
            }

            // Köşe verilerini MonoGame vertex yapısına dönüştür
            for (int i = 0; i < cmdList.VtxBuffer.Size; i++)
            {
                var vtx = cmdList.VtxBuffer[i];
                // ImGui Color: RGBA uint
                uint c = vtx.col;
                byte r = (byte)(c & 0xFF);
                byte g = (byte)((c >> 8) & 0xFF);
                byte b = (byte)((c >> 16) & 0xFF);
                byte a = (byte)((c >> 24) & 0xFF);

                _vertexBuffer[i] = new VertexPositionColorTexture(
                    new Vector3(vtx.pos.X, vtx.pos.Y, 0f),
                    new Color(r, g, b, a),
                    new Vector2(vtx.uv.X, vtx.uv.Y)
                );
            }

            for (int i = 0; i < cmdList.IdxBuffer.Size; i++)
            {
                _indexBuffer[i] = (int)cmdList.IdxBuffer[i];
            }

            for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
            {
                var pcmd = cmdList.CmdBuffer[cmdI];

                int primitiveCount = (int)pcmd.ElemCount / 3;
                if (primitiveCount <= 0) continue;

                // Kırpma (Scissor) dikdörtgeni ayarla
                int clipLeft = Math.Clamp((int)pcmd.ClipRect.X, 0, viewport.Width);
                int clipTop = Math.Clamp((int)pcmd.ClipRect.Y, 0, viewport.Height);
                int clipWidth = Math.Clamp((int)(pcmd.ClipRect.Z - pcmd.ClipRect.X), 0, viewport.Width - clipLeft);
                int clipHeight = Math.Clamp((int)(pcmd.ClipRect.W - pcmd.ClipRect.Y), 0, viewport.Height - clipTop);

                if (clipWidth <= 0 || clipHeight <= 0) continue;

                _graphicsDevice.ScissorRectangle = new Rectangle(clipLeft, clipTop, clipWidth, clipHeight);

                // Dokuyu bağla
                if (_loadedTextures.TryGetValue(pcmd.TextureId, out var texture))
                {
                    _effect.Texture = texture;
                }
                else
                {
                    _effect.Texture = _fontTexture;
                }

                int vtxOffset = (int)pcmd.VtxOffset;
                int numVertices = Math.Max(cmdList.VtxBuffer.Size - vtxOffset, 1);

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    _graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertexBuffer,
                        vtxOffset,
                        numVertices,
                        _indexBuffer,
                        (int)pcmd.IdxOffset,
                        primitiveCount
                    );
                }
            }
        }

        _graphicsDevice.RasterizerState = prevRasterizer;
        _graphicsDevice.DepthStencilState = prevDepth;
        _graphicsDevice.BlendState = prevBlend;
        _graphicsDevice.SamplerStates[0] = prevSampler;
    }

    private static void SetupDarkTheme()
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 6.0f;
        style.FrameRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 4.0f;
        style.GrabRounding = 3.0f;
        style.TabRounding = 4.0f;
        style.WindowBorderSize = 1.0f;
        style.FrameBorderSize = 1.0f;

        var colors = style.Colors;
        colors[(int)ImGuiCol.Text] = new System.Numerics.Vector4(0.92f, 0.93f, 0.95f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new System.Numerics.Vector4(0.50f, 0.52f, 0.56f, 1.00f);
        colors[(int)ImGuiCol.WindowBg] = new System.Numerics.Vector4(0.12f, 0.13f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.ChildBg] = new System.Numerics.Vector4(0.14f, 0.15f, 0.18f, 1.00f);
        colors[(int)ImGuiCol.PopupBg] = new System.Numerics.Vector4(0.15f, 0.16f, 0.20f, 0.98f);
        colors[(int)ImGuiCol.Border] = new System.Numerics.Vector4(0.24f, 0.26f, 0.32f, 0.70f);
        colors[(int)ImGuiCol.BorderShadow] = new System.Numerics.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.FrameBg] = new System.Numerics.Vector4(0.18f, 0.20f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.FrameBgHovered] = new System.Numerics.Vector4(0.24f, 0.27f, 0.33f, 1.00f);
        colors[(int)ImGuiCol.FrameBgActive] = new System.Numerics.Vector4(0.28f, 0.32f, 0.38f, 1.00f);
        colors[(int)ImGuiCol.TitleBg] = new System.Numerics.Vector4(0.10f, 0.11f, 0.14f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new System.Numerics.Vector4(0.14f, 0.16f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new System.Numerics.Vector4(0.08f, 0.09f, 0.11f, 1.00f);
        colors[(int)ImGuiCol.MenuBarBg] = new System.Numerics.Vector4(0.10f, 0.11f, 0.14f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarBg] = new System.Numerics.Vector4(0.10f, 0.11f, 0.13f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrab] = new System.Numerics.Vector4(0.24f, 0.27f, 0.33f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = new System.Numerics.Vector4(0.30f, 0.34f, 0.42f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = new System.Numerics.Vector4(0.38f, 0.44f, 0.54f, 1.00f);
        colors[(int)ImGuiCol.CheckMark] = new System.Numerics.Vector4(0.20f, 0.65f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.SliderGrab] = new System.Numerics.Vector4(0.20f, 0.65f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new System.Numerics.Vector4(0.35f, 0.75f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.Button] = new System.Numerics.Vector4(0.20f, 0.23f, 0.28f, 1.00f);
        colors[(int)ImGuiCol.ButtonHovered] = new System.Numerics.Vector4(0.26f, 0.30f, 0.37f, 1.00f);
        colors[(int)ImGuiCol.ButtonActive] = new System.Numerics.Vector4(0.20f, 0.60f, 0.95f, 1.00f);
        colors[(int)ImGuiCol.Header] = new System.Numerics.Vector4(0.20f, 0.24f, 0.30f, 1.00f);
        colors[(int)ImGuiCol.HeaderHovered] = new System.Numerics.Vector4(0.25f, 0.30f, 0.38f, 1.00f);
        colors[(int)ImGuiCol.HeaderActive] = new System.Numerics.Vector4(0.22f, 0.55f, 0.88f, 1.00f);
        style.WindowPadding = new System.Numerics.Vector2(10f, 10f);
        style.FramePadding = new System.Numerics.Vector2(7f, 4f);
        style.ItemSpacing = new System.Numerics.Vector2(8f, 6f);
        style.ItemInnerSpacing = new System.Numerics.Vector2(6f, 4f);

        colors[(int)ImGuiCol.Tab] = new System.Numerics.Vector4(0.14f, 0.16f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.TabHovered] = new System.Numerics.Vector4(0.24f, 0.28f, 0.36f, 1.00f);
        colors[(int)ImGuiCol.TabSelected] = new System.Numerics.Vector4(0.20f, 0.24f, 0.30f, 1.00f);
    }

    public void Dispose()
    {
        if (s_glyphRangesHandle.IsAllocated)
        {
            s_glyphRangesHandle.Free();
        }
        _fontTexture?.Dispose();
        _effect.Dispose();
        _loadedTextures.Clear();
    }
}
