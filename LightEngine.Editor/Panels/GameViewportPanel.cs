using System;
using ImGuiNET;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

public enum GameAspectRatio
{
    FreeAspect,
    SixteenByNine,
    SixteenByTen,
    FourByThree
}

/// <summary>
/// Oyunun gerçek çalışma zamanı (Game Runtime) görünüm paneli.
/// Editör sahnesinden bağımsız olarak saf oyun kamerasını çizer;
/// ızgara (grid), gizmo, nesne seçimi ve sahne düzenleme kesinlikle KAPALIDIR.
/// Play modunda oyun bu ekranda doğrudan oynanır.
/// </summary>
public class GameViewportPanel : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ImGuiRenderer _imGuiRenderer;
    private readonly EditorContext _context;

    private RenderTarget2D? _renderTarget;
    private IntPtr _renderTargetTextureId;
    private int _targetWidth = 1280;
    private int _targetHeight = 720;

    public GameAspectRatio AspectRatio { get; set; } = GameAspectRatio.SixteenByNine;
    public bool MaximizeOnPlay { get; set; } = false;
    public bool IsHovered { get; private set; }
    public bool IsFocused { get; private set; }

    public GameViewportPanel(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer, EditorContext context)
    {
        _graphicsDevice = graphicsDevice;
        _imGuiRenderer = imGuiRenderer;
        _context = context;

        RecreateRenderTarget(_targetWidth, _targetHeight);
    }

    private void RecreateRenderTarget(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        if (_renderTargetTextureId != IntPtr.Zero)
        {
            _imGuiRenderer.UnbindTexture(_renderTargetTextureId);
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.DiscardContents);

        _renderTargetTextureId = _imGuiRenderer.BindTexture(_renderTarget);
        _targetWidth = width;
        _targetHeight = height;
    }

    /// <summary>
    /// Saf oyun kamerasını render target içine çizer (Hiçbir editör çizgisi/gizmo/ızgara olmadan).
    /// </summary>
    public void RenderGameToTarget(IScene scene, SpriteBatchRenderer renderer)
    {
        if (_renderTarget == null || scene == null) return;

        _graphicsDevice.SetRenderTarget(_renderTarget);

        if (_context.Is3DMode)
        {
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(15, 18, 24), 1.0f, 0);

            if (Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
            {
                // Saf oyun kamerası (Izgara: false, Seçim: null)
                scene.Camera3D.AspectRatio = (float)_targetWidth / Math.Max(1, _targetHeight);
                r3d.RenderScene(scene, scene.Camera3D, drawEditorGrid: false, selectedEntity: null);
            }

            _graphicsDevice.SetRenderTarget(null);
            return;
        }

        // 2D Modu: Sadece sahne varlıkları çizilir, ızgara veya gizmo çizilmez
        _graphicsDevice.Clear(new Color(20, 22, 28));
        scene.Camera.Origin = new Vector2(_targetWidth / 2f, _targetHeight / 2f);

        renderer.Begin(transformMatrix: scene.Camera.ViewMatrix);
        scene.Draw(renderer);
        renderer.End();

        _graphicsDevice.SetRenderTarget(null);
    }

    public void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVec2(0, 0));

        if (ImGui.Begin("Game Viewport##GameViewportPanel", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            // Play / Stop durumunda sekme odaklanma isteği
            if (_context.FocusGameTabRequested)
            {
                ImGui.SetWindowFocus();
                _context.FocusGameTabRequested = false;
            }

            IsFocused = ImGui.IsWindowFocused();

            // Üst Oyun Araç Çubuğu
            DrawGameHeader();

            var availSize = ImGui.GetContentRegionAvail();
            if (availSize.X > 50 && availSize.Y > 50)
            {
                // En-Boy oranına göre boyut hesaplama
                NumVec2 imageSize = CalculateAspectSize(availSize);
                NumVec2 imageOffset = new((availSize.X - imageSize.X) * 0.5f, (availSize.Y - imageSize.Y) * 0.5f);

                int desiredW = Math.Max((int)imageSize.X, 320);
                int desiredH = Math.Max((int)imageSize.Y, 180);

                if (Math.Abs(desiredW - _targetWidth) > 4 || Math.Abs(desiredH - _targetHeight) > 4)
                {
                    RecreateRenderTarget(desiredW, desiredH);
                }

                NumVec2 cursorStart = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new NumVec2(cursorStart.X + imageOffset.X, cursorStart.Y + imageOffset.Y));

                NumVec2 screenImageStart = ImGui.GetCursorScreenPos();

                if (_renderTarget != null && _renderTargetTextureId != IntPtr.Zero)
                {
                    ImGui.Image(_renderTargetTextureId, imageSize);
                }

                // Buton ile tıklandığında focus al
                ImGui.SetCursorScreenPos(screenImageStart);
                ImGui.InvisibleButton("##GameCanvas", imageSize);
                IsHovered = ImGui.IsItemHovered();

                // Eğer oyun çalışmıyorsa ortada hoş bir "Hazır / Başlat" overlay'i göster
                if (!_context.IsPlaying)
                {
                    DrawStandbyOverlay(screenImageStart, imageSize);
                }
            }
        }
        ImGui.End();

        ImGui.PopStyleVar();
    }

    private void DrawGameHeader()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVec2(6, 3));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVec4(0.11f, 0.12f, 0.15f, 1f));

        if (ImGui.BeginChild("##GameHeader", new NumVec2(0, 28), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.SetCursorPos(new NumVec2(8, 4));

            // Durum rozeti
            if (_context.IsPlaying)
            {
                if (_context.IsPaused)
                {
                    ImGui.TextColored(new NumVec4(1f, 0.8f, 0.2f, 1f), "[PAUSED]");
                }
                else
                {
                    ImGui.TextColored(new NumVec4(0.3f, 0.95f, 0.4f, 1f), "[PLAYING]");
                }
            }
            else
            {
                ImGui.TextColored(new NumVec4(0.6f, 0.65f, 0.75f, 1f), "[STANDBY]");
            }

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 15);

            // Aspect Ratio seçici
            ImGui.SetNextItemWidth(120);
            string[] aspects = ["Free Aspect", "16:9", "16:10", "4:3"];
            int currentAspect = (int)AspectRatio;
            if (ImGui.Combo("##AspectRatio", ref currentAspect, aspects, aspects.Length))
            {
                AspectRatio = (GameAspectRatio)currentAspect;
            }

            ImGui.SameLine(0, 15);
            bool maxOnPlay = MaximizeOnPlay;
            if (ImGui.Checkbox("Maximize on Play", ref maxOnPlay))
            {
                MaximizeOnPlay = maxOnPlay;
            }

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled($"Res: {_targetWidth}x{_targetHeight} px");

            ImGui.SameLine(0, 20);
            if (ImGui.SmallButton("<< Switch to Scene View"))
            {
                _context.FocusSceneTabRequested = true;
            }
        }
        ImGui.EndChild();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private NumVec2 CalculateAspectSize(NumVec2 availSize)
    {
        float targetAspect = AspectRatio switch
        {
            GameAspectRatio.SixteenByNine => 16f / 9f,
            GameAspectRatio.SixteenByTen => 16f / 10f,
            GameAspectRatio.FourByThree => 4f / 3f,
            _ => availSize.X / Math.Max(1f, availSize.Y)
        };

        float w = availSize.X;
        float h = w / targetAspect;

        if (h > availSize.Y)
        {
            h = availSize.Y;
            w = h * targetAspect;
        }

        return new NumVec2(MathF.Floor(w), MathF.Floor(h));
    }

    private void DrawStandbyOverlay(NumVec2 screenPos, NumVec2 size)
    {
        var drawList = ImGui.GetWindowDrawList();
        NumVec2 center = new(screenPos.X + size.X * 0.5f, screenPos.Y + size.Y * 0.5f);

        // Yarı saydam koyu zemin
        drawList.AddRectFilled(
            new NumVec2(center.X - 180, center.Y - 50),
            new NumVec2(center.X + 180, center.Y + 50),
            ImGui.ColorConvertFloat4ToU32(new NumVec4(0.06f, 0.07f, 0.09f, 0.88f)),
            8f);

        drawList.AddRect(
            new NumVec2(center.X - 180, center.Y - 50),
            new NumVec2(center.X + 180, center.Y + 50),
            ImGui.ColorConvertFloat4ToU32(new NumVec4(0.25f, 0.35f, 0.5f, 0.6f)),
            8f);

        // Metinler
        string title = "GAME RUNTIME VIEW";
        string sub = "Press Play [ > ] on Toolbar to run and play";
        NumVec2 titleSize = ImGui.CalcTextSize(title);
        NumVec2 subSize = ImGui.CalcTextSize(sub);

        drawList.AddText(new NumVec2(center.X - titleSize.X * 0.5f, center.Y - 32), ImGui.ColorConvertFloat4ToU32(new NumVec4(0.9f, 0.95f, 1f, 1f)), title);
        drawList.AddText(new NumVec2(center.X - subSize.X * 0.5f, center.Y - 6), ImGui.ColorConvertFloat4ToU32(new NumVec4(0.6f, 0.7f, 0.8f, 0.9f)), sub);

        // İpucu
        string hint = "(Editing is disabled here - Switch to 'Scene' tab to edit objects)";
        NumVec2 hintSize = ImGui.CalcTextSize(hint);
        drawList.AddText(new NumVec2(center.X - hintSize.X * 0.5f, center.Y + 18), ImGui.ColorConvertFloat4ToU32(new NumVec4(0.45f, 0.85f, 0.55f, 0.8f)), hint);
    }

    public void Dispose()
    {
        if (_renderTargetTextureId != IntPtr.Zero)
        {
            _imGuiRenderer.UnbindTexture(_renderTargetTextureId);
            _renderTargetTextureId = IntPtr.Zero;
        }

        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}
